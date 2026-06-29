using HarmonyLib;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;

namespace SlimeCorralSpawn.Patches
{
    [HarmonyPatch(typeof(Il2CppLandPlot), nameof(Il2CppLandPlot.Start))]
    internal static class LandPlotStartPatch
    {
        private static Il2CppLandPlotLocation FindLPL(Il2CppLandPlot lp)
        {
            var t = lp.transform;
            while (t != null)
            {
                var loc = t.GetComponent<Il2CppLandPlotLocation>();
                if (loc != null) return loc;
                t = t.parent;
            }
            return null;
        }

        [HarmonyPrefix]
        internal static bool Prefix(Il2CppLandPlot __instance)
        {
            if (!GamePatches.IsOurLandPlot(__instance)) return true;

            var sc = Il2Cpp.SceneContext.Instance;
            if (sc == null) return true;

            var lpl = FindLPL(__instance);
            if (lpl == null) return true;

            var model = sc.GameModel.GetLandPlotModel(lpl._id);
            if (model != null)
            {
                __instance.InitModel(model);
            }
            else
            {
                sc.GameModel.InitializeLandPlotModel(lpl._id);
                model = sc.GameModel.GetLandPlotModel(lpl._id);
                if (model != null)
                    __instance.InitModel(model);
            }
            return true;
        }

        [HarmonyPostfix]
        internal static void Postfix(Il2CppLandPlot __instance)
        {
            if (!GamePatches.IsOurLandPlot(__instance)) return;

            Deferred.Run(() =>
            {
                if (__instance == null || !GamePatches.IsOurLandPlot(__instance)) return;
                if (Placement.CorralRegistrationHelper.IsRegistered(__instance)) return;
                string key = null;
                try { key = __instance.transform?.parent?.name; } catch { }
                Placement.CorralRegistrationHelper.RegisterPlotForInit(__instance, key);
            }, 15);
        }
    }
}
