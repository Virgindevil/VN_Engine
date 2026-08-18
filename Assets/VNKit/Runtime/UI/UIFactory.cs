using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// Programmatic UI builders, so no scene/prefab setup is ever required.
    /// Text is TextMeshPro. Colors and the default font follow the active VNUITheme
    /// (VisualNovelEngine.uiTheme); without a theme the built-in defaults are used.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>Active theme, assigned by VisualNovelEngine at boot. May be null.</summary>
        public static VNUITheme Theme;

        static TMP_FontAsset tmpFont;

        /// <summary>
        /// Default TMP font: theme font → LiberationSans SDF from TMP essentials →
        /// any TMP_FontAsset in Resources. Call SetDefaultFont() to override.
        /// </summary>
        public static TMP_FontAsset DefaultTMPFont
        {
            get
            {
                if (Theme != null && Theme.font != null) return Theme.font;
                if (tmpFont != null) return tmpFont;

                tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (tmpFont == null)
                    tmpFont = Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF");

                if (tmpFont == null)
                {
                    var all = Resources.LoadAll<TMP_FontAsset>("");
                    if (all != null && all.Length > 0)
                        tmpFont = all[0];
                }

                if (tmpFont == null)
                    Debug.LogError("[VNKit] TMP font not found. Use Window → TextMeshPro → Import TMP Essential Resources.");

                return tmpFont;
            }
        }

        /// <summary>Override the default font (e.g. after loading one from Addressables).</summary>
        public static void SetDefaultFont(TMP_FontAsset font)
        {
            if (font != null) tmpFont = font;
        }

        // ---------------- Dynamic OS-font fallback (Cyrillic / CJK) ----------------

        static TMP_FontAsset dynamicFallback;
        static string dynamicFallbackLang;
        static bool fontCoverageWarned;

        // ---------------- Emoji fallback (Segoe UI Emoji / Noto / Apple) ----------------

        /// <summary>
        /// Set from VisualNovelEngine.useOsEmojiFont at boot. When false (user renders
        /// emoji through a color TMP Sprite Asset), the monochrome OS emoji font is
        /// skipped entirely — font fallbacks are searched before sprite assets, so
        /// keeping it would shadow the color sprites.
        /// </summary>
        public static bool UseOsEmojiFont = true;

        static TMP_FontAsset emojiFallback;
        static bool emojiFallbackTried;
        static readonly string[] EmojiFontCandidates =
            { "seguiemj", "seguisym", "notocoloremoji", "notoemoji", "applecoloremoji", "openmoji", "twemoji" };

        /// <summary>
        /// Emoji (🐶, 😂, …) live in dedicated OS fonts, not in text fonts. Build a dynamic
        /// TMP asset from the OS emoji font once and chain it AFTER the language fallback,
        /// so per-character lookup flows: theme font → language fallback → emoji font.
        /// Never throws; when no emoji font exists, emoji simply render as □ as before.
        /// </summary>
        static void EnsureEmojiFallback(TMP_FontAsset target)
        {
            if (!UseOsEmojiFont) return;
            if (!emojiFallbackTried)
            {
                emojiFallbackTried = true;
                try
                {
                    Font osFont = CreateOSFontFromFile(EmojiFontCandidates);
                    if (osFont != null)
                    {
                        emojiFallback = TMP_FontAsset.CreateFontAsset(osFont);
                        if (emojiFallback != null)
                        {
                            emojiFallback.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                            Debug.Log("[VNKit] Emoji OS font ready: " + osFont.name +
                                " — used only for characters missing from the primary font");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[VNKit] Emoji font setup failed: " + e.Message);
                    emojiFallback = null;
                }
            }
            if (emojiFallback == null) return;
            var host = dynamicFallback != null ? dynamicFallback : target;
            if (host == null || host == emojiFallback) return;
            if (host.fallbackFontAssetTable == null)
                host.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!host.fallbackFontAssetTable.Contains(emojiFallback))
                host.fallbackFontAssetTable.Add(emojiFallback);
        }

        /// <summary>OS font candidates per language group; first installed match wins.</summary>
        static string[] CandidatesForLanguage(string lang)
        {
            switch (lang)
            {
                case "ja":
                    return new[] { "Yu Gothic", "MS Gothic", "Meiryo", "Hiragino Sans",
                                   "Noto Sans CJK JP", "Arial Unicode MS", "Arial" };
                case "zh":
                    return new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC",
                                   "SimHei", "Hiragino Sans GB", "Arial Unicode MS", "Arial" };
                case "ko":
                    return new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo", "Arial" };
                default: // Latin + Cyrillic + Greek
                    return new[] { "Arial", "Segoe UI", "Noto Sans", "DejaVu Sans",
                                   "Liberation Sans", "Roboto", "Helvetica Neue" };
            }
        }

        /// <summary>Representative character used to test whether a font covers the language.</summary>
        static char SampleCharForLanguage(string lang)
        {
            switch (lang)
            {
                case "ru": case "uk": case "be": case "bg": case "sr": case "mk": return 'Н';
                case "ja": return 'あ';
                case "zh": return '中';
                case "ko": return '한';
                case "el": return 'Ω';
                default: return 'A';
            }
        }

        /// <summary>Alphabet pre-populated into the dynamic atlas (CJK glyphs are added on demand).</summary>
        static string AlphabetForLanguage(string lang)
        {
            switch (lang)
            {
                case "ru": case "uk": case "be": case "bg": case "sr": case "mk":
                    return "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
                           "абвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
                           "ІЇЄЎіїєўҐґ";
                case "el":
                    return "ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩαβγδεζηθικλμνξοπρστυφχψω";
                default:
                    return null; // Latin lives in the primary font; CJK is rasterized on demand
            }
        }

        /// <summary>
        /// True when the font asset can render the language's sample glyph.
        /// A freshly created dynamic font asset has an EMPTY character table, so a plain
        /// HasCharacter() check is wrong for it — instead we try to actually add the glyph:
        /// succeeds only when the source TTF/OTF contains it (and warms the atlas as a bonus).
        /// </summary>
        static bool CoversLanguage(TMP_FontAsset font, string lang)
        {
            if (font == null) return false;
            char sample = SampleCharForLanguage(lang);
            if (sample == 'A') return true; // Latin is assumed covered
            if (font.HasCharacter(sample)) return true;
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                try { return font.TryAddCharacters(sample.ToString()); }
                catch (System.Exception) { return false; }
            }
            return false; // статический атлас без символа — покрытия нет
        }

        /// <summary>
        /// Find an OS font FILE for one of the candidates and load it as a file-backed Font.
        /// A Font created from a file path contains real font data, so TMP can rasterize it.
        /// Font.CreateDynamicFontFromOSFont only references the font by name — TMP 3.x often
        /// cannot load such a face ("Unable to load font face ... Include Font Data").
        /// </summary>
        static Font CreateOSFontFromFile(string[] candidates)
        {
            string[] paths = null;
            try { paths = Font.GetPathsToOSFonts(); }
            catch (System.Exception) { }
            if (paths == null) return null;

            for (int c = 0; c < candidates.Length; c++)
            {
                string cand = candidates[c].ToLowerInvariant().Replace(" ", string.Empty);
                string best = null;
                int bestLen = int.MaxValue;
                for (int i = 0; i < paths.Length; i++)
                {
                    string p = paths[i];
                    if (string.IsNullOrEmpty(p)) continue;
                    string file;
                    try { file = System.IO.Path.GetFileName(p).ToLowerInvariant(); }
                    catch (System.Exception) { continue; }
                    if (!file.Contains(cand)) continue;
                    // Shortest filename wins — the base style (arial.ttf, not arialbd.ttf).
                    if (file.Length < bestLen) { bestLen = file.Length; best = p; }
                }
                if (best == null) continue;

                try
                {
                    var f = new Font(best);
                    if (f != null)
                    {
                        Debug.Log("[VNKit] OS font file: " + best);
                        return f;
                    }
                }
                catch (System.Exception) { }
            }
            return null;
        }

        /// <summary>
        /// The bundled LiberationSans SDF covers Latin only, so Cyrillic/CJK render as □.
        /// Build a dynamic TMP font asset from an OS font once, pre-populate its atlas with
        /// the language alphabet and chain it as a fallback onto whatever font the texts use.
        /// Rebuilt automatically when the language changes. Never throws: a missing font
        /// must not take the whole engine down.
        /// </summary>
        static void EnsureDynamicFallback(TMP_FontAsset target)
        {
            string lang = VNLoc.Language ?? "en";
            if (dynamicFallbackLang != lang) // одна попытка на язык (dynamicFallbackLang == null при старте)
            {
                TMP_FontAsset asset = null;
                try
                {
                    Font osFont = CreateOSFontFromFile(CandidatesForLanguage(lang));
                    if (osFont != null)
                    {
                        // Одноаргументная перегрузка: не зависит от GlyphRenderMode,
                        // чьё пространство имён различается между версиями TMP.
                        asset = TMP_FontAsset.CreateFontAsset(osFont);
                        if (asset == null)
                            Debug.LogWarning("[VNKit] TMP could not build a font asset from '" + osFont.name + "'.");
                    }
                    else
                    {
                        Debug.LogWarning("[VNKit] No suitable OS font found for language '" + lang + "'");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[VNKit] Dynamic font setup failed: " + e.Message);
                }

                if (asset != null)
                {
                    asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    // Сразу растеризуем алфавит: символы гарантированно попадают в таблицу
                    // шрифта ещё до первого рендера (никаких гонок добавления глифов).
                    string alphabet = AlphabetForLanguage(lang);
                    if (!string.IsNullOrEmpty(alphabet))
                        asset.TryAddCharacters(alphabet);
                    Debug.Log("[VNKit] Dynamic OS fallback font ready (language: " + lang +
                        ") — used only where the primary font lacks glyphs");
                    dynamicFallback = asset;
                }
                // One attempt per language — on failure we retry only after a language switch.
                dynamicFallbackLang = lang;
            }

            if (dynamicFallback == null || target == null) return;
            if (target.fallbackFontAssetTable == null)
                target.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!target.fallbackFontAssetTable.Contains(dynamicFallback))
                target.fallbackFontAssetTable.Add(dynamicFallback);
        }

        static Sprite uiSprite;
        public static Sprite UISprite
        {
            get
            {
                if (uiSprite != null) return uiSprite;
                uiSprite = CreateFallbackUISprite();
                return uiSprite;
            }
        }

        /// <summary>32x32 white texture with an 8px border, so Image.Type.Sliced works.</summary>
        static Sprite CreateFallbackUISprite()
        {
            const int size = 32;
            const int border = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "VNKit.FallbackUISprite";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var borderVec = new Vector4(border, border, border, border);
            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                borderVec);
        }

        static readonly Color DefaultPanel = new Color(0.09f, 0.10f, 0.14f, 0.98f);
        static readonly Color DefaultButton = new Color(0.16f, 0.17f, 0.23f, 0.96f);
        static readonly Color DefaultAccent = new Color(0.85f, 0.45f, 0.65f, 1f);
        public static readonly Color DarkSlotColor = new Color(0.05f, 0.06f, 0.09f, 1f);

        // Theme-aware colors (fall back to defaults when no theme is assigned).
        public static Color PanelColor { get { return Theme != null ? Theme.panelColor : DefaultPanel; } }
        public static Color ButtonColor { get { return Theme != null ? Theme.buttonColor : DefaultButton; } }
        public static Color AccentColor { get { return Theme != null ? Theme.accentColor : DefaultAccent; } }
        public static Color TextColor { get { return Theme != null ? Theme.textColor : Color.white; } }

        // ---------------- Basics ----------------

        public static Canvas CreateCanvas(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Reference resolution: all fractional layouts adapt to the real device
            // resolution/aspect automatically (match 0.5 = balanced width/height scaling).
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Image Image(Transform parent, string name, Color color)
        {
            var rt = Rect(name, parent);
            return AddImage(rt.gameObject, color);
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content,
            int size, TextAnchor anchor, Color color)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = DefaultTMPFont;
            EnsureDynamicFallback(font);
            EnsureEmojiFallback(font);
            // Если основной шрифт не покрывает активный язык — назначаем системный
            // шрифт ОСНОВНЫМ, а не fallback'ом: TMP кэширует промахи fallback-поиска,
            // и символы, однажды не найденные до подключения fallback, навсегда
            // остаются «□». Свой шрифт — свой кэш, промахов в нём нет.
            string textLang = VNLoc.Language ?? "en";
            if (dynamicFallback != null && !CoversLanguage(font, textLang))
            {
                if (!fontCoverageWarned)
                {
                    fontCoverageWarned = true;
                    Debug.LogWarning("[VNKit] Font '" + (font != null ? font.name : "null") +
                        "' does not cover language '" + textLang + "' — using the dynamic OS font instead. " +
                        "To use your own font: set Atlas Population Mode to Dynamic on its font asset " +
                        "and make sure the source TTF/OTF contains the needed characters.");
                }
                font = dynamicFallback;
            }
            t.font = font;
            t.text = content ?? "";
            t.fontSize = size;
            t.color = color;
            t.alignment = ToTMPAlignment(anchor);
            t.richText = true;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.extraPadding = true;
            return t;
        }

        /// <summary>Outline via TMP's built-in outline; falls back to uGUI Outline for other graphics.</summary>
        public static void AddOutline(Graphic g, Color c, float dist)
        {
            var tmp = g as TextMeshProUGUI;
            if (tmp != null)
            {
                tmp.outlineColor = c;
                tmp.outlineWidth = Mathf.Clamp(dist * 0.12f, 0.1f, 0.45f);
                return;
            }

            var o = g.gameObject.GetComponent<Outline>();
            if (o == null) o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(dist, -dist);
        }

        static TextAlignmentOptions ToTMPAlignment(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft:    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:  return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:   return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:   return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:  return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:  return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:   return TextAlignmentOptions.BottomRight;
                default:                      return TextAlignmentOptions.Center;
            }
        }

        public static LayoutElement Layout(GameObject go, float prefWidth, float prefHeight)
        {
            var le = go.AddComponent<LayoutElement>();
            if (prefWidth > 0f) le.preferredWidth = prefWidth;
            if (prefHeight > 0f) le.preferredHeight = prefHeight;
            return le;
        }

        // ---------------- Controls ----------------

        static ColorBlock ButtonColors(Color normal)
        {
            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = Color.Lerp(normal, Color.white, 0.25f),
                pressedColor = Color.Lerp(normal, Color.black, 0.25f),
                selectedColor = normal,
                disabledColor = new Color(normal.r, normal.g, normal.b, 0.4f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        public static Button Button(Transform parent, string name, string label, int fontSize, UnityAction onClick)
        {
            var rt = Rect(name, parent);
            var img = AddImage(rt.gameObject, ButtonColor);
            img.sprite = UISprite;
            // Fully qualified: UIFactory has a method named Image()
            img.type = UnityEngine.UI.Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = ButtonColors(img.color);
            if (onClick != null) btn.onClick.AddListener(onClick);

            var txt = Text(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, TextColor);
            Stretch((RectTransform)txt.transform);
            return btn;
        }

        // ============================== 2.12.3: live-localized variants ==============================
        // These build the same widgets as Text/Button/Toggle/Window but bind the
        // label to a VNLoc key via VNLocLabel, so a language change in settings
        // re-translates them immediately (no restart).

        public static TextMeshProUGUI LocText(Transform parent, string name, string locKey, int fontSize, TextAnchor anchor, Color color)
        {
            var t = Text(parent, name, VNLoc.T(locKey), fontSize, anchor, color);
            t.gameObject.AddComponent<VNLocLabel>().key = locKey;
            return t;
        }

        public static Button LocButton(Transform parent, string name, string locKey, int fontSize, UnityAction onClick)
        {
            var b = Button(parent, name, VNLoc.T(locKey), fontSize, onClick);
            b.GetComponentInChildren<TextMeshProUGUI>().gameObject.AddComponent<VNLocLabel>().key = locKey;
            return b;
        }

        public static Toggle LocToggle(Transform parent, string name, string locKey, bool value, UnityAction<bool> onChange)
        {
            var t = Toggle(parent, name, VNLoc.T(locKey), value, onChange);
            t.GetComponentInChildren<TextMeshProUGUI>().gameObject.AddComponent<VNLocLabel>().key = locKey;
            return t;
        }

        /// <summary>Re-bind the header title of a Window() to a VNLoc key.</summary>
        public static void LocalizeWindowTitle(RectTransform win, string locKey)
        {
            var titleT = win.Find("Header/Title");
            if (titleT == null) return;
            var tmp = titleT.GetComponent<TextMeshProUGUI>();
            if (tmp == null) return;
            tmp.text = VNLoc.T(locKey);
            var loc = titleT.GetComponent<VNLocLabel>();
            if (loc == null) loc = titleT.gameObject.AddComponent<VNLocLabel>();
            loc.key = locKey;
        }

        public static Slider Slider(Transform parent, string name, float min, float max, float value, UnityAction<float> onChange)
        {
            var rt = Rect(name, parent);
            var slider = rt.gameObject.AddComponent<Slider>();

            var bgRT = Rect("Background", rt);
            bgRT.anchorMin = new Vector2(0f, 0.25f);
            bgRT.anchorMax = new Vector2(1f, 0.75f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = AddImage(bgRT.gameObject, DarkSlotColor);
            bgImg.sprite = UISprite;
            bgImg.type = UnityEngine.UI.Image.Type.Sliced;

            var fillArea = Rect("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(5f, 0f);
            fillArea.offsetMax = new Vector2(-15f, 0f);
            var fillRT = Rect("Fill", fillArea);
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.sizeDelta = new Vector2(10f, 0f);
            var fillImg = AddImage(fillRT.gameObject, AccentColor);
            fillImg.sprite = UISprite;
            fillImg.type = UnityEngine.UI.Image.Type.Sliced;

            var handleArea = Rect("Handle Slide Area", rt);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            var handleRT = Rect("Handle", handleArea);
            handleRT.anchorMin = Vector2.zero;
            handleRT.anchorMax = new Vector2(0f, 1f);
            handleRT.sizeDelta = new Vector2(24f, 0f);
            var handleImg = AddImage(handleRT.gameObject, new Color(0.92f, 0.92f, 0.95f, 1f));
            handleImg.sprite = UISprite;
            handleImg.type = UnityEngine.UI.Image.Type.Sliced;

            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(value);
            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        public static Toggle Toggle(Transform parent, string name, string label, bool value, UnityAction<bool> onChange)
        {
            var rt = Rect(name, parent);
            var toggle = rt.gameObject.AddComponent<Toggle>();

            var bgRT = Rect("Background", rt);
            bgRT.anchorMin = new Vector2(0f, 0.5f);
            bgRT.anchorMax = new Vector2(0f, 0.5f);
            bgRT.pivot = new Vector2(0f, 0.5f);
            bgRT.sizeDelta = new Vector2(30f, 30f);
            bgRT.anchoredPosition = new Vector2(15f, 0f);
            var bgImg = AddImage(bgRT.gameObject, DarkSlotColor);
            bgImg.sprite = UISprite;
            bgImg.type = UnityEngine.UI.Image.Type.Sliced;

            var checkRT = Rect("Checkmark", bgRT);
            checkRT.anchorMin = Vector2.zero;
            checkRT.anchorMax = Vector2.one;
            checkRT.offsetMin = new Vector2(5f, 5f);
            checkRT.offsetMax = new Vector2(-5f, -5f);
            var checkImg = AddImage(checkRT.gameObject, AccentColor);
            checkImg.sprite = UISprite;
            checkImg.type = UnityEngine.UI.Image.Type.Sliced;

            var labelText = Text(rt, "Label", label, 26, TextAnchor.MiddleLeft, TextColor);
            var lrt = (RectTransform)labelText.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(56f, 0f);
            lrt.offsetMax = Vector2.zero;

            toggle.graphic = checkImg;
            toggle.targetGraphic = bgImg;
            toggle.SetIsOnWithoutNotify(value);
            if (onChange != null) toggle.onValueChanged.AddListener(onChange);
            return toggle;
        }

        public static ScrollRect ScrollView(Transform parent, string name, out RectTransform content)
        {
            var rt = Rect(name, parent);
            var scroll = rt.gameObject.AddComponent<ScrollRect>();

            var viewport = Rect("Viewport", rt);
            Stretch(viewport);
            // Clipping is done by RectMask2D — it does NOT depend on the alpha of any
            // graphic (the old Mask + near-transparent image made content invisible
            // when the viewport graphic rendered at alpha 0).
            viewport.gameObject.AddComponent<RectMask2D>();
            // Transparent raycast target so wheel/drag scrolling works over the whole viewport.
            var raycastImg = AddImage(viewport.gameObject, new Color(1f, 1f, 1f, 0f));
            raycastImg.raycastTarget = true;

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(12, 12, 12, 12);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }

        /// <summary>Anchored window with a background, a title header and a close button.</summary>
        public static RectTransform Window(Transform parent, string title, Vector2 min, Vector2 max, out Button closeButton)
        {
            var win = Rect("Window", parent);
            Anchor(win, min, max);
            var bg = AddImage(win.gameObject, PanelColor);
            bg.sprite = UISprite;
            bg.type = UnityEngine.UI.Image.Type.Sliced;

            var header = Rect("Header", win);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 56f);
            header.anchoredPosition = Vector2.zero;

            var titleText = Text(header, "Title", title, 30, TextAnchor.MiddleLeft, TextColor);
            var ttrt = (RectTransform)titleText.transform;
            ttrt.anchorMin = Vector2.zero;
            ttrt.anchorMax = Vector2.one;
            ttrt.offsetMin = new Vector2(24f, 0f);
            ttrt.offsetMax = new Vector2(-70f, 0f);

            closeButton = Button(header, "Close", "X", 22, null);
            var cbrt = (RectTransform)closeButton.transform;
            cbrt.anchorMin = new Vector2(1f, 0.5f);
            cbrt.anchorMax = new Vector2(1f, 0.5f);
            cbrt.pivot = new Vector2(1f, 0.5f);
            cbrt.sizeDelta = new Vector2(44f, 40f);
            cbrt.anchoredPosition = new Vector2(-8f, 0f);

            return win;
        }

        /// <summary>Full-screen dim layer that blocks clicks to whatever is behind it.</summary>
        public static Image DimBackground(GameObject root, float alpha)
        {
            var img = AddImage(root, new Color(0f, 0f, 0f, alpha));
            img.raycastTarget = true;
            return img;
        }
    }
}
