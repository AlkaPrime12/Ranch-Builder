using HarmonyLib;
using UnityEngine;
using Il2CppGadgetDirector = Il2Cpp.GadgetDirector;
using Il2CppGadget = Il2CppMonomiPark.SlimeRancher.World.Gadget;
using Il2CppGadgetItem = Il2CppMonomiPark.SlimeRancher.Player.PlayerItems.GadgetItem;
using SlimeCorralSpawn.Gadgets;

namespace SlimeCorralSpawn.Patches
{
    public static class GadgetPlacementPatches
    {
        internal static bool Active => ModSettings.CustomGadgetPlacement;

        public static bool CanPlaceGadget_Prefix(ref bool __result)
        {
            if (!Active) return true;
            __result = true;
            return false;
        }

        public static bool GetPlacementError_Prefix(ref Il2CppGadgetDirector.PlacementError __result)
        {
            if (!Active) return true;
            __result = default;
            return false;
        }

        public static bool OverlapFalse_Prefix(ref bool __result)
        {
            if (!Active) return true;
            __result = false;
            return false;
        }

        public static bool IsPlacementValidRay_Prefix(ref bool __result)
        {
            if (!Active) return true;
            __result = true;
            return false;
        }

        public static void ForceValidity_Prefix(ref bool isValid)
        {
            if (Active) isValid = true;
        }

        public static void GadgetItem_AfterPlacementUpdate(Il2CppGadgetItem __instance)
        {
            if (!Active || __instance == null) return;
            try
            {
                if (!__instance.IsPlaceholderVisible)
                {
                    GadgetPlacementHelper.OnPlacementEnded();
                    return;
                }
                GadgetPlacementHelper.SetActiveItem(__instance);
                GadgetPlacementHelper.ForcePlacementValid(__instance);
            }
            catch { }
        }

        public static void Gadget_LateUpdate_Postfix(Il2CppGadget __instance)
        {
            if (!Active || __instance == null) return;
            try
            {
                var item = GadgetPlacementHelper.GetActiveGadgetItem();
                if (item == null) return;
                var ph = item.GetPlaceholderGadget();
                if (ph == null || ph != __instance) return;
                GadgetPlacementHelper.ApplyHeightEndOfFrame(item);
            }
            catch { }
        }
    }
}
