using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;

namespace SlimeCorralSpawn.Placement
{
    internal static class GardenDriver
    {
        private const int MaxCrops = 50;
        private static readonly Collider[] _cropBuffer = new Collider[MaxCrops];
        private static float _nextTick;
        private const float TickInterval = 3f;
        private const int MAX_CROPS = 24;
        private const int MAX_CATCHUP = 8;
        private const double MIN_INTERVAL_H = 2.0;
        private const double DEFAULT_INTERVAL_H = 6.0;
        private const float REAL_TIME_COOLDOWN = 45f;

        private static readonly Dictionary<string, double> _nextDrop = new Dictionary<string, double>();
        private static readonly Dictionary<string, float> _lastRealSpawn = new Dictionary<string, float>();

        // Cache nombre → IdentifiableType: se llena en la primera llamada a Update()
        // cuando el juego ya ha cargado todos los tipos de recursos.
        private static Dictionary<string, Il2Cpp.IdentifiableType> _typeCache = null;

        internal static void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;
            if (!RealPlotFactory.ContextReady()) return;

            // Llenar cache de tipos si aún no existe
            if (_typeCache == null) BuildTypeCache();

            Il2Cpp.TimeDirector timeDir = null;
            try { var sc = Il2Cpp.SceneContext.Instance; if (sc != null) timeDir = sc.TimeDirector; } catch { }
            if (timeDir == null) return;
            double now; try { now = timeDir.WorldTime(); } catch { return; }

            foreach (var pd in Plots.PlotData.GetAll())
            {
                if (pd?.LinkedObject == null) continue;
                Il2CppLandPlot lp = pd.GetLandPlot();
                if (lp == null || !Patches.GamePatches.IsOurLandPlot(lp)) continue;

                double interval = Math.Max(GetIntervalHours(lp), MIN_INTERVAL_H);

                Il2Cpp.IdentifiableType crop = lp.GetAttachedCropId();
                if (crop == null) continue;

                // Obtener el tipo de COMIDA real (recolectable), no el crop plantado.
                Il2Cpp.IdentifiableType foodType = ResolveFoodType(crop);

                if (!_nextDrop.TryGetValue(pd.UniqueId, out var nextDrop))
                {
                    _nextDrop[pd.UniqueId] = now;
                    continue;
                }
                if (now < nextDrop) continue;

                float rn = Time.time;
                string uid = pd.UniqueId;
                if (_lastRealSpawn.TryGetValue(uid, out var lastSpawn) && rn - lastSpawn < REAL_TIME_COOLDOWN)
                    continue;
                _lastRealSpawn[uid] = rn;

                int drops = 0;
                while (now >= nextDrop && drops < MAX_CATCHUP)
                {
                    if (CountFood(lp, foodType) >= MAX_CROPS) { nextDrop = now + interval; break; }
                    SpawnFood(lp, foodType);
                    nextDrop += interval;
                    drops++;
                }
                _nextDrop[pd.UniqueId] = nextDrop;
            }
        }

        /// <summary>Escanea todos los IdentifiableType del juego y los guarda por nombre.
        /// Se llama una sola vez en la primera Update() en el ranch.</summary>
        private static void BuildTypeCache()
        {
            _typeCache = new Dictionary<string, Il2Cpp.IdentifiableType>();
            try
            {
                var all = UnityEngine.Resources.FindObjectsOfTypeAll<Il2Cpp.IdentifiableType>();
                if (all != null)
                    foreach (var t in all)
                        if (t != null && t.name != null && !_typeCache.ContainsKey(t.name))
                            _typeCache[t.name] = t;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GardenDriver.BuildTypeCache", ex); }
        }

        private static double GetIntervalHours(Il2CppLandPlot lp)
        {
            try
            {
                var sr = lp.GetComponentInChildren<Il2Cpp.SpawnResource>(true);
                if (sr != null)
                {
                    var def = sr._resourceGrowerDefinition;
                    if (def != null) { float h = def.MinSpawnIntervalGameHours; return Math.Max(h, (float)MIN_INTERVAL_H); }
                }
            }
            catch { }
            return DEFAULT_INTERVAL_H;
        }

        /// <summary>Dado el crop plantado (ej: "PogofruitPlant"), devuelve el
        /// IdentifiableType de la COMIDA real (ej: "Pogofruit"). Usa el cache.</summary>
        private static Il2Cpp.IdentifiableType ResolveFoodType(Il2Cpp.IdentifiableType crop)
        {
            string name = crop.name;
            if (name != null && name.EndsWith("Plant"))
            {
                string foodName = name.Substring(0, name.Length - 5);
                Il2Cpp.IdentifiableType ft;
                if (_typeCache != null && _typeCache.TryGetValue(foodName, out ft))
                    return ft;
            }
            return crop;
        }

        private static int CountFood(Il2CppLandPlot lp, Il2Cpp.IdentifiableType foodType)
        {
            int c = 0;
            try
            {
                int n = Physics.OverlapSphereNonAlloc(lp.transform.position, 10f, _cropBuffer);
                for (int i = 0; i < n; i++)
                {
                    var col = _cropBuffer[i]; if (col == null) continue;
                    Il2Cpp.Identifiable id = null;
                    try { id = col.GetComponentInParent<Il2Cpp.Identifiable>(); } catch { }
                    if (id == null) continue;
                    try { if (id.identType != null && id.identType.name == foodType.name) c++; } catch { }
                }
            }
            catch { }
            return c;
        }

        /// <summary>Spawn simple: Instantiate del prefab de comida correcto.
        /// Sin AccessTools, sin Traverse, sin intentar registrar en región.
        /// El prefab de comida real ya trae el IdentifiableType correcto y es
        /// directamente aspirable por la vacaspiradora.</summary>
        private static void SpawnFood(Il2CppLandPlot lp, Il2Cpp.IdentifiableType foodType)
        {
            try
            {
                GameObject prefab = foodType.prefab;
                if (prefab == null) return;

                Vector3 basePos = lp.transform.position;
                Vector3 pos = basePos + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 1.6f, UnityEngine.Random.Range(-1.5f, 1.5f));
                var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (go == null) return;
                go.transform.localScale = Vector3.one;
                if (!go.activeSelf) go.SetActive(true);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GardenDriver.SpawnFood", ex); }
        }

        internal static void ResetTimer(string uniqueId, double now, double interval)
        {
            _nextDrop[uniqueId] = now + interval;
        }

        internal static double GetCurrentInterval(Il2CppLandPlot lp)
        {
            double interval = DEFAULT_INTERVAL_H;
            try
            {
                var sr = lp.GetComponentInChildren<Il2Cpp.SpawnResource>(true);
                if (sr != null)
                {
                    var def = sr._resourceGrowerDefinition;
                    if (def != null) interval = Math.Max((double)def.MinSpawnIntervalGameHours, MIN_INTERVAL_H);
                }
            }
            catch { }
            return Math.Max(interval, MIN_INTERVAL_H);
        }

        internal static void Reset() { _nextDrop.Clear(); _lastRealSpawn.Clear(); _typeCache = null; }
    }
}
