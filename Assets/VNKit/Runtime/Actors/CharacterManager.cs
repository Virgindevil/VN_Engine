using System.Collections.Generic;
using UnityEngine;

namespace VNKit
{
    /// <summary>Spawns, moves, hides and restores the on-stage characters.</summary>
    public class CharacterManager
    {
        readonly VisualNovelEngine engine;
        readonly VNRunner runner;
        readonly Dictionary<string, CharacterActor> actors = new Dictionary<string, CharacterActor>();

        public CharacterManager(Transform stageRoot, VisualNovelEngine engine)
        {
            this.engine = engine;
            runner = VNRunner.Create("VNKit.Characters", stageRoot);
        }

        /// <summary>
        /// Applies a @char command with a pre-loaded sprite.
        /// ScriptPlayer loads the sprite via Addressables before calling this.
        /// </summary>
        public void ApplyCommand(VNCommand cmd, Sprite sprite)
        {
            if (string.IsNullOrEmpty(cmd.Name))
            {
                VNLog.Warn("@char is missing a character id (line " + cmd.LineNumber + ").");
                return;
            }

            string id = cmd.Name;
            string name = id, appearance = null;
            int dot = id.IndexOf('.');
            if (dot >= 0)
            {
                name = id.Substring(0, dot);
                appearance = id.Substring(dot + 1);
            }

            float time = cmd.GetFloat("time", 0.35f);
            if (!cmd.GetBool("visible", true)) { Hide(name, time); return; }

            CharacterActor existing;
            float fallback = actors.TryGetValue(name, out existing) ? existing.PosX : 0.5f;
            float pos = ParsePos(cmd.Get("pos", cmd.Pos), fallback);
            Show(name, appearance, pos, time, sprite);
        }

        /// <summary>
        /// Shows a character. Pass a pre-loaded sprite (from Addressables).
        /// If sprite is null and the actor already has the same appearance, keeps the current one.
        /// </summary>
        public void Show(string name, string appearance, float pos, float time, Sprite sprite)
        {
            CharacterActor a;
            bool isNew = !actors.TryGetValue(name, out a);
            if (isNew)
            {
                a = new CharacterActor(runner.transform, name, runner);
                actors[name] = a;
            }

            string app = appearance ?? (a.Appearance ?? "Default");
            if (sprite != null)
                a.SetSprite(sprite, app);
            else if (app != a.Appearance || !a.HasSprite)
                a.SetSprite(null, app);

            a.SetPosition(pos, isNew ? 0f : time);
            a.Show(time);
        }

        public void Hide(string name, float time)
        {
            CharacterActor a;
            if (!actors.TryGetValue(name, out a)) return;
            a.Hide(time, delegate
            {
                actors.Remove(name);
                a.DestroyActor();
            });
        }

        public void HideAll(float time)
        {
            foreach (var kv in new List<KeyValuePair<string, CharacterActor>>(actors))
            {
                string n = kv.Key;
                CharacterActor a = kv.Value;
                a.Hide(time, delegate
                {
                    actors.Remove(n);
                    a.DestroyActor();
                });
            }
        }

        public void ClearAll()
        {
            foreach (var kv in actors) kv.Value.DestroyActor();
            actors.Clear();
        }

        /// <summary>Instant appearance swap. Pass a pre-loaded sprite.</summary>
        public void SetAppearance(string name, string appearance, Sprite sprite)
        {
            CharacterActor a;
            if (!actors.TryGetValue(name, out a)) return;
            if (a.Appearance == appearance && a.HasSprite) return;
            a.SetSprite(sprite, appearance);
        }

        public List<VNCharState> GetStates()
        {
            var list = new List<VNCharState>();
            foreach (var kv in actors)
            {
                var a = kv.Value;
                if (!a.Visible) continue;
                list.Add(new VNCharState { name = a.Name, appearance = a.Appearance, pos = a.PosX, visible = a.Visible });
            }
            return list;
        }

        /// <summary>
        /// Instant restore. sprites[i] must match states[i] (same order, only visible entries).
        /// Call after loading all sprites via Addressables.
        /// </summary>
        public void RestoreStates(List<VNCharState> states, List<Sprite> sprites)
        {
            ClearAll();
            if (states == null) return;
            int si = 0;
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (!s.visible) continue;
                var a = new CharacterActor(runner.transform, s.name, runner);
                actors[s.name] = a;
                Sprite spr = (sprites != null && si < sprites.Count) ? sprites[si] : null;
                si++;
                a.SetSprite(spr, s.appearance);
                a.SetPosition(s.pos, 0f);
                a.Show(0f);
            }
        }

        /// <summary>left / midleft / center / midright / right, or a 0..1 fraction.</summary>
        public static float ParsePos(string token, float fallback)
        {
            if (string.IsNullOrEmpty(token)) return fallback;
            switch (token)
            {
                case "left": return 0.2f;
                case "midleft": return 0.35f;
                case "center": return 0.5f;
                case "midright": return 0.65f;
                case "right": return 0.8f;
            }
            float f;
            if (float.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out f))
                return Mathf.Clamp01(f);
            return fallback;
        }
    }
}