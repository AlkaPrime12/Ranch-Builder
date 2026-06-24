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

            // BuyPlot: save the constructed type + clear overlay
            PatchPostfix("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot", "BuyPlot",
                         typeof(LandPlotBuyPlotPatch), "Postfix");

            // Upgrade: save the purchased upgrade (re-apply on reload)
            PatchPostfix("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot", "Upgrade",
                         typeof(LandPlotUpgradePatch), "Postfix");

            MelonLogger.Msg("[SlimeCorralSpawn] Harmony patches applied.");
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

        private static void PatchFinalizer(string typeName, string method, Type patchType, string patchMethod)
        {
            var t = FindType(typeName);
            if (t == null) { MelonLogger.Warning($"[SCS] type not found: {typeName}"); return; }
            MethodInfo m = null;
            try { m = AccessTools.Method(t, method); } catch { }
            if (m == null)
            {
                try { m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
                catch { }
            }
            if (m == null) { MelonLogger.Warning($"[SCS] method not found: {typeName}.{method}"); return; }
            _harmony.Patch(m, finalizer: new HarmonyMethod(patchType.GetMethod(patchMethod, BindingFlags.Static | BindingFlags.Public)));
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { try { var t = asm.GetType(name); if (t != null) return t; } catch { } }
            return null;
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
