using System;
using UnityEngine;

namespace VNKit
{
    public enum PlayerState { Idle, Running, WaitingInput, WaitingChoice, WaitingTimer, WaitingAsset, Ended }

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
    }

    [Serializable]
    public class VNBacklogEntry
    {
        public string speaker;
        public string text;
    }

    public static class VNLog
    {
        public static void Warn(string msg) { Debug.LogWarning("[VNKit] " + msg); }
        public static void Error(string msg) { Debug.LogError("[VNKit] " + msg); }
    }

    // Tiny MonoBehaviour используется исключительно в качестве хоста для сопрограмм для сервисов, не использующих MonoBehaviour.
    public class VNRunner : MonoBehaviour
    {
        /*
        Создает хост сопрограммы. Если родительский элемент — RectTransform (иерархия пользовательского интерфейса),
        хост растягивается, чтобы заполнить его, так что дочерние элементы, использующие дробные привязки
        (базовый слой DialogueUI, CharacterActor, BackgroundManager), получают реальный размер
        вместо того, чтобы сжиматься до точки 0×0 в центре.
        */
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