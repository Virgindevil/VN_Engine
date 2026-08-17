using UnityEngine;
using UnityEngine.EventSystems;

namespace VNKit
{
    /// <summary>
    /// Input abstraction that works with the legacy Input Manager,
    /// the new Input System package, or both (compile-time switched).
    /// Hotkeys are rebindable: skip / auto / rollback keys come from VNSettings.
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

        /// <summary>Hold-to-skip. The key is configurable (Settings > Game); defaults to Ctrl.</summary>
        public static bool SkipHeld(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(key)) return true;
            // Convenience: both Ctrl keys work when either is bound.
            if (key == KeyCode.LeftControl && Input.GetKey(KeyCode.RightControl)) return true;
            if (key == KeyCode.RightControl && Input.GetKey(KeyCode.LeftControl)) return true;
            return false;
#elif ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
#else
            return false;
#endif
        }

        /// <summary>One-shot press of a configurable key (auto toggle, rollback).</summary>
        public static bool KeyPressed(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            if (key == KeyCode.A) return kb.aKey.wasPressedThisFrame;
            if (key == KeyCode.PageUp) return kb.pageUpKey.wasPressedThisFrame;
            if (key == KeyCode.PageDown) return kb.pageDownKey.wasPressedThisFrame;
            return false;
#else
            return false;
#endif
        }

        /// <summary>Mouse wheel delta (used for rollback / backlog scroll).</summary>
        public static float ScrollDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#elif ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue().y : 0f;
#else
            return 0f;
#endif
        }

        /// <summary>True on the frame any key goes down; outputs which one (for rebinding UI).</summary>
        public static bool AnyKeyDown(out KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.anyKeyDown)
            {
                foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (k == KeyCode.None) continue;
                    if (Input.GetKeyDown(k)) { key = k; return true; }
                }
            }
            key = KeyCode.None;
            return false;
#else
            key = KeyCode.None;
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

        static readonly System.Collections.Generic.List<RaycastResult> raycastBuffer =
            new System.Collections.Generic.List<RaycastResult>();

        /// <summary>
        /// Advance-click gating that looks only at the TOPMOST UI element under the pointer:
        /// when it belongs to allowRoot (the phone overlay), the click counts as "not over UI"
        /// and advances the dialogue. The phone body is raycast-solid and swallows clicks
        /// (VNClickCatcher), so buttons behind the phone can neither be clicked nor block
        /// advance clicks — while any other UI (buttons, panels) still blocks as before.
        /// </summary>
        public static bool PointerOverUI(Transform allowRoot)
        {
            if (EventSystem.current == null) return false;
#if ENABLE_LEGACY_INPUT_MANAGER
            Vector2 pos = Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 pos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            Vector2 pos = Vector2.zero;
#endif
            var data = new PointerEventData(EventSystem.current) { position = pos };
            raycastBuffer.Clear();
            EventSystem.current.RaycastAll(data, raycastBuffer);
            if (raycastBuffer.Count == 0) return false;
            var top = raycastBuffer[0].gameObject;
            if (allowRoot != null && top != null && top.transform.IsChildOf(allowRoot))
            {
                // Interactive elements inside the phone (chat rows, app icons,
                // photo bubbles, the back button) perform their own action —
                // the same click must NOT also advance the dialogue.
                if (top.GetComponentInParent<UnityEngine.UI.Button>() != null) return true;
                // Photo attachments ("PhonePhoto") open the full-screen viewer on click.
                return top.name == "PhonePhoto";
            }
            return true;
        }
    }
}
