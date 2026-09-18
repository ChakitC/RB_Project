using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZLZ.AnimeShader
{
    // Backend-agnostic input reads for the demo scripts. Lets every demo work
    // whether the project's Active Input Handling is "Input Manager (Old)",
    // "Input System Package (New)", or "Both" — without this, calling the legacy
    // UnityEngine.Input class under a New-only project throws InvalidOperationException
    // every frame. The demos keep their inspector-friendly KeyCode fields; under the
    // new Input System we translate KeyCode -> Key on the fly (unmapped keys simply
    // do nothing rather than error).
    public static class ZLZ_DemoInput
    {
        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            Key k = ToKey(key);
            return k != Key.None && kb[k].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        // WASD / arrow-key movement as a (-1..1, -1..1) vector — x = horizontal,
        // y = vertical, matching the legacy "Horizontal" / "Vertical" axes.
        public static Vector2 GetMoveAxis()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f)
                    - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed)  ? 1f : 0f);
            float y = ((kb.wKey.isPressed || kb.upArrowKey.isPressed)    ? 1f : 0f)
                    - ((kb.sKey.isPressed || kb.downArrowKey.isPressed)  ? 1f : 0f);
            return new Vector2(x, y);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
            return Vector2.zero;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static Key ToKey(KeyCode key)
        {
            switch (key)
            {
                // Letters
                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;

                // Top-row digits
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

                // Numpad
                case KeyCode.Keypad0: return Key.Numpad0;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
                case KeyCode.Keypad4: return Key.Numpad4;
                case KeyCode.Keypad5: return Key.Numpad5;
                case KeyCode.Keypad6: return Key.Numpad6;
                case KeyCode.Keypad7: return Key.Numpad7;
                case KeyCode.Keypad8: return Key.Numpad8;
                case KeyCode.Keypad9: return Key.Numpad9;
                case KeyCode.KeypadPeriod:   return Key.NumpadPeriod;
                case KeyCode.KeypadDivide:   return Key.NumpadDivide;
                case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
                case KeyCode.KeypadMinus:    return Key.NumpadMinus;
                case KeyCode.KeypadPlus:     return Key.NumpadPlus;
                case KeyCode.KeypadEnter:    return Key.NumpadEnter;
                case KeyCode.KeypadEquals:   return Key.NumpadEquals;

                // Function keys
                case KeyCode.F1:  return Key.F1;
                case KeyCode.F2:  return Key.F2;
                case KeyCode.F3:  return Key.F3;
                case KeyCode.F4:  return Key.F4;
                case KeyCode.F5:  return Key.F5;
                case KeyCode.F6:  return Key.F6;
                case KeyCode.F7:  return Key.F7;
                case KeyCode.F8:  return Key.F8;
                case KeyCode.F9:  return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;

                // Arrows
                case KeyCode.UpArrow:    return Key.UpArrow;
                case KeyCode.DownArrow:  return Key.DownArrow;
                case KeyCode.LeftArrow:  return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;

                // Navigation / editing
                case KeyCode.Space:     return Key.Space;
                case KeyCode.Return:    return Key.Enter;
                case KeyCode.Escape:    return Key.Escape;
                case KeyCode.Tab:       return Key.Tab;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Delete:    return Key.Delete;
                case KeyCode.Insert:    return Key.Insert;
                case KeyCode.Home:      return Key.Home;
                case KeyCode.End:       return Key.End;
                case KeyCode.PageUp:    return Key.PageUp;
                case KeyCode.PageDown:  return Key.PageDown;
                case KeyCode.CapsLock:  return Key.CapsLock;

                // Modifiers
                case KeyCode.LeftShift:    return Key.LeftShift;
                case KeyCode.RightShift:   return Key.RightShift;
                case KeyCode.LeftControl:  return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt:      return Key.LeftAlt;
                case KeyCode.RightAlt:     return Key.RightAlt;

                // Punctuation
                case KeyCode.Minus:        return Key.Minus;
                case KeyCode.Equals:       return Key.Equals;
                case KeyCode.LeftBracket:  return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Backslash:    return Key.Backslash;
                case KeyCode.Semicolon:    return Key.Semicolon;
                case KeyCode.Quote:        return Key.Quote;
                case KeyCode.BackQuote:    return Key.Backquote;
                case KeyCode.Comma:        return Key.Comma;
                case KeyCode.Period:       return Key.Period;
                case KeyCode.Slash:        return Key.Slash;

                default: return Key.None;
            }
        }
#endif
    }
}
