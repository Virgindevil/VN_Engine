using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace VNKit
{
    /// <summary>
    /// Async content loading on top of Addressables, with caching and handle tracking.
    /// Replaces the old synchronous Resources loader. Addressing convention (configurable
    /// via VisualNovelEngine.resourcesRoot):
    ///   VN/Backgrounds/Campus          — background sprite or texture
    ///   VN/Characters/Ayame/Smile      — character sprite or texture
    ///   VN/CG/RooftopSunset            — event CG sprite or texture
    ///   VN/Audio/BGM/ThemeDay          — AudioClip
    ///   VN/Audio/SFX/Chime             — AudioClip
    ///   VN/Audio/Voice/hana_01         — AudioClip
    /// Mark assets Addressable with matching addresses (groups per category recommended
    /// for WebGL / mobile size control and remote catalogs).
    /// </summary>
    
    public static class VNResources
    {
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        static readonly List<AsyncOperationHandle> handles = new List<AsyncOperationHandle>();
        static bool initialized;

        /// <summary>Initialize the Addressables runtime (catalogs, providers). Required for remote groups / WebGL.</summary>
        public static IEnumerator Initialize()
        {
            if (initialized) yield break;

            // autoReleaseHandle: false — since Addressables 1.21 the default (true) releases
            // the handle as soon as initialization completes, so reading op.Status after the
            // yield throws "Attempting to use an invalid operation handle".
            var op = Addressables.InitializeAsync(false);
            yield return op;

            initialized = op.Status == AsyncOperationStatus.Succeeded;
            if (!initialized) VNLog.Error("Addressables initialization failed.");
            Addressables.Release(op);
        }

        public static IEnumerator LoadSprite(string address, System.Action<Sprite> onDone)
        {
            Sprite cached;
            if (spriteCache.TryGetValue(address, out cached))
            {
                if (onDone != null) onDone(cached);
                yield break;
            }

            Sprite s = null;
            var op = Addressables.LoadAssetAsync<Sprite>(address);
            yield return op;
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
            {
                s = op.Result;
                handles.Add(op);
            }
            else
            {
                // The asset may be imported as a plain Texture2D.
                var texOp = Addressables.LoadAssetAsync<Texture2D>(address);
                yield return texOp;
                if (texOp.Status == AsyncOperationStatus.Succeeded && texOp.Result != null)
                {
                    var tex = texOp.Result;
                    s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    handles.Add(texOp);
                }
            }

            if (s == null) VNLog.Warn("Sprite not found at address '" + address + "'.");
            spriteCache[address] = s;
            if (onDone != null) onDone(s);
        }

        public static IEnumerator LoadClip(string address, System.Action<AudioClip> onDone)
        {
            AudioClip cached;
            if (clipCache.TryGetValue(address, out cached))
            {
                if (onDone != null) onDone(cached);
                yield break;
            }

            AudioClip clip = null;
            var op = Addressables.LoadAssetAsync<AudioClip>(address);
            yield return op;
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
            {
                clip = op.Result;
                handles.Add(op);
            }

            if (clip == null) VNLog.Warn("Audio clip not found at address '" + address + "'.");
            clipCache[address] = clip;
            if (onDone != null) onDone(clip);
        }

        /// <summary>Preload a list of sprite addresses with progress reporting (0..1, current address).</summary>
        public static IEnumerator PreloadSprites(IList<string> addresses, System.Action<float, string> onProgress)
        {
            if (addresses == null || addresses.Count == 0) yield break;
            for (int i = 0; i < addresses.Count; i++)
            {
                if (onProgress != null) onProgress(i / (float)addresses.Count, addresses[i]);
                yield return LoadSprite(addresses[i], null);
            }
            if (onProgress != null) onProgress(1f, null);
        }

        /// <summary>Preload a list of audio addresses with progress reporting.</summary>
        public static IEnumerator PreloadClips(IList<string> addresses, System.Action<float, string> onProgress)
        {
            if (addresses == null || addresses.Count == 0) yield break;
            for (int i = 0; i < addresses.Count; i++)
            {
                if (onProgress != null) onProgress(i / (float)addresses.Count, addresses[i]);
                yield return LoadClip(addresses[i], null);
            }
            if (onProgress != null) onProgress(1f, null);
        }

        /// <summary>Release every tracked Addressables handle and clear the caches (e.g. on scene unload).</summary>
        public static void ReleaseAll()
        {
            for (int i = 0; i < handles.Count; i++)
                if (handles[i].IsValid()) Addressables.Release(handles[i]);
            handles.Clear();
            spriteCache.Clear();
            clipCache.Clear();
        }
    }
}