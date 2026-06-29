using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
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

        internal static void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;
            if (!RealPlotFactory.ContextReady()) return;

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
                    if (CountFood(lp, crop) >= MAX_CROPS) { nextDrop = now + interval; break; }
                    SpawnFood(lp, crop);
                    nextDrop += interval;
                    drops++;
                }
                _nextDrop[pd.UniqueId] = nextDrop;
            }
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

        private static int CountFood(Il2CppLandPlot lp, Il2Cpp.IdentifiableType crop)
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
                    try { if (id.identType != null && id.identType.name == crop.name) c++; } catch { }
                }
            }
            catch { }
            return c;
        }

        private static void SpawnFood(Il2CppLandPlot lp, Il2Cpp.IdentifiableType crop)
        {
            try
            {
                // 1) Intentar SpawnResource del juego (tamaño real, aspirable, registrado en región).
                var sr = lp.GetComponentInChildren<Il2Cpp.SpawnResource>(true);
                if (sr != null)
                {
                    foreach (var name in new[] { "TrySpawnResource", "Spawn", "ForceSpawn" })
                    {
                        try
                        {
                            var m = AccessTools.Method(typeof(Il2Cpp.SpawnResource), name);
                            if (m != null) { m.Invoke(sr, null); return; }
                        }
                        catch { }
                    }
                }

                // 2) Fallback manual: Instantiate + registro en región.
                UnityEngine.GameObject prefab = crop.prefab;
                if (prefab == null) return;

                Vector3 basePos = lp.transform.position;
                Vector3 pos = basePos + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 1.6f, UnityEngine.Random.Range(-1.5f, 1.5f));
                var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (go == null) return;
                if (!go.activeSelf) go.SetActive(true);

                // Forzar registro en la región para que la vacaspiradora lo reconozca.
                RegisterInRegion(go, lp);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GardenDriver.SpawnFood", ex); }
        }

        private static void RegisterInRegion(GameObject go, Il2CppLandPlot lp)
        {
            Il2Cpp.Identifiable ident = null;
            try { ident = go.GetComponent<Il2Cpp.Identifiable>(); } catch { }
            if (ident == null) try { ident = go.GetComponentInChildren<Il2Cpp.Identifiable>(true); } catch { }
            if (ident == null) return;

            // Asignar la región del garden al Identifiable para que la vacaspiradora lo reconozca.
            try
            {
                var regionField = AccessTools.Field(typeof(Il2Cpp.Identifiable), "_region");
                if (regionField != null && lp._region != null)
                    regionField.SetValue(ident, lp._region);
            }
            catch { }
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

        internal static void Reset() { _nextDrop.Clear(); _lastRealSpawn.Clear(); }
    }
}
