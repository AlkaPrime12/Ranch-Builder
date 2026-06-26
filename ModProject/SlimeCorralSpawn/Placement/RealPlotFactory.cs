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

            var sc = Il2Cpp.SceneContext.Instance;
            if (sc == null) return;

            var lpl = lp.transform.parent != null ? lp.transform.parent.GetComponent<Il2CppLandPlotLocation>() : null;
            var model = lpl != null ? sc.GameModel.GetLandPlotModel(lpl._id) : null;
            if (model != null)
            {
                lp.InitModel(model);
                FeederSpeedHelper.RestoreToModel(model, pd);
            }

            foreach (var u in pd.PurchasedUpgrades)
            {
                if (!Enum.TryParse<Il2CppLandPlot.Upgrade>(u, out var up)) continue;
                if (lp.HasUpgrade(up)) continue;
                try { lp.AddUpgrade(up); } catch { }
            }

            // Apply + cableado van en RegisterAndInitialize (después de RegisterToRanchMetadata).
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

            sc.GameModel.RegisterLandPlot(lpl._id, obj);

            Deferred.Run(() =>
            {
                var emptyPrefab = gc.LookupDirector.GetPlotPrefab(Il2CppLandPlot.Id.EMPTY);
                if (emptyPrefab == null) return;
                var plotObj = UnityEngine.Object.Instantiate(emptyPrefab, obj.transform);

                if (plotId != Il2CppLandPlot.Id.EMPTY)
                {
                    Deferred.Run(() =>
                    {
                        var landPlot = plotObj.GetComponent<Il2CppLandPlot>();
                        if (landPlot == null) landPlot = obj.GetComponentInChildren<Il2CppLandPlot>();
                        var targetPrefab = gc.LookupDirector.GetPlotPrefab(plotId);
                        if (landPlot == null || targetPrefab == null) return;

                        var newPlot = lpl.Replace(landPlot, targetPrefab);

                        // Re-initialize the model now that the LandPlot is CORRAL (not EMPTY).
                        // InitializeLandPlotModel creates a model with internal state matching
                        // the current LandPlot type — calling it after Replace ensures the model
                        // supports CORRAL-specific upgrades (WALLS, AIR_NET, etc.).
                        sc.GameModel.InitializeLandPlotModel(lpl._id);

                        Deferred.Run(() =>
                        {
                            var newLp = newPlot != null ? newPlot.GetComponent<Il2CppLandPlot>() : null;
                            if (newLp == null) newLp = obj.GetComponentInChildren<Il2CppLandPlot>();
                            ApplySavedUpgrades(newLp, plotKey);
                            CorralRegistrationHelper.SyncUpgradeVisibility(newLp);

                            Deferred.Run(() =>
                            {
                                var pdc = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                                if (pdc != null)
                                {
                                    var lp2 = newLp ?? obj.GetComponentInChildren<Il2CppLandPlot>();
                                    CorralRegistrationHelper.RegisterPlotForInit(lp2, plotKey);
                                }
                            }, 6);
                        }, 4);
                    }, 3);
                }
                else
                {
                    var pd = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                    if (pd != null) pd.ContentReady = true;
                }
            }, 2);

            return obj;
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

        public static void ResetRoots()
        {
            _roots.Clear();
            OurLocations.Clear();
            CorralRegistrationHelper.ClearRegistrationState();
        }
    }
}
