using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Small button row above the dialogue panel: Backlog / Save / Load / Auto / Skip / Settings / Title.</summary>
    public class QuickMenuUI
    {
        readonly GameObject root;
        readonly VisualNovelEngine engine;
        readonly Button autoBtn;
        readonly Button skipBtn;

        public QuickMenuUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            root = UIFactory.Rect("VNKit.QuickMenu", parent).gameObject;
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(1f, 0.272f);
            rt.anchorMax = new Vector2(1f, 0.33f);
            rt.pivot = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(-830f, 0f);
            rt.offsetMax = new Vector2(-8f, 0f);

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f;

            AddButton("Backlog", engine.OpenBacklog);
            AddButton("Save", engine.OpenSavePanel);
            AddButton("Load", engine.OpenLoadPanel);
            autoBtn = AddButton("Auto", engine.ToggleAuto);
            skipBtn = AddButton("Skip", engine.ToggleSkip);
            AddButton("Settings", engine.OpenSettings);
            AddButton("Title", engine.ReturnToTitle);

            root.SetActive(false);
        }

        Button AddButton(string label, UnityAction action)
        {
            var b = UIFactory.Button(root.transform, label, label, 20, action);
            UIFactory.Layout(b.gameObject, 112f, 0f);
            return b;
        }

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
