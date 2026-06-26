using HarmonyLib;

namespace SlimeCorralSpawn.Patches
{
    /// <summary>Botón manual de recolección: cablea collector antes de Activate vanilla.</summary>
    [HarmonyPatch(typeof(Il2Cpp.PlortCollectorActivator), "Activate")]
    internal static class PlortCollectorActivatorPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(Il2Cpp.PlortCollectorActivator __instance)
        {
            var lp = __instance.GetComponentInParent<Il2Cpp.LandPlot>();
            if (lp == null || !GamePatches.IsOurLandPlot(lp)) return;
            if (!Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2Cpp.LandPlot.Upgrade.PLORT_COLLECTOR))
                return;
            Placement.CorralRegistrationHelper.WirePlotComponents(lp);
            Placement.CorralRegistrationHelper.ForceCollectNow(lp);
        }
    }
}
