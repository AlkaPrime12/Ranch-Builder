using HarmonyLib;

namespace SlimeCorralSpawn.Patches
{
    /// <summary>
    /// Botón manual del collector en plots custom: cableado completo y activación vanilla
    /// (animación del botón, sonido, _forceCollectUntil, FX de aspirado).
    /// </summary>
    [HarmonyPatch(typeof(Il2Cpp.PlortCollectorActivator), "Activate")]
    internal static class PlortCollectorActivatorPatch
    {
        // Solo CABLEA y deja correr el Activate() VANILLA (animación del botón, PressButtonCue,
        // _forceCollectUntil, StartCollection, FX). Antes reemplazábamos el vanilla (return false) y el
        // botón "no hacía nada" porque la réplica manual fallaba.
        [HarmonyPrefix]
        internal static bool Prefix(Il2Cpp.PlortCollectorActivator __instance)
        {
            var lp = __instance.GetComponentInParent<Il2Cpp.LandPlot>();
            if (lp == null || !GamePatches.IsOurLandPlot(lp)) return true;
            if (!Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2Cpp.LandPlot.Upgrade.PLORT_COLLECTOR))
                return true;

            try
            {
                // Asegurar que el collector y su silo estén 100% cableados ANTES de que corra el vanilla.
                Placement.PlortCollectorHelper.WireForPlot(lp);
                Placement.CorralRegistrationHelper.WirePlotComponents(lp);
                Placement.CorralRegistrationHelper.EnsureCollectorSiloReady(lp);

                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                var pc = Placement.CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
                if (pc != null)
                    Placement.PlortCollectorHelper.WireSingleActivator(__instance, pc);  // act.Collector = pc
            }
            catch { }

            return true;   // ← corre el Activate() vanilla completo
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.PlortCollectorActivator), "Awake")]
    internal static class PlortCollectorActivatorAwakePatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Il2Cpp.PlortCollectorActivator __instance)
        {
            var lp = __instance.GetComponentInParent<Il2Cpp.LandPlot>();
            if (lp == null || !GamePatches.IsOurLandPlot(lp)) return;

            var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
            var pc = Placement.CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
            if (pc != null)
                Placement.PlortCollectorHelper.WireSingleActivator(__instance, pc);
        }
    }
}
