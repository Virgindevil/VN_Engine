using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace VNKit
{
    /// <summary>
    /// Addressables-based asset loading with an in-memory cache.
    ///
    /// Address convention (set these as the Addressable "Address" of each asset):
    ///   VN/Backgrounds/Campus
    ///   VN/Characters/Ayame/Smile
    ///   VN/Audio/BGM/ThemeDay
    ///   VN/Audio/SFX/Chime
    ///   VN/Audio/Voice/hana_01
    ///
    /// Content lives in Assets/VNContent/... (Tools → VNKit → Create Content Folders).
    /// Mark assets Addressable in Window → Asset Management → Addressables → Groups.
    /// </summary>
    public static class VNResources
    {
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AsyncOperationHandle> handles = new Dictionary<string, AsyncOperationHandle>();

        static bool initialized;

        // ------------------------------------------------------------------
        // Initialization (call once at boot)
        // ------------------------------------------------------------------

        /// <summary>
        /// Initializes the Addressables system (catalog, local/remote providers).
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// Does not throw if Addressables is not fully configured yet.
        /// </summary>
        public static IEnumerator Initialize(Action onDone = null)
        {
            if (initialized)
            {
                if (onDone != null) onDone();
                yield break;
            }

            AsyncOperationHandle handle = default;
            bool started = false;

            // Start outside try so we can yield safely.
            try
            {
                handle = Addressables.InitializeAsync();
                started = true;
            }
            catch (Exception e)
            {
                VNLog.Error("Addressables.InitializeAsync failed to start: " + e.Message);
            }

            if (started)
            {
                // yield must be outside try/catch
                yield return handle;

                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
                    initialized = true;
                else if (!handle.IsValid())
                    initialized = true; // already init / auto-init
                else
                    VNLog.Warn("Addressables.InitializeAsync finished with status: " + handle.Status);
            }

            // Even if init failed, mark true so boot continues (loads will just return null).
            if (!initialized)
            {
                VNLog.Warn("Addressables not fully ready. Content loads may fail until assets are marked Addressable.");
                initialized = true;
            }

            if (onDone != null) onDone();
        }

        public static bool IsInitialized { get { return initialized; } }

        // ------------------------------------------------------------------
        // Cache lookups (instant, no I/O)
        // ------------------------------------------------------------------

        public static Sprite GetCachedSprite(string address)
        {
            Sprite s;
            return spriteCache.TryGetValue(address, out s) ? s : null;
        }

        public static AudioClip GetCachedClip(string address)
        {
            AudioClip c;
            return clipCache.TryGetValue(address, out c) ? c : null;
        }

        // ------------------------------------------------------------------
        // Async loaders (yield in coroutines)
        // ------------------------------------------------------------------

        /// <summary>
        /// Loads a Sprite by Addressables key.
        /// Also accepts a Texture2D address and wraps it into a Sprite.
        /// Calls onDone(null) when the asset is missing.
        /// </summary>
        public static IEnumerator LoadSprite(string address, Action<Sprite> onDone)
        {
            if (string.IsNullOrEmpty(address))
            {
                if (onDone != null) onDone(null);
                yield break;
            }

            Sprite cached;
            if (spriteCache.TryGetValue(address, out cached))
            {
                if (onDone != null) onDone(cached);
                yield break;
            }

            // 1) Try as Sprite
            var handle = Addressables.LoadAssetAsync<Sprite>(address);
            yield return handle;

            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                Track(address, handle);
                spriteCache[address] = handle.Result;
                if (onDone != null) onDone(handle.Result);
                yield break;
            }

            if (handle.IsValid()) Addressables.Release(handle);

            // 2) Try as Texture2D → create Sprite
            var texHandle = Addressables.LoadAssetAsync<Texture2D>(address);
            yield return texHandle;

            if (texHandle.IsValid() && texHandle.Status == AsyncOperationStatus.Succeeded && texHandle.Result != null)
            {
                var t = texHandle.Result;
                var sprite = Sprite.Create(
                    t,
                    new Rect(0, 0, t.width, t.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = address;
                Track(address, texHandle);
                spriteCache[address] = sprite;
                if (onDone != null) onDone(sprite);
                yield break;
            }

            if (texHandle.IsValid()) Addressables.Release(texHandle);
            VNLog.Warn("Sprite not found at Addressables key: " + address);
            if (onDone != null) onDone(null);
        }

        /// <summary>Loads an AudioClip by Addressables key.</summary>
        public static IEnumerator LoadClip(string address, Action<AudioClip> onDone)
        {
            if (string.IsNullOrEmpty(address))
            {
                if (onDone != null) onDone(null);
                yield break;
            }

            AudioClip cached;
            if (clipCache.TryGetValue(address, out cached))
            {
                if (onDone != null) onDone(cached);
                yield break;
            }

            var handle = Addressables.LoadAssetAsync<AudioClip>(address);
            yield return handle;

            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                Track(address, handle);
                clipCache[address] = handle.Result;
                if (onDone != null) onDone(handle.Result);
                yield break;
            }

            if (handle.IsValid()) Addressables.Release(handle);
            VNLog.Warn("AudioClip not found at Addressables key: " + address);
            if (onDone != null) onDone(null);
        }

        // ------------------------------------------------------------------
        // Preload helpers (for boot / chapter starts)
        // ------------------------------------------------------------------

        public static IEnumerator PreloadSprites(IList<string> addresses, Action<float, string> onProgress = null)
        {
            if (addresses == null || addresses.Count == 0)
            {
                if (onProgress != null) onProgress(1f, null);
                yield break;
            }

            for (int i = 0; i < addresses.Count; i++)
            {
                string addr = addresses[i];
                if (onProgress != null) onProgress(i / (float)addresses.Count, addr);
                yield return LoadSprite(addr, null);
            }
            if (onProgress != null) onProgress(1f, null);
        }

        public static IEnumerator PreloadClips(IList<string> addresses, Action<float, string> onProgress = null)
        {
            if (addresses == null || addresses.Count == 0)
            {
                if (onProgress != null) onProgress(1f, null);
                yield break;
            }

            for (int i = 0; i < addresses.Count; i++)
            {
                string addr = addresses[i];
                if (onProgress != null) onProgress(i / (float)addresses.Count, addr);
                yield return LoadClip(addr, null);
            }
            if (onProgress != null) onProgress(1f, null);
        }

        // ------------------------------------------------------------------
        // Lifetime
        // ------------------------------------------------------------------

        static void Track(string address, AsyncOperationHandle handle)
        {
            AsyncOperationHandle old;
            if (handles.TryGetValue(address, out old) && old.IsValid())
                Addressables.Release(old);
            handles[address] = handle;
        }

        public static void ReleaseAll()
        {
            foreach (var kv in handles)
            {
                if (kv.Value.IsValid()) Addressables.Release(kv.Value);
            }
            handles.Clear();
            spriteCache.Clear();
            clipCache.Clear();
        }
    }
}