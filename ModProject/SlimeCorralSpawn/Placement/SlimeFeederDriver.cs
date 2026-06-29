using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppSiloStorage = Il2Cpp.SiloStorage;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Fuerza el ciclo vanilla del SlimeFeeder en corrales custom:
    /// FixedUpdate a veces no avanza si el cableado/registro llegó tarde.
    /// </summary>
    internal static class SlimeFeederDriver
    {
        private static float _nextTick;
        private const float TickInterval = 1.5f;   // antes 0.35s: iteraba todos los plots 3x/seg = lag

        internal static void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;
            if (!RealPlotFactory.ContextReady()) return;

            foreach (var pd in Plots.PlotData.GetAll())
            {
                if (pd?.LinkedObject == null) continue;
                Il2CppLandPlot lp = pd.GetLandPlot();   // cacheado
                if (lp == null || !Patches.GamePatches.IsOurLandPlot(lp)) continue;
                if (!CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.FEEDER))
                    continue;
                TickFeeder(lp);
            }
        }

        // Cadencia de feeding forzado por feeder (segundos reales), según velocidad.
        private static readonly System.Collections.Generic.Dictionary<int, float> _nextFeed
            = new System.Collections.Generic.Dictionary<int, float>();

        private static void TickFeeder(Il2CppLandPlot lp)
        {
            var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
            var sf = CorralRegistrationHelper.ResolveSlimeFeeder(fu, lp);
            if (sf == null) return;

            if (!PrepareFeeder(lp, sf, fu)) return;

            // 1) Condición vanilla (si el juego decide alimentar, perfecto).
            try { if (sf.ShouldFeed()) { sf.ProcessFeedOperation(true); return; } }
            catch { }

            // 2) FORZADO paceado: en plots custom ShouldFeed() suele dar false. Si hay comida en el silo
            //    del feeder, escupir una tanda cada X seg (según la velocidad puesta).
            try
            {
                int food = 0;
                try { food = sf.GetFoodCount(); } catch { }
                if (food <= 0) return;

                int id = sf.GetInstanceID();
                float now = Time.time;
                if (_nextFeed.TryGetValue(id, out var next) && now < next) return;
                _nextFeed[id] = now + FeedIntervalSeconds(sf);

                // El "escupir" literal es EjectFood(ammo): expulsa físicamente la comida al corral.
                // ProcessFeedOperation por sí solo a veces NO eyecta en plots custom; EjectFood directo SÍ.
                // PERO EjectFood NO descuenta del silo → hay que llamar ammo.Decrement(foodId,1) por cada comida
                // (si no, la comida nunca baja). API: AmmoSlotManager.Decrement(IdentifiableType id, int count).
                bool ejected = false;
                try
                {
                    var storage = sf._storage;
                    var ammo = storage != null ? storage.GetRelevantAmmo() : null;
                    if (ammo != null)
                    {
                        Il2Cpp.IdentifiableType foodId = null;
                        try { foodId = sf.GetFoodId(); } catch { }

                        // SOLO 1 comida por ciclo (el ritmo lo da FeedIntervalSeconds según la modalidad).
                        // Antes eyectaba ItemsPerFeeding veces → tiraba todo de una.
                        int before = food;
                        try { sf.EjectFood(ammo); ejected = true; } catch { }

                        // Descontar lo que realmente bajó; si EjectFood no descontó, bajar 1 a mano.
                        int afterEject = before;
                        try { afterEject = sf.GetFoodCount(); } catch { }
                        if (afterEject >= before && foodId != null)
                        {
                            try { ammo.Decrement(foodId, 1); } catch { }
                        }
                    }
                }
                catch { }
                if (!ejected)
                {
                    try { sf.ProcessFeedOperation(true); } catch { }
                }
            }
            catch { }
        }

        private static float FeedIntervalSeconds(Il2Cpp.SlimeFeeder sf)
        {
            try
            {
                string n = sf.GetFeedingCycleSpeed().ToString();
                if (n.IndexOf("Fast", System.StringComparison.OrdinalIgnoreCase) >= 0) return 7f;
                if (n.IndexOf("Slow", System.StringComparison.OrdinalIgnoreCase) >= 0) return 22f;
            }
            catch { }
            return 13f; // Normal
        }

        private static bool PrepareFeeder(Il2CppLandPlot lp, Il2Cpp.SlimeFeeder sf, Il2Cpp.FeederUpgrader fu)
        {
            try
            {
                if (sf.gameObject != null && !sf.gameObject.activeSelf)
                    sf.gameObject.SetActive(true);
                if (!sf.enabled) sf.enabled = true;
            }
            catch { }

            CorralRegistrationHelper.EnsurePlotRegion(lp);

            try { if (lp._region != null) sf._region = lp._region; } catch { }

            Il2CppSiloStorage silo = null;
            try { silo = sf._storage; } catch { }
            if (silo == null)
            {
                try
                {
                    var go = fu?.Feeder;
                    if (go != null) silo = go.GetComponentInChildren<Il2CppSiloStorage>(true);
                }
                catch { }
            }
            if (silo == null)
            {
                try
                {
                    var arr = lp.GetComponentsInChildren<Il2CppSiloStorage>(true);
                    if (arr != null && arr.Length > 0) silo = arr[0];
                }
                catch { }
            }
            if (silo == null) return false;
            try { sf._storage = silo; } catch { }

            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc != null && sf._timeDir == null)
                    sf._timeDir = sc.TimeDirector;
            }
            catch { }

            FeederSpeedHelper.ApplySpeedToFeeder(lp);
            return true;
        }
    }
}
