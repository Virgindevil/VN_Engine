using System;
using System.Collections;
using UnityEngine;
#if VNKIT_SPINE
using Spine.Unity;
#endif

namespace VNKit
{
    /// <summary>Config entry: which character is a Spine skeleton (instead of sprites).</summary>
    [Serializable]
    public class VNSpineCharEntry
    {
        public string character;
        [Tooltip("Addressables address of the SkeletonDataAsset, e.g. VN/Spine/Ayame")]
        public string skeletonAddress;
        [Tooltip("Animation played when no appearance is specified.")]
        public string defaultAnimation = "idle";
        public bool loop = true;
        [Tooltip("Uniform scale applied to the skeleton inside the character rect.")]
        public float scale = 1f;
    }

    /// <summary>Config entry: which event CG is an animated Spine skeleton.</summary>
    [Serializable]
    public class VNSpineCgEntry
    {
        public string cg;
        [Tooltip("Addressables address of the SkeletonDataAsset, e.g. VN/Spine/CG/Sunset")]
        public string skeletonAddress;
        public string animation = "idle";
        public bool loop = true;
    }

#if VNKIT_SPINE
    /// <summary>
    /// Spine bridge (spine-unity runtime detected — VNKIT_SPINE is added automatically
    /// by Editor/VNKitDefines.cs). For Spine characters the "appearance" in scripts maps
    /// to an animation name: "Ayame.Smile:" plays the "Smile" animation, so animated
    /// characters change emotion smoothly with the text. Same for animated CGs via @cg.
    /// </summary>
    public class VNSpineActor : MonoBehaviour
    {
        SkeletonGraphic graphic;
        public SkeletonGraphic Graphic { get { return graphic; } }

        public static bool Available { get { return true; } }

        public static IEnumerator LoadSkeleton(string address, Action<UnityEngine.Object> onDone)
        {
            var op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<SkeletonDataAsset>(address);
            yield return op;
            if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded
                && op.Result != null)
            {
                if (onDone != null) onDone(op.Result);
            }
            else
            {
                VNLog.Warn("Spine skeleton not found at address '" + address + "'.");
                if (onDone != null) onDone(null);
            }
        }

        public static VNSpineActor Create(RectTransform parent, string name, UnityEngine.Object data,
            string animation, bool loop, float scale)
        {
            var sda = data as SkeletonDataAsset;
            if (sda == null) return null;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = new Vector3(scale, scale, 1f);

            var actor = go.AddComponent<VNSpineActor>();
            var g = go.AddComponent<SkeletonGraphic>();
            g.skeletonDataAsset = sda;
            g.Initialize(true);
            actor.graphic = g;
            actor.Play(animation, loop);
            return actor;
        }

        public void Play(string animation, bool loop)
        {
            if (graphic == null || graphic.Skeleton == null || string.IsNullOrEmpty(animation)) return;
            if (graphic.Skeleton.Data.FindAnimation(animation) == null)
            {
                VNLog.Warn("Spine animation '" + animation + "' not found on '" + name + "'.");
                return;
            }
            graphic.AnimationState.SetAnimation(0, animation, loop);
        }
    }
#else
    /// <summary>
    /// Stub compiled when the spine-unity runtime is not installed. Everything no-ops,
    /// so projects without Spine still compile and run (sprite characters only).
    /// Install spine-unity and VNKIT_SPINE is defined automatically.
    /// </summary>
    public class VNSpineActor : MonoBehaviour
    {
        public static bool Available { get { return false; } }

        public static IEnumerator LoadSkeleton(string address, Action<UnityEngine.Object> onDone)
        {
            VNLog.Warn("Spine character/CG requested ('" + address + "') but the spine-unity runtime is not installed.");
            if (onDone != null) onDone(null);
            yield break;
        }

        public static VNSpineActor Create(RectTransform parent, string name, UnityEngine.Object data,
            string animation, bool loop, float scale)
        {
            return null;
        }

        public void Play(string animation, bool loop) { }
    }
#endif
}
