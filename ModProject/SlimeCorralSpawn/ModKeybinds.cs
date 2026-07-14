using UnityEngine;

namespace SlimeCorralSpawn
{
    public enum ModAction
    {
        OpenMenu,
        PaintTool,
        RemoveTool,
        ConfirmEdit,
        DeleteSceneModel
    }

    /// <summary>Teclas reconfigurables del mod (PlayerPrefs).</summary>
    public static class ModKeybinds
    {
        private static readonly ModAction[] All = { ModAction.OpenMenu, ModAction.PaintTool, ModAction.RemoveTool, ModAction.ConfirmEdit, ModAction.DeleteSceneModel };

        private static readonly KeyCode[] Defaults =
        {
            KeyCode.F5,
            KeyCode.F7,
            KeyCode.F9,
            KeyCode.R,
            KeyCode.Delete
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
                case ModAction.ConfirmEdit: return Loc.T("key_confirm_edit");
                case ModAction.DeleteSceneModel: return Loc.T("key_delete_scene");
                default: return action.ToString();
            }
        }

        public static string KeyName(KeyCode key)
        {
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return "F" + (1 + (key - KeyCode.F1));
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((char)('0' + (key - KeyCode.Alpha0))).ToString();
            if (key >= KeyCode.A && key <= KeyCode.Z) return ((char)('A' + (key - KeyCode.A))).ToString();
            switch (key)
            {
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "KpEnter";
                case KeyCode.Space: return "Space";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Backspace: return "Bksp";
                case KeyCode.Escape: return "Esc";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.PageUp: return "PgUp";
                case KeyCode.PageDown: return "PgDn";
                case KeyCode.Home: return "Home";
                case KeyCode.End: return "End";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.LeftControl: return "Ctrl";
                case KeyCode.RightControl: return "R-Ctrl";
                case KeyCode.LeftAlt: return "Alt";
                case KeyCode.RightAlt: return "R-Alt";
                case KeyCode.LeftShift: return "Shift";
                case KeyCode.RightShift: return "R-Shift";
                case KeyCode.KeypadPlus: return "Kp+";
                case KeyCode.KeypadMinus: return "Kp-";
                case KeyCode.KeypadPeriod: return "Kp.";
                case KeyCode.KeypadDivide: return "Kp/";
                case KeyCode.KeypadMultiply: return "Kp*";
                case KeyCode.None: return "—";
                default: return key.ToString();
            }
        }

        public static bool IsDown(ModAction action) => InputHelper.GetKeyDown(Get(action));

        private static string PrefKey(ModAction action) => "scs_key_" + action;
    }
}
