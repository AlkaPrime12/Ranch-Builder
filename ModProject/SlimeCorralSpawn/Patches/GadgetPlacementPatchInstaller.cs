using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppGadgetDirector = Il2Cpp.GadgetDirector;
using Il2CppGadget = Il2CppMonomiPark.SlimeRancher.World.Gadget;
using Il2CppGadgetItem = Il2CppMonomiPark.SlimeRancher.Player.PlayerItems.GadgetItem;
using Il2CppGadgetOverlapHelpers = Il2CppMonomiPark.SlimeRancher.World.GadgetOverlapHelpers;
using Il2CppGadgetOverlapInfo = Il2CppMonomiPark.SlimeRancher.World.GadgetOverlapInfo;

namespace SlimeCorralSpawn.Patches
{
    /// <summary>Registra parches de colocación vanilla con AccessTools (más fiable que atributos en Il2Cpp).</summary>
    public static class GadgetPlacementPatchInstaller
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            int ok = 0, fail = 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadgetDirector), "CanPlaceGadget", GadgetPlacementPatches.CanPlaceGadget_Prefix, ref fail) ? 1 : 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadgetDirector), "GetPlacementError", GadgetPlacementPatches.GetPlacementError_Prefix, ref fail) ? 1 : 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadget), "IsOverlapping", new[] { typeof(float), typeof(LayerMask), typeof(bool) },
                GadgetPlacementPatches.OverlapFalse_Prefix, ref fail) ? 1 : 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadget), "IsOverlapping",
                new[] { typeof(Il2CppGadgetOverlapInfo).MakeByRefType(), typeof(float), typeof(LayerMask), typeof(bool) },
                GadgetPlacementPatches.OverlapFalse_Prefix, ref fail) ? 1 : 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadgetOverlapHelpers), "IsGadgetOverlapping",
                new[] { typeof(Transform), typeof(Collider), typeof(Il2Cpp.IdentifiableType), typeof(float), typeof(LayerMask), typeof(bool) },
                GadgetPlacementPatches.OverlapFalse_Prefix, ref fail) ? 1 : 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadgetOverlapHelpers), "IsGadgetOverlapping",
                new[] { typeof(Transform), typeof(Collider), typeof(Il2Cpp.IdentifiableType), typeof(Il2CppGadgetOverlapInfo).MakeByRefType(), typeof(float), typeof(LayerMask), typeof(bool) },
                GadgetPlacementPatches.OverlapFalse_Prefix, ref fail) ? 1 : 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadgetItem), "IsPlacementValid", new[] { typeof(Ray), typeof(RaycastHit) },
                GadgetPlacementPatches.IsPlacementValidRay_Prefix, ref fail) ? 1 : 0;
            // Postfixes de GadgetItem: solo validez. Altura una vez por frame en LateUpdate + Tick.
            ok += PatchPostfix(harmony, typeof(Il2CppGadgetItem), nameof(Il2CppGadgetItem.Update),
                GadgetPlacementPatches.GadgetItem_AfterPlacementUpdate, ref fail) ? 1 : 0;
            ok += PatchPostfix(harmony, typeof(Il2CppGadget), nameof(Il2CppGadget.LateUpdate),
                GadgetPlacementPatches.Gadget_LateUpdate_Postfix, ref fail) ? 1 : 0;
            ok += PatchPrefixBool(harmony, typeof(Il2CppGadgetItem), "SetGadgetPlacementValidity", new[] { typeof(bool) },
                GadgetPlacementPatches.ForceValidity_Prefix, ref fail) ? 1 : 0;

            MelonLogger.Msg($"[SCS] Gadget placement patches: {ok} ok, {fail} skipped/failed");
        }

        private static bool PatchPrefixBool(HarmonyLib.Harmony h, Type type, string method, Delegate prefix, ref int fail)
        {
            return PatchPrefixBool(h, type, method, null, prefix, ref fail);
        }

        private static bool PatchPrefixBool(HarmonyLib.Harmony h, Type type, string method, Type[] args, Delegate prefix, ref int fail)
        {
            try
            {
                var m = args == null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, args);
                if (m == null) { fail++; return false; }
                h.Patch(m, prefix: new HarmonyMethod(prefix.Method));
                return true;
            }
            catch { fail++; return false; }
        }

        private static bool PatchPostfix(HarmonyLib.Harmony h, Type type, string method, Delegate postfix, ref int fail)
        {
            try
            {
                var m = AccessTools.Method(type, method);
                if (m == null) { fail++; return false; }
                h.Patch(m, postfix: new HarmonyMethod(postfix.Method));
                return true;
            }
            catch { fail++; return false; }
        }
    }
}
