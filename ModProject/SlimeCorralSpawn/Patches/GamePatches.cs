using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using MelonLoader;
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

            // Finalizers: tragar las excepciones que tiran NUESTROS LandPlots custom en Start/OnDestroy
            // (no están 100% integrados, pero funcionan; sin esto el juego loguea/rompe).
            PatchFinalizer(typeof(Il2CppLandPlot), "Start", typeof(LandPlotStartFinalizer), "Finalizer");
            PatchFinalizer(typeof(Il2CppLandPlot), "OnDestroy", typeof(LandPlotDestroyFinalizer), "Finalizer");

            // BuyPlot: guardar el tipo construido + limpiar el overlay (NO tocar el modelo del juego:
            // SetModel/SetGameObject/NotifyParticipants integraban el plot en el RanchModel y CRASHEABAN
            // el load del juego). El contenido lo persistimos nosotros vía ContentPersistence.
            PatchPostfix("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot", "BuyPlot",
                         typeof(LandPlotBuyPlotPatch), "Postfix");

            // Postfix de Upgrade: guardar la mejora comprada (para re-aplicarla al recargar).
            PatchPostfix("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot", "Upgrade",
                         typeof(LandPlotUpgradePatch), "Postfix");

            // Finalizer de Upgrade: tragar la NRE que tira el juego al mejorar NUESTROS plots custom
            // (no son LandPlots reales completos). Sólo se traga para nuestros plots; los vanilla intactos.
            PatchFinalizerByName("Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot", "Upgrade",
                         typeof(LandPlotUpgradeFinalizer), "Finalizer");

            MelonLogger.Msg("[SlimeCorralSpawn] Harmony patches applied (plots reales).");
        }

        private static void PatchFinalizer(Type t, string method, Type patchType, string patchMethod)
        {
            try
            {
                var m = AccessTools.Method(t, method);
                if (m == null) { MelonLogger.Warning($"[SCS] {t.Name}.{method} no encontrado"); return; }
                _harmony.Patch(m, finalizer: new HarmonyMethod(patchType.GetMethod(patchMethod)));
                MelonLogger.Msg($"[SCS] Finalizer aplicado: {t.Name}.{method}");
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] Finalizer {t.Name}.{method}: {ex.Message}"); }
        }

        private static void PatchPostfix(string typeName, string method, Type patchType, string patchMethod)
        {
            try
            {
                var t = FindType(typeName);
                if (t == null) { MelonLogger.Warning($"[SCS] tipo no encontrado: {typeName}"); return; }
                MethodInfo m = null;
                try { m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
                catch (AmbiguousMatchException)
                {
                    foreach (var mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        if (mi.Name == method) { m = mi; break; }
                }
                if (m == null) { MelonLogger.Warning($"[SCS] método no encontrado: {typeName}.{method}"); return; }
                _harmony.Patch(m, postfix: new HarmonyMethod(patchType.GetMethod(patchMethod)));
                MelonLogger.Msg($"[SCS] Postfix aplicado: {typeName}.{method}");
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] Postfix {typeName}.{method}: {ex.Message}"); }
        }

        private static void PatchFinalizerByName(string typeName, string method, Type patchType, string patchMethod)
        {
            try
            {
                var t = FindType(typeName);
                if (t == null) { MelonLogger.Warning($"[SCS] tipo no encontrado: {typeName}"); return; }
                MethodInfo m = null;
                try { m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
                catch (AmbiguousMatchException)
                {
                    foreach (var mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        if (mi.Name == method) { m = mi; break; }
                }
                if (m == null) { MelonLogger.Warning($"[SCS] método no encontrado: {typeName}.{method}"); return; }
                _harmony.Patch(m, finalizer: new HarmonyMethod(patchType.GetMethod(patchMethod)));
                MelonLogger.Msg($"[SCS] Finalizer aplicado: {typeName}.{method}");
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] Finalizer {typeName}.{method}: {ex.Message}"); }
        }

        private static void PatchPrefix(Type t, string method, Type patchType, string patchMethod)
        {
            try
            {
                var m = AccessTools.Method(t, method);
                if (m == null) { MelonLogger.Warning($"[SCS] {t.Name}.{method} no encontrado"); return; }
                _harmony.Patch(m, prefix: new HarmonyMethod(patchType.GetMethod(patchMethod)));
                MelonLogger.Msg($"[SCS] Prefix aplicado: {t.Name}.{method}");
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] Prefix {t.Name}.{method}: {ex.Message}"); }
        }

        private static void PatchPrefixPostfix(string typeName, string method,
                                               Type prefixType, string prefixMethod,
                                               Type postfixType, string postfixMethod)
        {
            try
            {
                var t = FindType(typeName);
                if (t == null) { MelonLogger.Warning($"[SCS] tipo no encontrado: {typeName}"); return; }
                MethodInfo m = null;
                try { m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
                catch (AmbiguousMatchException)
                {
                    foreach (var mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        if (mi.Name == method) { m = mi; break; }
                }
                if (m == null) { MelonLogger.Warning($"[SCS] método no encontrado: {typeName}.{method}"); return; }
                _harmony.Patch(
                    m,
                    prefix: new HarmonyMethod(prefixType.GetMethod(prefixMethod)),
                    postfix: new HarmonyMethod(postfixType.GetMethod(postfixMethod))
                );
                MelonLogger.Msg($"[SCS] Prefix/Postfix aplicado: {typeName}.{method}");
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] Prefix/Postfix {typeName}.{method}: {ex.Message}"); }
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { try { var t = asm.GetType(name); if (t != null) return t; } catch { } }
            return null;
        }

        internal static bool IsOurLandPlot(Il2CppLandPlot lp)
        {
            try
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
            catch { return false; }
        }
    }

    public static class LandPlotStartFinalizer
    {
        // Solo suprimir la excepción si el LandPlot es NUESTRO (custom). Los plots vanilla no deben
        // tragarse sus errores (encubrirían bugs reales). El NRE ocurre porque el plot se construye
        // antes de que RegisterLandPlot termine (diferido 2 frames).
        public static Exception Finalizer(Il2CppLandPlot __instance, Exception __exception)
        {
            if (__exception == null) return null;
            try { if (GamePatches.IsOurLandPlot(__instance)) return null; } catch { }
            return __exception;
        }
    }

    public static class LandPlotDestroyFinalizer
    {
        public static Exception Finalizer(Il2CppLandPlot __instance, Exception __exception)
        {
            if (__exception == null) return null;
            try { if (GamePatches.IsOurLandPlot(__instance)) return null; } catch { }
            return __exception;
        }
    }

    public static class LandPlotUpgradePatch
    {
        public static void Postfix(Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot __instance,
                                   Il2CppLandPlot.Upgrade upgrade, bool __result)
        {
            try
            {
                if (!__result) return;   // mejora no comprada
                var lp = __instance.Activator;
                if (lp == null || !GamePatches.IsOurLandPlot(lp)) return;
                GameObject locGo = (lp.transform != null && lp.transform.parent != null) ? lp.transform.parent.gameObject : null;
                if (locGo != null)
                    Placement.RealPlotManager.AddSavedUpgrade(locGo.name, upgrade.ToString());
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] Upgrade postfix: {ex.Message}"); }
        }
    }

    public static class LandPlotUpgradeFinalizer
    {
        // Traga la NRE del Upgrade SÓLO si el plot es nuestro (vanilla intacto).
        public static Exception Finalizer(Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot __instance, Exception __exception)
        {
            if (__exception == null) return null;
            try { var lp = __instance.Activator; if (lp != null && GamePatches.IsOurLandPlot(lp)) return null; } catch { }
            return __exception;
        }
    }

    public static class LandPlotBuyPlotPatch
    {
        public static void Postfix(Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot __instance,
                                   GameObject plotPrefab)
        {
            try
            {
                var lp = __instance.Activator;
                bool ours = GamePatches.IsOurLandPlot(lp);
                GameObject locGo = (lp != null && lp.transform != null && lp.transform.parent != null) ? lp.transform.parent.gameObject : null;

                // Guardar el tipo construido (para que persista como corral/silo/etc. al recargar).
                if (ours && locGo != null && plotPrefab != null)
                {
                    try
                    {
                        var prefabLp = plotPrefab.GetComponent<Il2CppLandPlot>();
                        if (prefabLp != null)
                            Placement.RealPlotManager.UpdateSavedType(locGo.name,
                                Placement.RealPlotManager.FromRealId(prefabLp.GetPlotId()));
                    }
                    catch { }
                }

                // Limpiar el overlay oscuro (DimBackground(Clone)) que se acumula + cerrar el menú.
                Deferred.Run(() =>
                {
                    try
                    {
                        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                        if (canvases != null)
                            foreach (var c in canvases)
                                if (c != null && c.gameObject != null && c.gameObject.name == "DimBackground(Clone)")
                                    UnityEngine.Object.Destroy(c.gameObject);
                        try { __instance.Close(); } catch { }
                    }
                    catch { }
                }, 2);
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] BuyPlot postfix: {ex.Message}"); }
        }
    }
}
