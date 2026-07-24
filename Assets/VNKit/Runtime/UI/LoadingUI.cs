using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Simple full-screen loading / preloader overlay.
    /// Used at boot while Addressables initializes and optional assets preload.
    /// Also usable for longer in-game loads if desired.
    /// </summary>
    public class LoadingUI
    {
        public bool IsOpen { get { return root != null && root.activeSelf; } }

        readonly GameObject root;
        readonly Text statusText;
        readonly Text percentText;
        readonly Image barFill;
        readonly RectTransform barFillRT;
        readonly float barMaxWidth;

        public LoadingUI(Transform parent, string title = "Loading")
        {
            root = UIFactory.Rect("VNKit.Loading", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);

            // Solid dark backdrop
            var bg = UIFactory.AddImage(root, new Color(0.04f, 0.05f, 0.08f, 1f));
            bg.raycastTarget = true;

            // Title
            var titleT = UIFactory.Text(root.transform, "Title", title, 48,
                TextAnchor.MiddleCenter, Color.white);
            var trt = (RectTransform)titleT.transform;
            trt.anchorMin = new Vector2(0.2f, 0.55f);
            trt.anchorMax = new Vector2(0.8f, 0.68f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            titleT.fontStyle = FontStyle.Bold;
            UIFactory.AddOutline(titleT, new Color(0f, 0f, 0f, 0.6f), 2f);

            // Status line
            statusText = UIFactory.Text(root.transform, "Status", "Please wait…", 22,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));
            var srt = (RectTransform)statusText.transform;
            srt.anchorMin = new Vector2(0.15f, 0.42f);
            srt.anchorMax = new Vector2(0.85f, 0.50f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            // Progress bar background
            var barBgRT = UIFactory.Rect("BarBG", root.transform);
            barBgRT.anchorMin = new Vector2(0.25f, 0.36f);
            barBgRT.anchorMax = new Vector2(0.75f, 0.40f);
            barBgRT.offsetMin = Vector2.zero;
            barBgRT.offsetMax = Vector2.zero;
            var barBg = UIFactory.AddImage(barBgRT.gameObject, new Color(0.15f, 0.16f, 0.20f, 1f));
            barBg.type = Image.Type.Sliced;
            barBg.sprite = UIFactory.UISprite;

            // Fill
            barFillRT = UIFactory.Rect("BarFill", barBgRT);
            barFillRT.anchorMin = Vector2.zero;
            barFillRT.anchorMax = new Vector2(0f, 1f);
            barFillRT.pivot = new Vector2(0f, 0.5f);
            barFillRT.offsetMin = Vector2.zero;
            barFillRT.offsetMax = Vector2.zero;
            barFill = UIFactory.AddImage(barFillRT.gameObject, new Color(0.85f, 0.45f, 0.65f, 1f));
            barFill.type = Image.Type.Sliced;
            barFill.sprite = UIFactory.UISprite;
            barFill.raycastTarget = false;

            barMaxWidth = 960f;

            // Percent text
            percentText = UIFactory.Text(root.transform, "Percent", "0%", 20,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            var prt = (RectTransform)percentText.transform;
            prt.anchorMin = new Vector2(0.4f, 0.30f);
            prt.anchorMax = new Vector2(0.6f, 0.35f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            // Footer
            var foot = UIFactory.Text(root.transform, "Footer", "VNKit", 16,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.25f));
            var frt = (RectTransform)foot.transform;
            frt.anchorMin = new Vector2(0.4f, 0.04f);
            frt.anchorMax = new Vector2(0.6f, 0.08f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            root.SetActive(false);
        }

        public void Show(string status = null)
        {
            if (status != null) statusText.text = status;
            SetProgress(0f);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        public void SetProgress(float progress, string status = null)
        {
            progress = Mathf.Clamp01(progress);
            if (status != null) statusText.text = status;

            float w = barMaxWidth;
            if (barFillRT.parent is RectTransform parentRT && parentRT.rect.width > 1f)
                w = parentRT.rect.width;

            barFillRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w * progress);
            percentText.text = Mathf.RoundToInt(progress * 100f) + "%";
        }
    }
}