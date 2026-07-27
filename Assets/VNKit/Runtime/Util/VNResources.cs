using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace VNKit
{
    /*
    Загрузка ресурсов на основе адресных объектов с использованием кэша в оперативной памяти.
    Соглашение об адресации (установите эти адреса в качестве адресных "адресов" каждого ресурса):

    VN/Backgrounds/Campus
    VN/Characters/Ayame/Smile
    VN/Audio/BGM/ThemeDay
    VN/Audio/SFX/Chime
    VN/Audio/Voice/hana_01

    Контент находится в Assets/VNContent/... (Инструменты → VNKit → Создать папки контента).
    Отметьте ресурсы как адресные в Window → Asset Management → Addressables → Groups.
    */
    public static class VNResources
    {
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AsyncOperationHandle> handles = new Dictionary<string, AsyncOperationHandle>();

        static bool initialized;

        // ------------------------------------------------------------------
        // Initialization
        // ------------------------------------------------------------------

        /*
        Инициализирует систему Addressables (каталог, локальные/удалённые провайдеры).
        Можно безопасно вызывать несколько раз; последующие вызовы ничего не делают.
        Не генерирует исключение, если Addressables ещё не полностью настроена.
        */
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
                yield return handle;

                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
                    initialized = true;
                else if (!handle.IsValid())
                    initialized = true; 
                else
                    VNLog.Warn("Addressables.InitializeAsync finished with status: " + handle.Status);
            }

            // Даже если инициализация не удалась, пометьте значение true, чтобы загрузка продолжилась (при загрузке будет возвращено значение null)
            if (!initialized)
            {
                VNLog.Warn("Addressables not fully ready. Content loads may fail until assets are marked Addressable.");
                initialized = true;
            }

            if (onDone != null) onDone();
        }

        public static bool IsInitialized { get { return initialized; } }

        // ------------------------------------------------------------------
        // Cache find
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
        // Async loaders (пока что на корутинах)
        // ------------------------------------------------------------------

        /*
        Загружает спрайт по ключу Addressables.
        Также принимает адрес Texture2D и оборачивает его в спрайт.
        Вызывает onDone(null), если ресурс отсутствует.
        */
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

            // 1) Sprite
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

            // 2) Texture2D → Sprite
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

        // Загружает AudioClip по Addressables key
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
        // Preload helpers
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