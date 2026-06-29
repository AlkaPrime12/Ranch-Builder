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
        private static readonly Dictionary<string, int> _replaceRetries = new Dictionary<string, int>();
        private const int MaxReplaceRetries = 4;

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

            // TODO ENVUELTO: si el modelo aún no está listo, LandPlot.HasUpgrade/AddUpgrade tiran NRE
            // (excepción nativa IL2CPP). Antes se propagaba y ABORTABA toda la finalización del plot
            // (no se cableaban feeder/collector/jardín). Ahora cada llamada está protegida.
            try
            {
                var lpl = lp.transform.parent != null ? lp.transform.parent.GetComponent<Il2CppLandPlotLocation>() : null;
                var model = lpl != null ? sc.GameModel.GetLandPlotModel(lpl._id) : null;
                if (model == null) { try { model = lp._model; } catch { } }   // fallback al modelo propio
                if (model != null)
                {
                    try { lp.InitModel(model); } catch { }
                    try { FeederSpeedHelper.RestoreToModel(model, pd); } catch { }
                }

                foreach (var u in pd.PurchasedUpgrades)
                {
                    if (!Enum.TryParse<Il2CppLandPlot.Upgrade>(u, out var up)) continue;
                    bool has = false;
                    try { has = lp.HasUpgrade(up); } catch { has = false; }   // modelo no listo => seguir
                    if (has) continue;
                    try { lp.AddUpgrade(up); } catch { }
                }
            }
            catch (Exception e) { ModEntry.LogErrorOnce("ApplySavedUpgrades", e); }

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
            EnsurePlotModel(sc, lpl._id);

            Deferred.Run(() =>
            {
                if (obj == null || lpl == null) return;
                EnsurePlotModel(sc, lpl._id);

                var emptyPrefab = gc.LookupDirector.GetPlotPrefab(Il2CppLandPlot.Id.EMPTY);
                if (emptyPrefab == null) return;
                var emptyObj = UnityEngine.Object.Instantiate(emptyPrefab, obj.transform);

                if (plotId != Il2CppLandPlot.Id.EMPTY)
                {
                    Deferred.Run(() =>
                    {
                        TryReplacePlot(sc, gc, lpl, obj, emptyObj, plotKey, plotId, 0);
                    }, 2);
                }
                else
                {
                    var pd = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                    if (pd != null) pd.ContentReady = true;
                }
            }, 1);

            return obj;
        }

        private static void EnsurePlotModel(Il2Cpp.SceneContext sc, string plotModelId)
        {
            if (sc == null || string.IsNullOrEmpty(plotModelId)) return;
            try
            {
                if (sc.GameModel.GetLandPlotModel(plotModelId) != null) return;
                sc.GameModel.InitializeLandPlotModel(plotModelId);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("EnsurePlotModel." + plotModelId, ex); }
        }

        private static void TryReplacePlot(Il2Cpp.SceneContext sc, Il2Cpp.GameContext gc,
            Il2CppLandPlotLocation lpl, GameObject obj, GameObject plotObj, string plotKey,
            Il2CppLandPlot.Id plotId, int attempt)
        {
            if (lpl == null || obj == null) return;

            EnsurePlotModel(sc, lpl._id);
            var model = sc.GameModel.GetLandPlotModel(lpl._id);
            if (model == null)
            {
                if (attempt < MaxReplaceRetries)
                    Deferred.Run(() => TryReplacePlot(sc, gc, lpl, obj, plotObj, plotKey, plotId, attempt + 1), 4);
                return;
            }

            Il2CppLandPlot landPlot = null;
            try
            {
                if (plotObj != null)
                    landPlot = plotObj.GetComponent<Il2CppLandPlot>();
                if (landPlot == null)
                    landPlot = obj.GetComponentInChildren<Il2CppLandPlot>();
            }
            catch { }

            var targetPrefab = gc.LookupDirector.GetPlotPrefab(plotId);
            if (landPlot == null || targetPrefab == null)
            {
                if (attempt < MaxReplaceRetries)
                    Deferred.Run(() => TryReplacePlot(sc, gc, lpl, obj, plotObj, plotKey, plotId, attempt + 1), 4);
                return;
            }

            try { landPlot.InitModel(model); } catch { }

            GameObject newPlotGo = null;
            try
            {
                newPlotGo = lpl.Replace(landPlot, targetPrefab);
            }
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("Replace." + plotKey + "." + attempt, ex);
                if (attempt < MaxReplaceRetries)
                    Deferred.Run(() => TryReplacePlot(sc, gc, lpl, obj, plotObj, plotKey, plotId, attempt + 1), 5);
                return;
            }

            if (newPlotGo == null)
            {
                if (attempt < MaxReplaceRetries)
                    Deferred.Run(() => TryReplacePlot(sc, gc, lpl, obj, plotObj, plotKey, plotId, attempt + 1), 5);
                return;
            }

            _replaceRetries.Remove(plotKey);
            sc.GameModel.InitializeLandPlotModel(lpl._id);

            if (plotObj != null && plotObj != newPlotGo)
            {
                try { UnityEngine.Object.Destroy(plotObj); } catch { }
            }

            FinalizeSpawnedPlot(sc, obj, plotKey, lpl, newPlotGo);
        }

        private static void FinalizeSpawnedPlot(Il2Cpp.SceneContext sc, GameObject obj, string plotKey,
            Il2CppLandPlotLocation lpl, GameObject plotGo)
        {
            Deferred.Run(() =>
            {
                Il2CppLandPlot newLp = null;
                try { newLp = plotGo != null ? plotGo.GetComponent<Il2CppLandPlot>() : null; } catch { }
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
                }, 3);
            }, 2);
        }

        public static void ResetRoots()
        {
            _roots.Clear();
            OurLocations.Clear();
            _replaceRetries.Clear();
            _cachedCellDirectors = null;
            CorralRegistrationHelper.ClearRegistrationState();
        }

        internal static IEnumerable<GameObject> GetAllRoots()
        {
            foreach (var r in _roots.Values)
                if (r != null) yield return r;
        }

        private static Il2CppCellDirector[] _cachedCellDirectors;
        private static float _cellDirectorCacheTime;

        private static Il2CppCellDirector[] GetCellDirectors()
        {
            if (Time.time - _cellDirectorCacheTime > 30f)
            {
                _cachedCellDirectors = UnityEngine.Object.FindObjectsOfType<Il2CppCellDirector>();
                _cellDirectorCacheTime = Time.time;
            }
            return _cachedCellDirectors;
        }

        private static GameObject GetRootForPosition(Vector3 pos)
        {
            var cells = GetCellDirectors();
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
    }
}
