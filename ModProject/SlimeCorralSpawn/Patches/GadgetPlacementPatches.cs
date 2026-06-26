using HarmonyLib;
using UnityEngine;
using Il2CppGadgetDirector = Il2Cpp.GadgetDirector;
using Il2CppGadget = Il2CppMonomiPark.SlimeRancher.World.Gadget;
using Il2CppGadgetItem = Il2CppMonomiPark.SlimeRancher.Player.PlayerItems.GadgetItem;
using Il2CppGadgetOverlapHelpers = Il2CppMonomiPark.SlimeRancher.World.GadgetOverlapHelpers;
using SlimeCorralSpawn.Gadgets;

namespace SlimeCorralSpawn.Patches
{
    [HarmonyPatch]
    public static class GadgetPlacementPatches
    {
        private static bool Active => ModSettings.CustomGadgetPlacement;

        [HarmonyPatch(typeof(Il2CppGadgetDirector), nameof(Il2CppGadgetDirector.CanPlaceGadget))]
        [HarmonyPrefix]
        public static bool CanPlaceGadget_Prefix(ref bool __result)
        {
            if (!Active) return true;
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Il2CppGadgetDirector), "GetPlacementError")]
        [HarmonyPrefix]
        public static bool GetPlacementError_Prefix(ref Il2CppGadgetDirector.PlacementError __result)
        {
            if (!Active) return true;
            __result = default;
            return false;
        }

        [HarmonyPatch(typeof(Il2CppGadget), nameof(Il2CppGadget.IsOverlapping), typeof(float), typeof(LayerMask), typeof(bool))]
        [HarmonyPrefix]
        public static bool Gadget_IsOverlapping_Prefix(ref bool __result)
        {
            if (!Active) return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Il2CppGadgetOverlapHelpers), "IsGadgetOverlapping",
            new[] { typeof(Transform), typeof(Collider), typeof(Il2Cpp.IdentifiableType), typeof(float), typeof(LayerMask), typeof(bool) })]
        [HarmonyPrefix]
        public static bool OverlapHelpers_Prefix1(ref bool __result)
        {
            if (!Active) return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Il2CppGadgetItem), "IsPlacementValid")]
        [HarmonyPrefix]
        public static bool IsPlacementValid_Prefix(ref bool __result)
        {
            if (!Active) return true;
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Il2CppGadgetItem), nameof(Il2CppGadgetItem.Update))]
        [HarmonyPostfix]
        public static void GadgetItem_Update_Postfix(Il2CppGadgetItem __instance)
        {
            if (!Active) return;
            try
            {
                if (!__instance.IsPlaceholderVisible)
                {
                    GadgetPlacementHelper.OnPlacementEnded();
                    return;
                }
                GadgetPlacementHelper.HandleHeightInput();
                GadgetPlacementHelper.ApplyHeightToPlaceholder(__instance);
            }
            catch { }
        }

        [HarmonyPatch(typeof(Il2CppGadgetItem), "PlaceGadgetEvent")]
        [HarmonyPostfix]
        public static void PlaceGadgetEvent_Postfix(Il2CppGadgetItem __instance)
        {
            if (!Active) return;
            try
            {
                GadgetPlacementHelper.ApplyHeightToPlaceholder(__instance);
            }
            catch { }
            finally { GadgetPlacementHelper.OnPlacementEnded(); }
        }
    }
}
