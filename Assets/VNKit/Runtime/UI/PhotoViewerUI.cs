using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Full-screen viewer for phone photo attachments: tapping a photo bubble in a chat
    /// opens the picture over everything; a click or Esc closes it. Modal — the game
    /// is paused while it is open.
    /// </summary>
    public class PhotoViewerUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly RawImage image;
        readonly AspectRatioFitter fit;

        public PhotoViewerUI(Transform parent)
        {
            root = UIFactory.Rect("VNKit.PhotoViewer", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            var dim = UIFactory.DimBackground(root, 0.92f);
            var btn = root.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Hide);

            var imgRT = UIFactory.Rect("Image", root.transform);
            imgRT.anchorMin = new Vector2(0.06f, 0.05f);
            imgRT.anchorMax = new Vector2(0.94f, 0.92f);
            imgRT.offsetMin = Vector2.zero;
            imgRT.offsetMax = Vector2.zero;
            image = imgRT.gameObject.AddComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            fit = imgRT.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var hint = UIFactory.Text(root.transform, "Hint", VNLoc.T("gallery.hint"), 20,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            var hrt = (RectTransform)hint.transform;
            hrt.anchorMin = new Vector2(0.4f, 0.015f);
            hrt.anchorMax = new Vector2(0.6f, 0.05f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;

            root.SetActive(false);
        }

        public void Show(Sprite s)
        {
            if (s == null) return;
            image.texture = s.texture;
            fit.aspectRatio = s.rect.width / s.rect.height;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            root.SetActive(false);
        }
    }
}
