using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;
using Il2CppCellDirector = Il2Cpp.CellDirector;

namespace SlimeCorralSpawn.Placement
{
    public static class RealPlotFactory
    {
        public static readonly HashSet<Il2CppLandPlotLocation> OurLocations = new HashSet<Il2CppLandPlotLocation>();
        private static readonly Dictionary<string, GameObject> _roots = new Dictionary<string, GameObject>();

        public static bool IsOurLocation(Il2CppLandPlotLocation loc)
        {
            return loc != null && OurLocations.Contains(loc);
        }

        public static void ApplySavedUpgrades(Il2CppLandPlot lp, string plotKey)
        {
            if (lp == null) return;
            var pd = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
            if (pd == null || pd.PurchasedUpgrades == null || pd.PurchasedUpgrades.Count == 0) return;
            foreach (var u in pd.PurchasedUpgrades)
            {
                if (!Enum.TryParse<Il2CppLandPlot.Upgrade>(u, out var up)) continue;
                if (lp.HasUpgrade(up)) continue;
                lp.AddUpgrade(up);
            }
        }

        public static bool ContextReady()
        {
            return Il2Cpp.SceneContext.Instance != null && Il2Cpp.GameContext.Instance != null;
        }

        public static GameObject SpawnRealPlot(string plotKey, Vector3 pos, Quaternion rot, Il2CppLandPlot.Id plotId)
        {
            var sc = Il2Cpp.SceneContext.Instance;
            var gc = Il2Cpp.GameContext.Instance;
            if (sc == null || gc == null) return null;

            GameObject root = GetRootForPosition(pos);
            var obj = new GameObject(plotKey);
            var lpl = obj.AddComponent<Il2CppLandPlotLocation>();
            OurLocations.Add(lpl);
            lpl._id = "plot" + plotKey;
            if (root != null) obj.transform.SetParent(root.transform);
            obj.transform.position = pos;
            obj.transform.rotation = rot;

            if (plotId == Il2CppLandPlot.Id.NONE) plotId = Il2CppLandPlot.Id.EMPTY;

            // Register BEFORE Instantiate — same key everywhere (lpl._id).
            // LandPlot.Start() will find its model via lpl._id on the next frame.
            sc.GameModel.RegisterLandPlot(lpl._id, obj);
            sc.GameModel.InitializeLandPlotModel(lpl._id);

            Deferred.Run(() =>
            {
                var emptyPrefab = gc.LookupDirector.GetPlotPrefab(Il2CppLandPlot.Id.EMPTY);
                var plotObj = UnityEngine.Object.Instantiate(emptyPrefab, obj.transform);
                lpl.enabled = true;

                if (plotId != Il2CppLandPlot.Id.EMPTY)
                {
                    TryReplace(lpl, plotObj, obj, plotId, plotKey);
                }
                else
                {
                    var pd = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                    if (pd != null) pd.ContentReady = true;
                }
            }, 2);

            return obj;
        }

        private static void TryReplace(Il2CppLandPlotLocation lpl, GameObject plotObj, GameObject obj,
                                        Il2CppLandPlot.Id plotId, string plotKey)
        {
            Deferred.Run(() =>
            {
                var sc = Il2Cpp.SceneContext.Instance;
                var gc = Il2Cpp.GameContext.Instance;
                if (lpl == null || obj == null || sc == null || gc == null) return;

                var landPlot = plotObj.GetComponent<Il2CppLandPlot>();
                if (landPlot == null) landPlot = obj.GetComponentInChildren<Il2CppLandPlot>();
                var targetPrefab = gc.LookupDirector.GetPlotPrefab(plotId);
                if (landPlot == null || targetPrefab == null) return;

                // Associate model with LandPlot before Replace
                var model = sc.GameModel.GetLandPlotModel(lpl._id);
                if (model != null) landPlot.InitModel(model);

                var newPlot = lpl.Replace(landPlot, targetPrefab);

                Deferred.Run(() =>
                {
                    var newLp = newPlot != null ? newPlot.GetComponent<Il2CppLandPlot>() : null;
                    if (newLp == null) newLp = obj.GetComponentInChildren<Il2CppLandPlot>();
                    ApplySavedUpgrades(newLp, plotKey);

                    Deferred.Run(() =>
                    {
                        var pdc = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                        if (pdc != null)
                        {
                            var lp2 = newLp ?? obj.GetComponentInChildren<Il2CppLandPlot>();
                            if (!string.IsNullOrEmpty(pdc.GardenCropId) || pdc.SiloContent.Count > 0)
                                SlimeCorralSpawn.Plots.ContentPersistence.RestoreContent(lp2, pdc);
                            pdc.ContentReady = true;
                        }
                    }, 6);
                }, 4);
            }, 3);
        }

        private static GameObject GetRootForPosition(Vector3 pos)
        {
            var cells = UnityEngine.Object.FindObjectsOfType<Il2CppCellDirector>();
            Il2CppCellDirector best = null; float bestDist = float.MaxValue;
            if (cells != null)
                foreach (var c in cells)
                {
                    if (c == null) continue;
                    float d = (c.transform.position - pos).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = c; }
                }
            if (best == null) return null;
            string sceneName = best.gameObject.scene.name;
            if (_roots.TryGetValue(sceneName, out var r) && r != null) return r;
            var go = new GameObject("SCS_PlotRoot_" + sceneName);
            try { SceneManager.MoveGameObjectToScene(go, best.gameObject.scene); } catch { }
            go.transform.SetParent(best.transform);
            _roots[sceneName] = go;
            return go;
        }

        public static void ResetRoots() { _roots.Clear(); OurLocations.Clear(); }
    }
}
