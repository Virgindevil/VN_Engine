using System.Collections.Generic;
using UnityEngine;

namespace VNKit
{
    /// <summary>
    /// Spawns, moves, hides and restores the on-stage characters.
    /// Sprites / Spine skeletons are loaded asynchronously by the caller (ScriptPlayer
    /// or VisualNovelEngine) and handed over ready-made.
    /// </summary>
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

        public void ApplyCommand(VNCommand cmd, Sprite sprite, Object spineData)
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
            Show(name, appearance, pos, time, sprite, spineData);
        }

        public void Show(string name, string appearance, float pos, float time, Sprite sprite, Object spineData)
        {
            CharacterActor a;
            bool isNew = !actors.TryGetValue(name, out a);
            if (isNew)
            {
                a = new CharacterActor(runner.transform, name, runner);
                actors[name] = a;
            }

            var spineCfg = engine.GetSpineCharacter(name);
            if (spineCfg != null)
            {
                // Spine character: appearance == animation name.
                string anim = appearance ?? (a.Spine != null ? a.Appearance : null) ?? spineCfg.defaultAnimation;
                a.SetSpine(spineData, anim, spineCfg);
            }
            else
            {
                string app = appearance ?? (a.Appearance ?? "Default");
                if (app != a.Appearance || !a.HasSprite)
                    a.SetSprite(sprite, app);
            }

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

        /// <summary>Instant appearance/animation swap used by "Name.Appearance:" dialogue prefixes.</summary>
        public void SetAppearance(string name, string appearance, Sprite sprite, Object spineData)
        {
            CharacterActor a;
            if (!actors.TryGetValue(name, out a)) return;
            if (a.Appearance == appearance) return;

            var spineCfg = engine.GetSpineCharacter(name);
            if (spineCfg != null)
                a.SetSpine(spineData, appearance, spineCfg);
            else
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

        /// <summary>Instant restore from a save file. sprites / spineDatas align with visible states.</summary>
        public void RestoreStates(List<VNCharState> states, List<Sprite> sprites, List<Object> spineDatas)
        {
            ClearAll();
            if (states == null) return;
            int i = 0;
            foreach (var s in states)
            {
                if (!s.visible) continue;
                var a = new CharacterActor(runner.transform, s.name, runner);
                actors[s.name] = a;

                Sprite spr = sprites != null && i < sprites.Count ? sprites[i] : null;
                Object skel = spineDatas != null && i < spineDatas.Count ? spineDatas[i] : null;

                var spineCfg = engine.GetSpineCharacter(s.name);
                if (spineCfg != null)
                    a.SetSpine(skel, s.appearance ?? spineCfg.defaultAnimation, spineCfg);
                else
                    a.SetSprite(spr, s.appearance);

                a.SetPosition(s.pos, 0f);
                a.Show(0f);
                i++;
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
