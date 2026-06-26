using HarmonyLib;

namespace SlimeCorralSpawn.Patches
{
    /// <summary>Re-cablea SlimeFeeder en Awake para plots custom (region/silo/timeDir).</summary>
    [HarmonyPatch(typeof(Il2Cpp.SlimeFeeder), "Awake")]
    internal static class SlimeFeederRegionPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Il2Cpp.SlimeFeeder __instance)
        {
            var lp = __instance.GetComponentInParent<Il2Cpp.LandPlot>();
            if (lp == null || !GamePatches.IsOurLandPlot(lp)) return;
            if (!Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2Cpp.LandPlot.Upgrade.FEEDER))
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
            Placement.CorralRegistrationHelper.EnsureFeederRunning(lp);
            Placement.FeederSpeedHelper.ApplySpeedToFeeder(lp);
        }
    }
}
