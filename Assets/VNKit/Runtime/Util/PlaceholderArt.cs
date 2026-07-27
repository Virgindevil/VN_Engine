using UnityEngine;

namespace VNKit
{
    /*
    Генерирует простые цветные спрайты-заменители, чтобы можно было создать прототип истории
    до появления каких-либо реальных графических элементов (включите параметр "Use Placeholder Graphics" в движке).
    Цвета определяются по названию ресурса, поэтому каждое название выглядит по-разному.
    */
    public static class PlaceholderArt
    {
        public static Sprite Character(string name)
        {
            const int w = 256, h = 640;
            const float radius = 48f;
            Color c1, c2;
            HashColors(name, 0.62f, 0.92f, out c1, out c2);
            var dark = new Color(c2.r * 0.4f, c2.g * 0.4f, c2.b * 0.4f, 1f);

            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            var clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!InsideRoundedRect(x, y, w, h, radius)) { px[y * w + x] = clear; continue; }
                    float v = y / (float)h;
                    px[y * w + x] = Color.Lerp(c2, c1, v);
                }
            }

            // Простые глаза, чтобы цилиндр воспринимался как персонаж.
            DrawCircle(px, w, h, Mathf.RoundToInt(w * 0.36f), Mathf.RoundToInt(h * 0.78f), 13, dark);
            DrawCircle(px, w, h, Mathf.RoundToInt(w * 0.64f), Mathf.RoundToInt(h * 0.78f), 13, dark);

            t.SetPixels(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        public static Sprite Background(string name)
        {
            const int w = 480, h = 270;
            Color c1, c2;
            HashColors(name, 0.45f, 0.75f, out c1, out c2);

            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float k = (x / (float)w * 0.5f) + (y / (float)h * 0.5f);
                    px[y * w + x] = Color.Lerp(c1, c2, k);
                }
            }
            t.SetPixels(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool InsideRoundedRect(int x, int y, int w, int h, float r)
        {
            float cx = x < r ? r : (x > w - 1 - r ? w - 1 - r : x);
            float cy = y < r ? r : (y > h - 1 - r ? h - 1 - r : y);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        static void DrawCircle(Color[] px, int w, int h, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= w) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r * r) px[y * w + x] = c;
                }
            }
        }

        static void HashColors(string name, float sat, float val, out Color a, out Color b)
        {
            unchecked
            {
                int hash = 17;
                string s = name ?? "?";
                for (int i = 0; i < s.Length; i++) hash = hash * 31 + s[i];
                hash &= 0x7fffffff;
                float hue1 = (hash % 360) / 360f;
                float hue2 = (hue1 + 0.13f) % 1f;
                a = Color.HSVToRGB(hue1, sat, val);
                b = Color.HSVToRGB(hue2, sat * 0.9f, val * 0.7f);
            }
        }
    }
}
