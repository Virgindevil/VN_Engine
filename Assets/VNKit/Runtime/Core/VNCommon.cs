using System;
using UnityEngine;

namespace VNKit
{
    public enum PlayerState { Idle, Running, WaitingInput, WaitingChoice, WaitingTimer, Ended }

    [Serializable]
    public class VNSettings
    {
        public float masterVolume = 1f;
        public float bgmVolume = 0.8f;
        public float sfxVolume = 1f;
        public float voiceVolume = 1f;
        [Tooltip("Characters per second")] public float textSpeed = 45f;
        [Tooltip("Seconds to wait before advancing in auto mode")] public float autoDelay = 1.2f;
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

    /// <summary>Tiny MonoBehaviour used purely as a coroutine host for non-Mono services.</summary>
    public class VNRunner : MonoBehaviour
    {
        public static VNRunner Create(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<VNRunner>();
        }
    }
}
