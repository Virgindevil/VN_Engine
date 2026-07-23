using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Volume, text speed and auto-mode delay sliders. Persisted automatically.</summary>
    public class SettingsUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;

        public SettingsUI(Transform parent, VisualNovelEngine engine)
        {
            root = UIFactory.Rect("VNKit.Settings", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.6f);

            Button closeBtn;
            var win = UIFactory.Window(root.transform, "Settings",
                new Vector2(0.28f, 0.14f), new Vector2(0.72f, 0.88f), out closeBtn);
            closeBtn.onClick.AddListener(Hide);

            var content = UIFactory.Rect("Content", win);
            content.anchorMin = new Vector2(0.06f, 0.05f);
            content.anchorMax = new Vector2(0.94f, 0.86f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 20f;

            var s = engine.Settings;
            AddRow(content, "Master Volume", 0f, 1f, s.masterVolume, delegate (float v) { s.masterVolume = v; engine.ApplySettings(); });
            AddRow(content, "BGM Volume", 0f, 1f, s.bgmVolume, delegate (float v) { s.bgmVolume = v; engine.ApplySettings(); });
            AddRow(content, "SFX Volume", 0f, 1f, s.sfxVolume, delegate (float v) { s.sfxVolume = v; engine.ApplySettings(); });
            AddRow(content, "Voice Volume", 0f, 1f, s.voiceVolume, delegate (float v) { s.voiceVolume = v; engine.ApplySettings(); });
            AddRow(content, "Text Speed", 10f, 120f, s.textSpeed, delegate (float v) { s.textSpeed = v; engine.ApplySettings(); });
            AddRow(content, "Auto Play Delay", 0.5f, 5f, s.autoDelay, delegate (float v) { s.autoDelay = v; engine.ApplySettings(); });

            root.SetActive(false);
        }

        void AddRow(RectTransform content, string label, float min, float max, float value, UnityAction<float> onChange)
        {
            var row = UIFactory.Rect("Row." + label, content);
            UIFactory.Layout(row.gameObject, 0f, 46f);

            var txt = UIFactory.Text(row, "Label", label, 24, TextAnchor.MiddleLeft, Color.white);
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = new Vector2(0.38f, 1f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var slider = UIFactory.Slider(row, "Slider", min, max, value, onChange);
            var srt = (RectTransform)slider.transform;
            srt.anchorMin = new Vector2(0.42f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
        }

        public void Show()
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            root.SetActive(false);
        }
    }
}
