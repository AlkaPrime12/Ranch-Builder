using System;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Elimina previews huérfanos (cuadrícula azul) y reintenta plots que quedaron en EMPTY.
    /// </summary>
    internal static class SceneArtifactCleanup
    {
        private static float _nextSweep;

        internal static void Tick()
        {
            if (Time.time < _nextSweep) return;
            _nextSweep = Time.time + 30f;
            if (!RealPlotFactory.ContextReady()) return;

            DestroyOrphanGhosts();
            RetryStuckEmptyPlots();
        }

        internal static void OnSceneLoaded()
        {
            _nextSweep = 0f;
            try { PlacementManager.ResetLitTemplates(); } catch { }
            DestroyOrphanGhosts();
        }

        private static void DestroyOrphanGhosts()
        {
            try
            {
                foreach (var r in RealPlotFactory.GetAllRoots())
                {
                    if (r == null) continue;
                    var ghosts = r.GetComponentsInChildren<Transform>(true);
                    if (ghosts == null) continue;
                    foreach (var t in ghosts)
                    {
                        if (t == null) continue;
                        string n = t.name;
                        if (n == "RealPlotGhostPreview" || n == "PlacementGhost")
                            UnityEngine.Object.Destroy(t.gameObject);
                    }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneArtifactCleanup.Ghosts", ex); }
        }

        private static void RetryStuckEmptyPlots()
        {
            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                var gc = Il2Cpp.GameContext.Instance;
                if (sc == null || gc == null) return;

                foreach (var pd in Plots.PlotData.GetAll())
                {
                    if (pd?.LinkedObject == null || pd.PlotType == PlotType.Empty || pd.PlotType == PlotType.House)
                        continue;

                    var lpl = pd.LinkedObject.GetComponent<Il2CppLandPlotLocation>();
                    if (lpl == null || !RealPlotFactory.IsOurLocation(lpl)) continue;

                    Il2CppLandPlot lp = null;
                    lp = pd.GetLandPlot();
                    if (lp == null) continue;

                    Il2CppLandPlot.Id expected = RealPlotManager.ToRealId(pd.PlotType);
                    if (expected == Il2CppLandPlot.Id.EMPTY) continue;

                    Il2CppLandPlot.Id current;
                    try { current = lp.GetPlotId(); } catch { continue; }
                    if (current == expected) continue;

                    var targetPrefab = gc.LookupDirector.GetPlotPrefab(expected);
                    if (targetPrefab == null) continue;

                    EnsurePlotModel(sc, lpl._id);
                    var model = sc.GameModel.GetLandPlotModel(lpl._id);
                    if (model == null) continue;

                    try { lp.InitModel(model); } catch { }

                    GameObject newPlotGo = null;
                    try { newPlotGo = lpl.Replace(lp, targetPrefab); } catch { }
                    if (newPlotGo == null) continue;

                    sc.GameModel.InitializeLandPlotModel(lpl._id);
                    Il2CppLandPlot newLp = null;
                    try { newLp = newPlotGo.GetComponent<Il2CppLandPlot>(); } catch { }
                    if (newLp == null) newLp = pd.LinkedObject.GetComponentInChildren<Il2CppLandPlot>();

                    RealPlotFactory.ApplySavedUpgrades(newLp, pd.UniqueId);
                    CorralRegistrationHelper.SyncUpgradeVisibility(newLp);
                    CorralRegistrationHelper.RegisterPlotForInit(newLp, pd.UniqueId);
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneArtifactCleanup.RetryEmpty", ex); }
        }

        private static void EnsurePlotModel(Il2Cpp.SceneContext sc, string plotModelId)
        {
            if (sc == null || string.IsNullOrEmpty(plotModelId)) return;
            try
            {
                if (sc.GameModel.GetLandPlotModel(plotModelId) != null) return;
                sc.GameModel.InitializeLandPlotModel(plotModelId);
            }
            catch { }
        }
    }
}
