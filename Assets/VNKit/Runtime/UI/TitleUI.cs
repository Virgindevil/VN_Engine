using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Title screen: background, game name, menu buttons.
    /// Button column position, button size/spacing, fonts and colors come from the
    /// active VNUITheme (engine.uiTheme) — rearrange the main menu without code.
    /// </summary>
    public class TitleUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;

        public TitleUI(Transform parent, VisualNovelEngine engine)
        {
            var theme = UIFactory.Theme;

            root = UIFactory.Rect("VNKit.Title", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);

            // Background
            var bgRT = UIFactory.Rect("Background", root.transform);
            UIFactory.Stretch(bgRT);
            var bgImg = UIFactory.AddImage(bgRT.gameObject, new Color(0.05f, 0.06f, 0.10f, 1f));
            if (engine.titleBackground != null)
            {
                bgImg.sprite = engine.titleBackground;
                bgImg.color = Color.white;
                bgImg.preserveAspect = false;
                var fitter = bgRT.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                var r = engine.titleBackground.rect;
                fitter.aspectRatio = r.width / r.height;
            }

            // Title text
            var title = UIFactory.Text(root.transform, "Title", engine.gameTitle,
                theme != null ? theme.titleFontSize : 76, TextAnchor.MiddleCenter, UIFactory.TextColor);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0.15f, 0.58f);
            trt.anchorMax = new Vector2(0.85f, 0.80f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            title.fontStyle = TMPro.FontStyles.Bold;
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.8f), 2f);

            // Logo (optional, above the title text)
            if (engine.titleLogo != null)
            {
                var logoRT = UIFactory.Rect("Logo", root.transform);
                logoRT.anchorMin = new Vector2(0.35f, 0.62f);
                logoRT.anchorMax = new Vector2(0.65f, 0.90f);
                logoRT.offsetMin = Vector2.zero;
                logoRT.offsetMax = Vector2.zero;
                var logoImg = UIFactory.AddImage(logoRT.gameObject, Color.white);
                logoImg.sprite = engine.titleLogo;
                logoImg.preserveAspect = true;
            }

            // Buttons (theme-driven layout)
            var column = UIFactory.Rect("Buttons", root.transform);
            column.anchorMin = theme != null ? theme.titleMenuAnchorMin : new Vector2(0.38f, 0.16f);
            column.anchorMax = theme != null ? theme.titleMenuAnchorMax : new Vector2(0.62f, 0.54f);
            column.offsetMin = Vector2.zero;
            column.offsetMax = Vector2.zero;
            var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = theme != null ? theme.titleButtonSpacing : 16f;

            float btnH = theme != null ? theme.titleButtonHeight : 66f;
            int btnFont = theme != null ? theme.titleButtonFontSize : 28;

            AddMenuButton(column, VNLoc.T("title.newgame"), btnH, btnFont, engine.StartNewGame);
            AddMenuButton(column, VNLoc.T("title.load"), btnH, btnFont, engine.OpenLoadPanel);
            AddMenuButton(column, VNLoc.T("title.gallery"), btnH, btnFont, engine.OpenGallery);
            AddMenuButton(column, VNLoc.T("title.settings"), btnH, btnFont, engine.OpenSettings);
            AddMenuButton(column, VNLoc.T("title.quit"), btnH, btnFont, engine.QuitGame);

            // Footer
            if (theme == null || theme.showTitleFooter)
            {
                var footer = UIFactory.Text(root.transform, "Footer", VNLoc.T("title.footer"), 18,
                    TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.35f));
                var frt = (RectTransform)footer.transform;
                frt.anchorMin = new Vector2(0.4f, 0.02f);
                frt.anchorMax = new Vector2(0.6f, 0.06f);
                frt.offsetMin = Vector2.zero;
                frt.offsetMax = Vector2.zero;
            }

            root.SetActive(false);
        }

        void AddMenuButton(RectTransform column, string label, float height, int fontSize,
            UnityEngine.Events.UnityAction action)
        {
            var btn = UIFactory.Button(column, label, label, fontSize, action);
            UIFactory.Layout(btn.gameObject, 0f, height);
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
