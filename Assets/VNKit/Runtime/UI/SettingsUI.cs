using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// Tabbed settings: Sound (volumes), Video (resolution / fullscreen), Game (text speed,
    /// auto speed, skip-mode, language, rebindable hotkeys). Changes apply and save immediately.
    /// </summary>
    public class SettingsUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly VisualNovelEngine engine;
        readonly VNRunner host;
        readonly RectTransform soundPanel;
        readonly RectTransform videoPanel;
        readonly RectTransform gamePanel;
        readonly Button soundTab;
        readonly Button videoTab;
        readonly Button gameTab;
        TextMeshProUGUI resLabel;
        TextMeshProUGUI langLabel;
        TextMeshProUGUI skipKeyLabel;
        TextMeshProUGUI autoKeyLabel;
        TextMeshProUGUI rollbackKeyLabel;
        readonly List<Resolution> uniqueResolutions = new List<Resolution>();
        int resIndex;
        Coroutine keyCapture;

        static readonly string[] Languages = { "en", "ru", "ja", "zh", "de", "fr", "es", "ko" };
        static readonly string[] LanguageNames = { "English", "Русский", "日本語", "中文", "Deutsch", "Français", "Español", "한국어" };

        public SettingsUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            root = UIFactory.Rect("VNKit.Settings", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.6f);
            host = VNRunner.Create("VNKit.SettingsHost", root.transform);

            Button closeBtn;
            var win = UIFactory.Window(root.transform, VNLoc.T("settings.title"),
                new Vector2(0.24f, 0.06f), new Vector2(0.76f, 0.94f), out closeBtn);
            closeBtn.onClick.AddListener(Hide);
            UIFactory.LocalizeWindowTitle(win, "settings.title"); // 2.12.3: live language switch

            // ---- Tab bar under the header ----
            var tabBar = UIFactory.Rect("TabBar", win);
            tabBar.anchorMin = new Vector2(0.04f, 0.88f);
            tabBar.anchorMax = new Vector2(0.96f, 0.94f);
            tabBar.offsetMin = Vector2.zero;
            tabBar.offsetMax = Vector2.zero;

            var tabHlg = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;
            tabHlg.spacing = 10f;
            tabHlg.padding = new RectOffset(4, 4, 2, 2);

            soundTab = MakeTab(tabBar, "Sound", () => ShowTab(0));
            videoTab = MakeTab(tabBar, "Video", () => ShowTab(1));
            gameTab  = MakeTab(tabBar, "Game",  () => ShowTab(2));

            // ---- Content area ----
            var contentArea = UIFactory.Rect("ContentArea", win);
            contentArea.anchorMin = new Vector2(0.04f, 0.03f);
            contentArea.anchorMax = new Vector2(0.96f, 0.87f);
            contentArea.offsetMin = Vector2.zero;
            contentArea.offsetMax = Vector2.zero;

            soundPanel = BuildSoundPanel(contentArea);
            videoPanel = BuildVideoPanel(contentArea);
            gamePanel  = BuildGamePanel(contentArea);

            ShowTab(0);
            root.SetActive(false);
        }

        Button MakeTab(RectTransform parent, string id, UnityAction onClick)
        {
            var b = UIFactory.LocButton(parent, "Tab." + id, "settings." + id.ToLower(), 24, onClick);
            UIFactory.Layout(b.gameObject, 0f, 0f);
            return b;
        }

        void ShowTab(int index)
        {
            soundPanel.gameObject.SetActive(index == 0);
            videoPanel.gameObject.SetActive(index == 1);
            gamePanel.gameObject.SetActive(index == 2);
            HighlightTab(soundTab, index == 0);
            HighlightTab(videoTab, index == 1);
            HighlightTab(gameTab,  index == 2);
            if (index == 1) RefreshResolutionLabel();
            if (index == 2) RefreshLanguageLabel();
        }

        void HighlightTab(Button b, bool active)
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

        // ============================== Sound ==============================

        RectTransform BuildSoundPanel(RectTransform parent)
        {
            var panel = UIFactory.Rect("SoundPanel", parent);
            UIFactory.Stretch(panel);
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 18f;
            vlg.padding = new RectOffset(8, 8, 12, 8);

            var s = engine.Settings;
            AddSliderRow(panel, "Master", "settings.master", 0f, 1f, s.masterVolume, v => { s.masterVolume = v; engine.ApplySettings(); });
            AddSliderRow(panel, "Bgm", "settings.bgm", 0f, 1f, s.bgmVolume, v => { s.bgmVolume = v; engine.ApplySettings(); });
            AddSliderRow(panel, "Sfx", "settings.sfx", 0f, 1f, s.sfxVolume, v => { s.sfxVolume = v; engine.ApplySettings(); });
            AddSliderRow(panel, "Voice", "settings.voice", 0f, 1f, s.voiceVolume, v => { s.voiceVolume = v; engine.ApplySettings(); });
            return panel;
        }

        // ============================== Video ==============================

        RectTransform BuildVideoPanel(RectTransform parent)
        {
            var panel = UIFactory.Rect("VideoPanel", parent);
            UIFactory.Stretch(panel);
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 22f;
            vlg.padding = new RectOffset(8, 8, 12, 8);

            BuildUniqueResolutions();
            var s = engine.Settings;

            resIndex = 0;
            for (int i = 0; i < uniqueResolutions.Count; i++)
            {
                if (uniqueResolutions[i].width == s.resolutionWidth && uniqueResolutions[i].height == s.resolutionHeight)
                {
                    resIndex = i;
                    break;
                }
            }

            var resRow = UIFactory.Rect("ResRow", panel);
            UIFactory.Layout(resRow.gameObject, 0f, 52f);

            var resTitle = UIFactory.LocText(resRow, "Label", "settings.resolution", 24, TextAnchor.MiddleLeft, UIFactory.TextColor);
            var rtrt = (RectTransform)resTitle.transform;
            rtrt.anchorMin = Vector2.zero;
            rtrt.anchorMax = new Vector2(0.32f, 1f);
            rtrt.offsetMin = Vector2.zero;
            rtrt.offsetMax = Vector2.zero;

            var prevBtn = UIFactory.Button(resRow, "Prev", "<", 26, () => CycleResolution(-1));
            var pbrt = (RectTransform)prevBtn.transform;
            pbrt.anchorMin = new Vector2(0.34f, 0.15f);
            pbrt.anchorMax = new Vector2(0.44f, 0.85f);
            pbrt.offsetMin = Vector2.zero;
            pbrt.offsetMax = Vector2.zero;

            resLabel = UIFactory.Text(resRow, "Value", "1920 × 1080", 24, TextAnchor.MiddleCenter, UIFactory.TextColor);
            var vlrt = (RectTransform)resLabel.transform;
            vlrt.anchorMin = new Vector2(0.45f, 0f);
            vlrt.anchorMax = new Vector2(0.80f, 1f);
            vlrt.offsetMin = Vector2.zero;
            vlrt.offsetMax = Vector2.zero;

            var nextBtn = UIFactory.Button(resRow, "Next", ">", 26, () => CycleResolution(1));
            var nbrt = (RectTransform)nextBtn.transform;
            nbrt.anchorMin = new Vector2(0.82f, 0.15f);
            nbrt.anchorMax = new Vector2(0.92f, 0.85f);
            nbrt.offsetMin = Vector2.zero;
            nbrt.offsetMax = Vector2.zero;

            var fsToggle = UIFactory.LocToggle(panel, "Fullscreen", "settings.fullscreen", s.fullscreen, v =>
            {
                s.fullscreen = v;
                engine.ApplySettings();
            });
            UIFactory.Layout(fsToggle.gameObject, 0f, 48f);

            var hint = UIFactory.LocText(panel, "Hint", "settings.reshint", 18, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.75f));
            UIFactory.Layout(hint.gameObject, 0f, 32f);

            RefreshResolutionLabel();
            return panel;
        }

        void BuildUniqueResolutions()
        {
            uniqueResolutions.Clear();
            var seen = new HashSet<string>();
            var list = Screen.resolutions;
            for (int i = list.Length - 1; i >= 0; i--)
            {
                var r = list[i];
                string key = r.width + "x" + r.height;
                if (seen.Add(key))
                    uniqueResolutions.Add(r);
            }
            uniqueResolutions.Reverse();
            if (uniqueResolutions.Count == 0)
            {
                uniqueResolutions.Add(new Resolution { width = 1280, height = 720 });
                uniqueResolutions.Add(new Resolution { width = 1920, height = 1080 });
            }
        }

        void CycleResolution(int delta)
        {
            if (uniqueResolutions.Count == 0) return;
            resIndex = (resIndex + delta + uniqueResolutions.Count) % uniqueResolutions.Count;
            var r = uniqueResolutions[resIndex];
            engine.Settings.resolutionWidth = r.width;
            engine.Settings.resolutionHeight = r.height;
            engine.ApplySettings();
            RefreshResolutionLabel();
        }

        void RefreshResolutionLabel()
        {
            if (resLabel == null) return;
            if (uniqueResolutions.Count > 0 && resIndex >= 0 && resIndex < uniqueResolutions.Count)
            {
                var r = uniqueResolutions[resIndex];
                resLabel.text = r.width + " × " + r.height;
            }
            else
            {
                resLabel.text = engine.Settings.resolutionWidth + " × " + engine.Settings.resolutionHeight;
            }
        }

        // ============================== Game ==============================

        RectTransform BuildGamePanel(RectTransform parent)
        {
            var panel = UIFactory.Rect("GamePanel", parent);
            UIFactory.Stretch(panel);
            var scroll = UIFactory.ScrollView(panel, "Scroll", out RectTransform content);
            UIFactory.Stretch((RectTransform)scroll.transform);

            var s = engine.Settings;

            AddSliderRow(content, "TextSpeed", "settings.textspeed", 10f, 120f, s.textSpeed, v => { s.textSpeed = v; engine.ApplySettings(); });
            // UI shows "speed" (higher = faster). Internally we store delay in seconds: delay = 5.5 - speed.
            float autoSpeed = Mathf.Clamp(5.5f - s.autoDelay, 0.5f, 5f);
            AddSliderRow(content, "AutoSpeed", "settings.autospeed", 0.5f, 5f, autoSpeed, v =>
            {
                s.autoDelay = 5.5f - v;
                engine.ApplySettings();
            });

            var skipToggle = UIFactory.LocToggle(content, "SkipUnread", "settings.skipunread", s.skipUnreadOnly, v =>
            {
                s.skipUnreadOnly = v;
                engine.ApplySettings();
            });
            UIFactory.Layout(skipToggle.gameObject, 0f, 48f);

            // ---- Language ----
            var langRow = UIFactory.Rect("LangRow", content);
            UIFactory.Layout(langRow.gameObject, 0f, 52f);

            var langTitle = UIFactory.LocText(langRow, "Label", "settings.language", 24, TextAnchor.MiddleLeft, UIFactory.TextColor);
            var ltrt = (RectTransform)langTitle.transform;
            ltrt.anchorMin = Vector2.zero;
            ltrt.anchorMax = new Vector2(0.32f, 1f);
            ltrt.offsetMin = Vector2.zero;
            ltrt.offsetMax = Vector2.zero;

            var langPrev = UIFactory.Button(langRow, "Prev", "<", 26, () => CycleLanguage(-1));
            var lprt = (RectTransform)langPrev.transform;
            lprt.anchorMin = new Vector2(0.34f, 0.15f);
            lprt.anchorMax = new Vector2(0.44f, 0.85f);
            lprt.offsetMin = Vector2.zero;
            lprt.offsetMax = Vector2.zero;

            langLabel = UIFactory.Text(langRow, "Value", "English", 24, TextAnchor.MiddleCenter, UIFactory.TextColor);
            var llrt = (RectTransform)langLabel.transform;
            llrt.anchorMin = new Vector2(0.45f, 0f);
            llrt.anchorMax = new Vector2(0.80f, 1f);
            llrt.offsetMin = Vector2.zero;
            llrt.offsetMax = Vector2.zero;

            var langNext = UIFactory.Button(langRow, "Next", ">", 26, () => CycleLanguage(1));
            var lnrt = (RectTransform)langNext.transform;
            lnrt.anchorMin = new Vector2(0.82f, 0.15f);
            lnrt.anchorMax = new Vector2(0.92f, 0.85f);
            lnrt.offsetMin = Vector2.zero;
            lnrt.offsetMax = Vector2.zero;

            // ---- Rebindable hotkeys ----
            var hotHeader = UIFactory.LocText(content, "HotkeysHeader", "settings.controls", 22, TextAnchor.MiddleCenter, new Color(0.75f, 0.7f, 0.8f));
            UIFactory.Layout(hotHeader.gameObject, 0f, 36f);

            skipKeyLabel = AddRebindRow(content, "SkipKey", "settings.skipkey", s.skipKey,
                delegate { StartKeyCapture(0); });
            autoKeyLabel = AddRebindRow(content, "AutoKey", "settings.autokey", s.autoKey,
                delegate { StartKeyCapture(1); });
            rollbackKeyLabel = AddRebindRow(content, "RollbackKey", "settings.hk.rollback", s.rollbackKey,
                delegate { StartKeyCapture(2); });

            AddHotkeyLine(content, "settings.hk.advance", "Space / Enter / LMB");
            AddHotkeyLine(content, "settings.hk.hide", "RMB");
            AddHotkeyLine(content, "settings.hk.cancel", "Esc");
            AddHotkeyLine(content, "settings.hk.rollback", "Mouse Wheel Up / PageUp");

            RefreshLanguageLabel();
            return panel;
        }

        TextMeshProUGUI AddRebindRow(RectTransform content, string id, string locKey, KeyCode current, UnityAction onRebind)
        {
            var row = UIFactory.Rect("Rebind." + id, content);
            UIFactory.Layout(row.gameObject, 0f, 44f);

            var txt = UIFactory.LocText(row, "Label", locKey, 22, TextAnchor.MiddleLeft, UIFactory.TextColor);
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = new Vector2(0.45f, 1f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var btn = UIFactory.Button(row, "Key", current.ToString(), 22, onRebind);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = new Vector2(0.5f, 0.1f);
            brt.anchorMax = new Vector2(0.85f, 0.9f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            var keyText = brt.Find("Label").GetComponent<TextMeshProUGUI>();
            return keyText;
        }

        void StartKeyCapture(int which) // 0 = skip, 1 = auto, 2 = rollback
        {
            if (keyCapture != null) host.StopCoroutine(keyCapture);
            var label = which == 0 ? skipKeyLabel : which == 1 ? autoKeyLabel : rollbackKeyLabel;
            keyCapture = host.StartCoroutine(CaptureKeyRoutine(which, label));
        }

        IEnumerator CaptureKeyRoutine(int which, TextMeshProUGUI label)
        {
            if (label != null) label.text = VNLoc.T("settings.presskey");
            yield return null; // skip the click frame
            while (true)
            {
                KeyCode k;
                if (VNInput.AnyKeyDown(out k))
                {
                    if (k != KeyCode.Escape) // Esc cancels the capture
                    {
                        if (which == 0) engine.Settings.skipKey = k;
                        else if (which == 1) engine.Settings.autoKey = k;
                        else engine.Settings.rollbackKey = k;
                        engine.ApplySettings();
                        if (label != null) label.text = k.ToString();
                    }
                    else if (label != null)
                    {
                        label.text = (which == 0 ? engine.Settings.skipKey
                            : which == 1 ? engine.Settings.autoKey
                            : engine.Settings.rollbackKey).ToString();
                    }
                    break;
                }
                yield return null;
            }
            keyCapture = null;
        }

        void AddHotkeyLine(RectTransform parent, string locKey, string keys)
        {
            var row = UIFactory.Rect("Hot." + locKey, parent);
            UIFactory.Layout(row.gameObject, 0f, 30f);

            var a = UIFactory.LocText(row, "Action", locKey, 20, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.9f));
            var art = (RectTransform)a.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = new Vector2(0.42f, 1f);
            art.offsetMin = Vector2.zero;
            art.offsetMax = Vector2.zero;

            var k = UIFactory.Text(row, "Keys", keys, 20, TextAnchor.MiddleLeft, UIFactory.TextColor);
            var krt = (RectTransform)k.transform;
            krt.anchorMin = new Vector2(0.44f, 0f);
            krt.anchorMax = Vector2.one;
            krt.offsetMin = Vector2.zero;
            krt.offsetMax = Vector2.zero;
        }

        void CycleLanguage(int delta)
        {
            int idx = 0;
            for (int i = 0; i < Languages.Length; i++)
            {
                if (Languages[i] == engine.Settings.language) { idx = i; break; }
            }
            idx = (idx + delta + Languages.Length) % Languages.Length;
            engine.Settings.language = Languages[idx];
            engine.ApplySettings();
            RefreshLanguageLabel();
        }

        void RefreshLanguageLabel()
        {
            if (langLabel == null) return;
            for (int i = 0; i < Languages.Length; i++)
            {
                if (Languages[i] == engine.Settings.language)
                {
                    langLabel.text = LanguageNames[i];
                    return;
                }
            }
            langLabel.text = engine.Settings.language;
        }

        // ============================== Helpers ==============================

        void AddSliderRow(RectTransform content, string id, string locKey, float min, float max, float value, UnityAction<float> onChange)
        {
            var row = UIFactory.Rect("Row." + id, content);
            UIFactory.Layout(row.gameObject, 0f, 46f);

            var txt = UIFactory.LocText(row, "Label", locKey, 24, TextAnchor.MiddleLeft, UIFactory.TextColor);
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
            resIndex = 0;
            for (int i = 0; i < uniqueResolutions.Count; i++)
            {
                if (uniqueResolutions[i].width == engine.Settings.resolutionWidth
                    && uniqueResolutions[i].height == engine.Settings.resolutionHeight)
                {
                    resIndex = i;
                    break;
                }
            }
            RefreshResolutionLabel();
            RefreshLanguageLabel();
            if (skipKeyLabel != null) skipKeyLabel.text = engine.Settings.skipKey.ToString();
            if (autoKeyLabel != null) autoKeyLabel.text = engine.Settings.autoKey.ToString();
            if (rollbackKeyLabel != null) rollbackKeyLabel.text = engine.Settings.rollbackKey.ToString();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (keyCapture != null) { host.StopCoroutine(keyCapture); keyCapture = null; }
            root.SetActive(false);
        }
    }
}
