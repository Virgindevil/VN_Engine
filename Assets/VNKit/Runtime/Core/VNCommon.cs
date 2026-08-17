using System;
using UnityEngine;

namespace VNKit
{
    public enum PlayerState { Idle, Running, WaitingInput, WaitingChoice, WaitingTimer, WaitingAsset, WaitingMinigame, WaitingTextInput, WaitingChatEnter, WaitingChat, WaitingChatHub, Ended }

    [Serializable]
    public class VNSettings
    {
        // Sound
        public float masterVolume = 1f;
        public float bgmVolume = 0.8f;
        public float sfxVolume = 1f;
        public float voiceVolume = 1f;

        // Game / text
        [Tooltip("Characters per second")] public float textSpeed = 45f;
        [Tooltip("Seconds to wait before advancing in auto mode")] public float autoDelay = 1.2f;
        [Tooltip("When true, Skip mode stops at unread text; when false, skips everything.")]
        public bool skipUnreadOnly = true;
        [Tooltip("Preferred UI / content language code (en, ru, ja, ...).")]
        public string language = "en";

        // Video
        public int resolutionWidth = 1920;
        public int resolutionHeight = 1080;
        public bool fullscreen = true;

        // Hotkeys (rebindable in Settings > Game)
        [Tooltip("Hold to skip text")] public KeyCode skipKey = KeyCode.LeftControl;
        [Tooltip("Toggle auto mode")] public KeyCode autoKey = KeyCode.A;
        [Tooltip("Rollback one line")] public KeyCode rollbackKey = KeyCode.PageUp;
        [Tooltip("Hide the dialogue UI (with the phone menu enabled, RMB opens the phone instead)")]
        public KeyCode hideKey = KeyCode.H;
    }

    [Serializable]
    public class VNBacklogEntry
    {
        public string speaker;
        public string text;
    }

    public static class VNLog
    {
        public static void Log(string msg) { Debug.Log("[VNKit] " + msg); }
        public static void Warn(string msg) { Debug.LogWarning("[VNKit] " + msg); }
        public static void Error(string msg) { Debug.LogError("[VNKit] " + msg); }
    }

    /// <summary>
    /// Tiny MonoBehaviour used purely as a coroutine host for non-MonoBehaviour services.
    /// Created with a RectTransform and stretched when the parent is a RectTransform,
    /// so children that use fractional anchors (DialogueUI base layer, CharacterActor,
    /// BackgroundManager) get a real size instead of collapsing to 0x0 at screen center.
    /// </summary>
    public class VNRunner : MonoBehaviour
    {
        public static VNRunner Create(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            if (parent is RectTransform)
            {
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            return go.AddComponent<VNRunner>();
        }
    }
}
