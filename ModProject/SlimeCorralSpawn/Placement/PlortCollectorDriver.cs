using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppSiloStorage = Il2Cpp.SiloStorage;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>Fuerza aspirado de plorts en corrales custom (vanilla a veces no arranca el ciclo).</summary>
    internal static class PlortCollectorDriver
    {
        private static float _nextTick;
        private const float TickInterval = 0.4f;

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
                if (!CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
                    continue;
                SuckForPlot(lp);
            }
        }

        private static void SuckForPlot(Il2CppLandPlot lp)
        {
            var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
            var pc = CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
            if (pc == null) return;

            PrepareCollector(lp, pc, pcu);

            try { pc.DoCollection(); } catch { }
        }

        private static void PrepareCollector(Il2CppLandPlot lp, Il2Cpp.PlortCollector pc,
            Il2Cpp.PlortCollectorUpgrader pcu)
        {
            try
            {
                if (pc.gameObject != null && !pc.gameObject.activeSelf)
                    pc.gameObject.SetActive(true);
                if (!pc.enabled) pc.enabled = true;
            }
            catch { }

            CorralRegistrationHelper.EnsurePlotRegion(lp);

            try { if (lp._region != null) pc._region = lp._region; } catch { }

            Il2CppSiloStorage silo = null;
            try { silo = pc._storage; } catch { }
            if (silo == null)
            {
                try
                {
                    var go = pcu?.Collector;
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
            if (silo != null)
            {
                try { pc._storage = silo; } catch { }
            }

            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc != null && pc._timeDir == null)
                    pc._timeDir = sc.TimeDirector;
            }
            catch { }

            try
            {
                var area = pc.CollectionArea;
                if (area != null)
                {
                    if (!area.enabled) area.enabled = true;
                    if (area.gameObject != null && !area.gameObject.activeSelf)
                        area.gameObject.SetActive(true);
                }
            }
            catch { }

            try { pc.StartCollection(); } catch { }
        }
    }
}
