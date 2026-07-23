using UnityEngine;
using UnityEngine.EventSystems;

namespace VNKit
{
    /// <summary>
    /// Input abstraction that works with the legacy Input Manager,
    /// the new Input System package, or both (compile-time switched).
    /// </summary>
    public static class VNInput
    {
        public static bool AdvancePressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
#else
            return false;
#endif
        }

        public static bool SkipHeld()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#elif ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
#else
            return false;
#endif
        }

        public static bool HidePressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#elif ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool CancelPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool PointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
