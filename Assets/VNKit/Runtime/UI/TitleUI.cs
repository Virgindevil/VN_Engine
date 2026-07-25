using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>Simple title screen: game name, New Game / Load / Settings / Quit.</summary>
    public class TitleUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;

        public TitleUI(Transform parent, VisualNovelEngine engine)
        {
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
            var title = UIFactory.Text(root.transform, "Title", engine.gameTitle, 76,
                TextAnchor.MiddleCenter, Color.white);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0.15f, 0.58f);
            trt.anchorMax = new Vector2(0.85f, 0.80f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            title.fontStyle = FontStyles.Bold;
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.8f), 2f);

            // Buttons
            var column = UIFactory.Rect("Buttons", root.transform);
            column.anchorMin = new Vector2(0.38f, 0.16f);
            column.anchorMax = new Vector2(0.62f, 0.54f);
            column.offsetMin = Vector2.zero;
            column.offsetMax = Vector2.zero;
            var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 16f;

            AddMenuButton(column, "New Game", engine.StartNewGame);
            AddMenuButton(column, "Load", engine.OpenLoadPanel);
            AddMenuButton(column, "Settings", engine.OpenSettings);
            AddMenuButton(column, "Quit", engine.QuitGame);

            // Footer
            var footer = UIFactory.Text(root.transform, "Footer", "Powered by VNKit", 18,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.35f));
            var frt = (RectTransform)footer.transform;
            frt.anchorMin = new Vector2(0.4f, 0.02f);
            frt.anchorMax = new Vector2(0.6f, 0.06f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            root.SetActive(false);
        }

        void AddMenuButton(RectTransform column, string label, UnityEngine.Events.UnityAction action)
        {
            var btn = UIFactory.Button(column, label, label, 28, action);
            UIFactory.Layout(btn.gameObject, 0f, 66f);
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