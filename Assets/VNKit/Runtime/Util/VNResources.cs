using UnityEngine;

namespace VNKit
{
    /// <summary>Resources-folder loading helpers with graceful warnings instead of crashes.</summary>
    public static class VNResources
    {
        /// <summary>Loads a sprite; also accepts textures imported as plain Texture2D.</summary>
        public static Sprite LoadSprite(string path)
        {
            var s = Resources.Load<Sprite>(path);
            if (s != null) return s;

            var t = Resources.Load<Texture2D>(path);
            if (t != null)
                return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);

            VNLog.Warn("Sprite not found at Resources path: " + path);
            return null;
        }

        public static AudioClip LoadClip(string path)
        {
            var c = Resources.Load<AudioClip>(path);
            if (c == null) VNLog.Warn("AudioClip not found at Resources path: " + path);
            return c;
        }
    }
}
