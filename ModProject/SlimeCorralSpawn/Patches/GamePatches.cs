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

            // Cuando el JUEGO guarda (autosave / Save & Exit), guardamos NUESTROS datos en ese mismo momento
            // (rancho cargado → captura fresca + escritura forzada al archivo del slot, sin cooldown).
            PatchPostfix("Il2CppMonomiPark.SlimeRancher.AutoSaveDirector", "SaveGameImpl",
                         typeof(ModSaveOnGameSavePatch), "Postfix");

            TryPatchFeederSpeedChange();

            _harmony.PatchAll();
            GadgetPlacementPatchInstaller.Apply(_harmony);
            TryPatchFastForwardCorrals();
            TryPatchLandPlotStartSuppress();
        }

        /// <summary>Nuestros LandPlots custom (clonados del prefab del juego) lanzan NullReferenceException en
        /// el <c>Start()</c> vanilla (algún ref interno que el juego espera no existe en el clon), pero IGUAL
        /// funcionan por nuestro cableado. Sin esto, al activarlos se spameaban CIENTOS de errores (lag + log).
        /// Finalizer = se traga la excepción de Start (NO afecta plots vanilla: esos no lanzan en Start).</summary>
        private static void TryPatchLandPlotStartSuppress()
        {
            try
            {
                var m = typeof(Il2Cpp.LandPlot).GetMethod("Start",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null)
                    _harmony.Patch(m, finalizer: new HarmonyMethod(
                        typeof(LandPlotStartSuppressPatch).GetMethod(nameof(LandPlotStartSuppressPatch.Finalizer))));
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] LandPlot.Start suppress: {ex.Message}"); }
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

    /// <summary>Se traga la NRE del Start() vanilla de nuestros LandPlots custom (que igual funcionan) para no
    /// spamear el log ni lagear. Devolver null = excepción "manejada".</summary>
    public static class LandPlotStartSuppressPatch
    {
        public static Exception Finalizer(Exception __exception) => null;
    }

    /// <summary>Engancha NUESTRO guardado al del juego: cuando SR2 guarda (autosave / Save & Exit), persistimos
    /// los datos del mod en ese instante (con el rancho cargado → captura fresca de silo/jardín) y forzamos la
    /// escritura al archivo del slot (sin el cooldown de 5s). Así el cambio recién hecho SÍ queda guardado y se
    /// refleja al recargar el slot. Todo en try/catch para no afectar el guardado del juego.</summary>
    public static class ModSaveOnGameSavePatch
    {
        public static void Postfix()
        {
            try { Plots.PlotData.CaptureAndForceSave(); }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] save hook: {ex.Message}"); }
        }
    }

    /// <summary>Fast-forward usando PlotData.GetAll() en vez de FindObjectsOfType (EVITA ALLOC LAG).
    /// El crecimiento del jardín lo maneja el JUEGO de forma nativa (no hay driver propio).</summary>
    public static class RanchCellFFPatch
    {
        public static void Prefix(Il2Cpp.RanchCellFastForwarder __instance, double __0, double __1, double __2)
        {
            try
            {
                foreach (var pd in Plots.PlotData.GetAll())
                {
                    if (pd?.LinkedObject == null) continue;
                    Il2CppLandPlot lp = null;
                    try { lp = pd.LinkedObject.GetComponentInChildren<Il2CppLandPlot>(true); } catch { }
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
                if (__instance != null && __instance.gameObject != null)
                    __instance.Close();
            }, 2);
        }
    }
}
