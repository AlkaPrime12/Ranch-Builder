using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;

namespace SlimeCorralSpawn.Patches
{
    public static class GamePatches
    {
        private static HarmonyLib.Harmony _harmony;

        public static void ApplyPatches()
        {
            if (_harmony != null) return;
            _harmony = new HarmonyLib.Harmony("SlimeCorralSpawn.Patches");

            PatchPostfix("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot", "BuyPlot",
                         typeof(LandPlotBuyPlotPatch), "Postfix");

            PatchPostfix("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot", "Upgrade",
                         typeof(LandPlotUpgradePatch), "Postfix");

            TryPatchFeederSpeedChange();

            _harmony.PatchAll();
            TryPatchFastForwardCorrals();
        }

        private static void PatchPostfix(string typeName, string method, Type patchType, string patchMethod)
        {
            var t = FindType(typeName);
            if (t == null) { MelonLogger.Warning($"[SCS] type not found: {typeName}"); return; }
            MethodInfo m = null;
            try { m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
            catch (AmbiguousMatchException)
            {
                foreach (var mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (mi.Name == method) { m = mi; break; }
            }
            if (m == null) { MelonLogger.Warning($"[SCS] method not found: {typeName}.{method}"); return; }
            _harmony.Patch(m, postfix: new HarmonyMethod(patchType.GetMethod(patchMethod)));
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { try { var t = asm.GetType(name); if (t != null) return t; } catch { } }
            return null;
        }

        private static void TryPatchFeederSpeedChange()
        {
            var uiType = FindType("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot");
            if (uiType == null) return;

            foreach (var m in uiType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name.IndexOf("FeederSpeed", StringComparison.OrdinalIgnoreCase) < 0 &&
                    m.Name.IndexOf("FeedSpeed", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (m.GetParameters().Length > 2) continue;
                try
                {
                    _harmony.Patch(m, postfix: new HarmonyMethod(typeof(FeederSpeedChangePatch).GetMethod(nameof(FeederSpeedChangePatch.Postfix))));
                }
                catch { }
            }
        }

        private static void TryPatchFastForwardCorrals()
        {
            try
            {
                var ffType = typeof(Il2Cpp.RanchCellFastForwarder);
                var ffcMethod = AccessTools.Method(ffType, "FastForwardCorrals");
                if (ffcMethod != null)
                {
                    _harmony.Patch(ffcMethod,
                        prefix: new HarmonyMethod(typeof(RanchCellFFPatch).GetMethod(nameof(RanchCellFFPatch.Prefix),
                            BindingFlags.Static | BindingFlags.Public)));
                    return;
                }

                foreach (var m in ffType.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("FastForward") && m.Name.Contains("Corral"))
                    {
                        _harmony.Patch(m,
                            prefix: new HarmonyMethod(typeof(RanchCellFFPatch).GetMethod(nameof(RanchCellFFPatch.Prefix),
                                BindingFlags.Static | BindingFlags.Public)));
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FF] TryPatchFastForwardCorrals: {ex.Message}");
            }
        }

        internal static bool IsOurLandPlot(Il2CppLandPlot lp)
        {
            if (lp == null || lp.transform == null) return false;
            var t = lp.transform;
            while (t != null)
            {
                var loc = t.GetComponent<Il2CppLandPlotLocation>();
                if (loc != null && Placement.RealPlotFactory.IsOurLocation(loc)) return true;
                string n = t.name;
                if (n != null && (n == "RealPlotGhostPreview" || n.StartsWith("SCS_PlotRoot") || n.StartsWith("SCP_"))) return true;
                t = t.parent;
            }
            return false;
        }
    }

    /// <summary>Fast-forward sin re-registrar plots (evita bucle).</summary>
    public static class RanchCellFFPatch
    {
        public static void Prefix(Il2Cpp.RanchCellFastForwarder __instance, double __0, double __1, double __2)
        {
            try
            {
                var plots = UnityEngine.Object.FindObjectsOfType<Il2CppLandPlot>(true);
                if (plots == null) return;

                foreach (var lp in plots)
                {
                    if (lp == null || !GamePatches.IsOurLandPlot(lp)) continue;
                    if (!Placement.CorralRegistrationHelper.IsRegistered(lp)) continue;
                    Placement.CorralRegistrationHelper.RunFastForwardOps(lp, __instance);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FF] Error: {ex.Message}");
            }
        }
    }

    public static class LandPlotUpgradePatch
    {
        public static void Postfix(Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot __instance,
                                   Il2CppLandPlot.Upgrade upgrade, bool __result)
        {
            if (!__result) return;
            var lp = __instance.Activator;
            if (lp == null || !GamePatches.IsOurLandPlot(lp)) return;
            GameObject locGo = (lp.transform != null && lp.transform.parent != null) ? lp.transform.parent.gameObject : null;
            if (locGo != null)
                Placement.RealPlotManager.AddSavedUpgrade(locGo.name, upgrade.ToString());

            // Compra nueva: Apply + OnInitialPurchase (defaults), luego registrar y cablear.
            Placement.CorralRegistrationHelper.SyncUpgradeVisibility(lp);
            Placement.UpgradeActivationHelper.EnsureUpgradesActive(lp, freshPurchase: true, purchased: upgrade);
            Deferred.Run(() =>
            {
                string key = null;
                try { key = lp.transform?.parent?.name; } catch { }
                Placement.CorralRegistrationHelper.RegisterPlotForInit(lp, key);
                if (upgrade == Il2CppLandPlot.Upgrade.PLORT_COLLECTOR)
                    Placement.CorralRegistrationHelper.ForceCollectNow(lp);
            }, 5);

            Placement.FeederSpeedHelper.CaptureAndSave(lp);
        }
    }

    public static class FeederSpeedChangePatch
    {
        public static void Postfix(Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot __instance)
        {
            var lp = __instance?.Activator;
            if (lp == null || !GamePatches.IsOurLandPlot(lp)) return;
            Placement.FeederSpeedHelper.CaptureAndSave(lp);
            Placement.FeederSpeedHelper.ApplySpeedToFeeder(lp);
        }
    }

    public static class LandPlotBuyPlotPatch
    {
        public static void Postfix(Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot __instance,
                                   GameObject plotPrefab)
        {
            var lp = __instance.Activator;
            bool ours = GamePatches.IsOurLandPlot(lp);
            GameObject locGo = (lp != null && lp.transform != null && lp.transform.parent != null) ? lp.transform.parent.gameObject : null;

            if (ours && locGo != null && plotPrefab != null)
            {
                var prefabLp = plotPrefab.GetComponent<Il2CppLandPlot>();
                if (prefabLp != null)
                    Placement.RealPlotManager.UpdateSavedType(locGo.name,
                        Placement.RealPlotManager.FromRealId(prefabLp.GetPlotId()));
            }

            Deferred.Run(() =>
            {
                var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                if (canvases != null)
                    foreach (var c in canvases)
                        if (c != null && c.gameObject != null && c.gameObject.name == "DimBackground(Clone)")
                            UnityEngine.Object.Destroy(c.gameObject);
                __instance.Close();
            }, 2);
        }
    }
}
