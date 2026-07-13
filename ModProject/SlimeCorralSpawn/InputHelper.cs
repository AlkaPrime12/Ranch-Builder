using UnityEngine;
using UnityEngine.InputSystem;

namespace SlimeCorralSpawn
{
    public static class InputHelper
    {
        /// <summary>True when the New Input System has an active keyboard device.</summary>
        public static bool KeyboardAvailable => Keyboard.current != null;

        /// <summary>Mapea KeyCode (lo que usamos) al enum Key del New Input System. Key.None = no soportado.</summary>
        private static Key ToKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z) return Key.A + (key - KeyCode.A);
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return Key.F1 + (key - KeyCode.F1);
            switch (key)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.KeypadPlus: return Key.NumpadPlus;
                case KeyCode.KeypadMinus: return Key.NumpadMinus;
                default: return Key.None;
            }
        }

        public static bool GetKeyDown(KeyCode key)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            Key k = ToKey(key);
            if (k == Key.None) return false;
            try { return kb[k].wasPressedThisFrame; } catch { return false; }
        }

        public static bool GetKey(KeyCode key)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            Key k = ToKey(key);
            if (k == Key.None) return false;
            try { return kb[k].isPressed; } catch { return false; }
        }

        public static bool GetMouseButtonDown(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            switch (button)
            {
                case 0: return mouse.leftButton.wasPressedThisFrame;
                case 1: return mouse.rightButton.wasPressedThisFrame;
                case 2: return mouse.middleButton.wasPressedThisFrame;
                default: return false;
            }
        }

        public static bool GetMouseButton(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            switch (button)
            {
                case 0: return mouse.leftButton.isPressed;
                case 1: return mouse.rightButton.isPressed;
                case 2: return mouse.middleButton.isPressed;
                default: return false;
            }
        }

        public static bool GetMouseButtonUp(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            switch (button)
            {
                case 0: return mouse.leftButton.wasReleasedThisFrame;
                case 1: return mouse.rightButton.wasReleasedThisFrame;
                case 2: return mouse.middleButton.wasReleasedThisFrame;
                default: return false;
            }
        }

        public static Vector2 GetMousePosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            return mouse.position.ReadValue();
        }

        /// <summary>Delta del mouse este frame (funciona con el cursor bloqueado — para free-look).</summary>
        public static Vector2 GetMouseDelta()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            try { return mouse.delta.ReadValue(); } catch { return Vector2.zero; }
        }

        public static float GetAxis(string axisName)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return 0f;
            if (axisName == "Mouse ScrollWheel") return mouse.scroll.ReadValue().y;
            if (axisName == "Mouse X") return mouse.delta.ReadValue().x;
            if (axisName == "Mouse Y") return mouse.delta.ReadValue().y;
            return 0f;
        }
    }
}
