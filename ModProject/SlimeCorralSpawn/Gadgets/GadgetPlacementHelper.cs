using UnityEngine;
using Il2CppGadget = Il2CppMonomiPark.SlimeRancher.World.Gadget;
using Il2CppGadgetItem = Il2CppMonomiPark.SlimeRancher.Player.PlayerItems.GadgetItem;

namespace SlimeCorralSpawn.Gadgets
{
    /// <summary>Estado ligero del modo colocación vanilla de gadgets (sin escaneos por frame).</summary>
    public static class GadgetPlacementHelper
    {
        private static Il2CppGadgetItem _cachedItem;
        private static float _cacheAt = -999f;

        public static float ManualHeightOffset => Placement.PlacementHeightInput.Offset;

        public static bool CustomActive => ModSettings.CustomGadgetPlacement;

        public static bool IsPlacingGadget()
        {
            if (!CustomActive) return false;
            try
            {
                var item = GetGadgetItem();
                if (item != null && item.IsPlaceholderVisible) return true;
            }
            catch { }
            return false;
        }

        public static void HandleHeightInput()
        {
            if (!IsPlacingGadget()) return;
            Placement.PlacementHeightInput.Tick();
        }

        public static void ApplyHeightToPlaceholder(Il2CppGadgetItem item)
        {
            if (!CustomActive || item == null) return;
            float off = ManualHeightOffset;
            if (Mathf.Abs(off) < 0.0001f) return;
            try
            {
                var g = item.GetPlaceholderGadget();
                if (g == null) return;
                var tr = g.transform;
                var p = tr.position;
                p.y += off;
                tr.position = p;
            }
            catch { }
        }

        public static void OnPlacementEnded() => Placement.PlacementHeightInput.Reset();

        public static Vector3 ApplyOffset(Vector3 pos)
        {
            if (!CustomActive) return pos;
            float off = ManualHeightOffset;
            if (Mathf.Abs(off) < 0.0001f) return pos;
            pos.y += off;
            return pos;
        }

        private static Il2CppGadgetItem GetGadgetItem()
        {
            if (Time.realtimeSinceStartup - _cacheAt < 0.5f && _cachedItem != null)
                return _cachedItem;

            _cacheAt = Time.realtimeSinceStartup;
            _cachedItem = null;
            try
            {
                var items = Object.FindObjectsOfType<Il2CppGadgetItem>(true);
                if (items == null || items.Length == 0) return null;
                foreach (var it in items)
                {
                    if (it != null && it.IsPlaceholderVisible) { _cachedItem = it; return it; }
                }
                _cachedItem = items[0];
            }
            catch { }
            return _cachedItem;
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
