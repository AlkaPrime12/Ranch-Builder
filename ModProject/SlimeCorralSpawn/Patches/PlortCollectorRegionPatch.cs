using HarmonyLib;

namespace SlimeCorralSpawn.Patches
{
    /// <summary>Re-cablea PlortCollector en Awake para plots custom (paridad con SlimeFeeder).</summary>
    [HarmonyPatch(typeof(Il2Cpp.PlortCollector), "Awake")]
    internal static class PlortCollectorRegionPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Il2Cpp.PlortCollector __instance)
        {
            var lp = __instance.GetComponentInParent<Il2Cpp.LandPlot>();
            if (lp == null || !GamePatches.IsOurLandPlot(lp)) return;

            if (!Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2Cpp.LandPlot.Upgrade.PLORT_COLLECTOR))
            {
                Placement.CorralRegistrationHelper.SyncUpgradeVisibility(lp);
                return;
            }

            if (!Placement.CorralRegistrationHelper.IsRegistered(lp))
            {
                string key = null;
                try { key = lp.transform?.parent?.name; } catch { }
                Placement.CorralRegistrationHelper.RegisterPlotForInit(lp, key);
                return;
            }

            Placement.CorralRegistrationHelper.WirePlotComponents(lp);
            Placement.CorralRegistrationHelper.EnsureCollectorRunning(lp, force: true);
        }
    }
}
