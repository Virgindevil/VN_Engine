using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Small button row above the dialogue panel: Backlog / Save / Load / Auto / Skip / CG / Settings / Title.</summary>
    public class QuickMenuUI
    {
        readonly GameObject root;
        readonly VisualNovelEngine engine;
        readonly Button autoBtn;
        readonly Button skipBtn;

        public QuickMenuUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            float btnW = UIFactory.Theme != null ? UIFactory.Theme.quickMenuButtonWidth : 112f;

            root = UIFactory.Rect("VNKit.QuickMenu", parent).gameObject;
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(1f, 0.272f);
            rt.anchorMax = new Vector2(1f, 0.33f);
            rt.pivot = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(-(btnW * 8 + 7 * 8 + 8), 0f);
            rt.offsetMax = new Vector2(-8f, 0f);

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f;

            // 2.12.3: key-bound labels re-translate on live language change.
            AddButton("backlog", "qm.backlog", engine.OpenBacklog, btnW);
            AddButton("save", "qm.save", engine.OpenSavePanel, btnW);
            AddButton("load", "qm.load", engine.OpenLoadPanel, btnW);
            autoBtn = AddButton("auto", "qm.auto", engine.ToggleAuto, btnW);
            skipBtn = AddButton("skip", "qm.skip", engine.ToggleSkip, btnW);
            AddButton("cg", "qm.gallery", engine.OpenGallery, btnW);
            AddButton("settings", "qm.settings", engine.OpenSettings, btnW);
            AddButton("title", "qm.title", engine.ReturnToTitle, btnW);

            root.SetActive(false);
        }

        Button AddButton(string name, string locKey, UnityAction action, float width)
        {
            var b = UIFactory.LocButton(root.transform, name, locKey, 20, action);
            UIFactory.Layout(b.gameObject, width, 0f);
            return b;
        }

        public bool IsVisible { get { return root.activeSelf; } }

        public void SetVisible(bool visible)
        {
            root.SetActive(visible);
        }

        public void RefreshToggles()
        {
            SetButtonActive(autoBtn, engine.Player != null && engine.Player.AutoMode);
            SetButtonActive(skipBtn, engine.Player != null && engine.Player.SkipMode);
        }

        void SetButtonActive(Button b, bool active)
        {
            var img = b.GetComponent<Image>();
            Color c = active ? engine.accentColor : UIFactory.ButtonColor;
            img.color = c;
            var cb = b.colors;
            cb.normalColor = c;
            cb.highlightedColor = Color.Lerp(c, Color.white, 0.25f);
            cb.pressedColor = Color.Lerp(c, Color.black, 0.25f);
            b.colors = cb;
        }
    }
}
