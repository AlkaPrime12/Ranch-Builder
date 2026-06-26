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
        private const float TickInterval = 0.35f;

        internal static void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;
            if (!RealPlotFactory.ContextReady()) return;

            foreach (var pd in Plots.PlotData.GetAll())
            {
                if (pd?.LinkedObject == null) continue;
                Il2CppLandPlot lp = null;
                try { lp = pd.LinkedObject.GetComponentInChildren<Il2CppLandPlot>(true); } catch { }
                if (lp == null || !Patches.GamePatches.IsOurLandPlot(lp)) continue;
                if (!CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.FEEDER))
                    continue;
                TickFeeder(lp);
            }
        }

        private static void TickFeeder(Il2CppLandPlot lp)
        {
            var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
            var sf = CorralRegistrationHelper.ResolveSlimeFeeder(fu, lp);
            if (sf == null) return;

            if (!PrepareFeeder(lp, sf, fu)) return;

            try
            {
                if (sf.ShouldFeed())
                    sf.ProcessFeedOperation(true);
            }
            catch { }
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
