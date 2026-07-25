using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>Programmatic uGUI builders so no scene/prefab setup is ever required.</summary>
    public static class UIFactory
    {
        static TMP_FontAsset tmpFont;

        /// <summary>
        /// Default TMP font. Loads LiberationSans SDF from TMP Essential Resources,
        /// or any TMP_FontAsset under Resources. Call SetDefaultFont() to override.
        /// </summary>
        public static TMP_FontAsset DefaultTMPFont
        {
            get
            {
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

        /// <summary>Override the default font (e.g. after Addressables load).</summary>
        public static void SetDefaultFont(TMP_FontAsset font)
        {
            if (font != null) tmpFont = font;
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

        /// <summary>
        /// 32×32 white texture with 8-pixel borders so Image.Type.Sliced works.
        /// </summary>
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

        public static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.14f, 0.98f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.17f, 0.23f, 0.96f);
        public static readonly Color AccentColor = new Color(0.85f, 0.45f, 0.65f, 1f);
        public static readonly Color DarkSlotColor = new Color(0.05f, 0.06f, 0.09f, 1f);

        // ---------------- Basics ----------------

        public static Canvas CreateCanvas(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
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
            t.font = DefaultTMPFont;
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

        /// <summary>
        /// Outline via TMP built-in outline. dist is approximate legacy Outline distance in px.
        /// </summary>
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
                default:                     return TextAlignmentOptions.Center;
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

            var txt = Text(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)txt.transform);
            return btn;
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

            var labelText = Text(rt, "Label", label, 26, TextAnchor.MiddleLeft, Color.white);
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
            // Alpha must be > 0 for the Mask stencil to work reliably.
            AddImage(viewport.gameObject, new Color(1f, 1f, 1f, 0.01f));
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

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

        /// <summary>Centered window with background, title bar and a close button.</summary>
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

            var titleText = Text(header, "Title", title, 30, TextAnchor.MiddleLeft, Color.white);
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

        /// <summary>Full-screen dimming layer that blocks clicks to whatever is behind it.</summary>
        public static Image DimBackground(GameObject root, float alpha)
        {
            var img = AddImage(root, new Color(0f, 0f, 0f, alpha));
            img.raycastTarget = true;
            return img;
        }
    }
}