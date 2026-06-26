using UnityEngine;

namespace SlimeCorralSpawn
{
    public enum ModAction
    {
        OpenMenu,
        PaintTool,
        RemoveTool
    }

    /// <summary>Teclas reconfigurables del mod (PlayerPrefs).</summary>
    public static class ModKeybinds
    {
        private static readonly ModAction[] All = { ModAction.OpenMenu, ModAction.PaintTool, ModAction.RemoveTool };

        private static readonly KeyCode[] Defaults =
        {
            KeyCode.F5,
            KeyCode.F7,
            KeyCode.F9
        };

        public static KeyCode Get(ModAction action)
        {
            try
            {
                int i = (int)action;
                if (i < 0 || i >= Defaults.Length) return KeyCode.None;
                int v = PlayerPrefs.GetInt(PrefKey(action), (int)Defaults[i]);
                return (KeyCode)v;
            }
            catch { return Defaults[(int)action]; }
        }

        public static void Set(ModAction action, KeyCode key)
        {
            try
            {
                PlayerPrefs.SetInt(PrefKey(action), (int)key);
                PlayerPrefs.Save();
            }
            catch { }
        }

        public static void ResetDefaults()
        {
            for (int i = 0; i < All.Length; i++)
                Set(All[i], Defaults[i]);
        }

        public static string Label(ModAction action)
        {
            switch (action)
            {
                case ModAction.OpenMenu: return Loc.T("key_open_menu");
                case ModAction.PaintTool: return Loc.T("key_paint");
                case ModAction.RemoveTool: return Loc.T("key_remove");
                default: return action.ToString();
            }
        }

        public static string KeyName(KeyCode key)
        {
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return "F" + (1 + (key - KeyCode.F1));
            return key.ToString();
        }

        public static bool IsDown(ModAction action) => InputHelper.GetKeyDown(Get(action));

        private static string PrefKey(ModAction action) => "scs_key_" + action;
    }
}
