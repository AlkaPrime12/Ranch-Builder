using HarmonyLib;
using UnityEngine;
using Il2CppGadget = Il2CppMonomiPark.SlimeRancher.World.Gadget;
using Il2CppGadgetItem = Il2CppMonomiPark.SlimeRancher.Player.PlayerItems.GadgetItem;

namespace SlimeCorralSpawn.Gadgets
{
    public static class GadgetPlacementHelper
    {
        private static Il2CppGadgetItem _activeItem;
        private static float _appliedSurplus;
        private static int _heightFrame = -1;
        private static bool _loggedNeedConfig;

        public static float ManualHeightOffset => Placement.PlacementHeightInput.Offset;
        public static bool CustomActive => ModSettings.CustomGadgetPlacement;

        public static void SetActiveItem(Il2CppGadgetItem item) => _activeItem = item;

        public static void Tick()
        {
            if (!CustomActive)
            {
                if (!_loggedNeedConfig && _activeItem != null)
                {
                    try
                    {
                        if (_activeItem.IsPlaceholderVisible)
                        {
                            _loggedNeedConfig = true;
                            ModEntry.Instance?.LoggerInstance.Msg("[SCS] Activa 'Colocación custom de gadgets' en F5 → Config.");
                        }
                    }
                    catch { }
                }
                return;
            }

            var item = GetActiveGadgetItem();
            if (item == null || !IsPlaceholderVisible(item)) return;
            ForcePlacementValid(item);
        }

        public static bool IsPlacingGadget()
        {
            if (!CustomActive) return false;
            var item = GetActiveGadgetItem();
            return item != null && IsPlaceholderVisible(item);
        }

        public static void ForcePlacementValid(Il2CppGadgetItem item)
        {
            if (!CustomActive || item == null) return;
            try
            {
                var tr = Traverse.Create(item);
                tr.Field("_isPlacementValid").SetValue(true);
                tr.Field("_isPlacementBlocked").SetValue(false);
            }
            catch
            {
                try { AccessTools.Method(item.GetType(), "SetGadgetPlacementValidity")?.Invoke(item, new object[] { true }); }
                catch { }
            }
        }

        /// <summary>Una sola vez por frame: input + offset incremental (no acumular Y varias veces).</summary>
        public static void ApplyHeightEndOfFrame(Il2CppGadgetItem item)
        {
            if (!CustomActive || item == null) return;
            if (Time.frameCount == _heightFrame) return;
            _heightFrame = Time.frameCount;

            Placement.PlacementHeightInput.TickGadget();

            float off = ManualHeightOffset;
            try
            {
                var g = item.GetPlaceholderGadget();
                if (g == null) return;

                var p = g.transform.position;
                if (Mathf.Abs(off) < 0.0001f)
                {
                    if (_appliedSurplus > 0.0001f)
                    {
                        p.y -= _appliedSurplus;
                        g.transform.position = p;
                        _appliedSurplus = 0f;
                    }
                    return;
                }

                float increment = off - _appliedSurplus;
                if (Mathf.Abs(increment) > 0.00001f)
                {
                    p.y += increment;
                    g.transform.position = p;
                }
                _appliedSurplus = off;
            }
            catch { }
        }

        public static void OnPlacementEnded()
        {
            Placement.PlacementHeightInput.Reset();
            _appliedSurplus = 0f;
            _activeItem = null;
            _heightFrame = -1;
        }

        public static Il2CppGadgetItem GetActiveGadgetItem()
        {
            if (_activeItem != null && IsPlaceholderVisible(_activeItem)) return _activeItem;
            _activeItem = null;
            return null;
        }

        private static bool IsPlaceholderVisible(Il2CppGadgetItem item)
        {
            try { return item != null && item.IsPlaceholderVisible; }
            catch { return false; }
        }

        public static void DrawHud()
        {
            if (!IsPlacingGadget()) return;
            float cx = Screen.width / 2f;
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 200f, Screen.height - 72f, 400f, 22f),
                string.Format(Loc.T("gadget_height_hud"), ManualHeightOffset.ToString("+0.0;-0.0;0.0")),
                GUI.skin.label);
        }
    }
}
