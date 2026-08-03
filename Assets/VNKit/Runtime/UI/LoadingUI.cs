using UnityEngine;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// Simple full-screen loading overlay with a status line and a progress bar.
    /// Shown during boot (Addressables init + optional preload) and when loading a save.
    /// </summary>
    public class LoadingUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly TextMeshProUGUI statusText;
        readonly RectTransform barFillRT;
        const float barMaxWidth = 960f;

        public LoadingUI(Transform parent, string gameTitle)
        {
            root = UIFactory.Rect("VNKit.Loading", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);

            // Opaque background blocks the scene and swallows clicks.
            var bg = UIFactory.AddImage(root, new Color(0.05f, 0.06f, 0.10f, 1f));
            bg.raycastTarget = true;

            var title = UIFactory.Text(root.transform, "Title", gameTitle ?? "", 44,
                TextAnchor.MiddleCenter, Color.white);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0.2f, 0.56f);
            trt.anchorMax = new Vector2(0.8f, 0.66f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // Progress bar: dark slot + accent fill (fixed pixel width, centered).
            var barBG = UIFactory.Rect("BarBG", root.transform);
            barBG.anchorMin = barBG.anchorMax = new Vector2(0.5f, 0.46f);
            barBG.pivot = new Vector2(0.5f, 0.5f);
            barBG.sizeDelta = new Vector2(barMaxWidth, 18f);
            barBG.anchoredPosition = Vector2.zero;
            var bgImg = UIFactory.AddImage(barBG.gameObject, UIFactory.DarkSlotColor);
            bgImg.sprite = UIFactory.UISprite;
            bgImg.type = UnityEngine.UI.Image.Type.Sliced;

            var fill = UIFactory.Rect("Fill", barBG);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(0f, -3f);
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            var fillImg = UIFactory.AddImage(fill.gameObject, UIFactory.AccentColor);
            fillImg.sprite = UIFactory.UISprite;
            fillImg.type = UnityEngine.UI.Image.Type.Sliced;
            barFillRT = fill;

            statusText = UIFactory.Text(root.transform, "Status", "", 22,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));
            var srt = (RectTransform)statusText.transform;
            srt.anchorMin = new Vector2(0.15f, 0.36f);
            srt.anchorMax = new Vector2(0.85f, 0.43f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            root.SetActive(false);
        }

        public void Show(string status)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            SetProgress(0f, status);
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        /// <summary>progress 0..1 + optional status line.</summary>
        public void SetProgress(float progress, string status)
        {
            progress = Mathf.Clamp01(progress);
            if (barFillRT != null)
                barFillRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    (barMaxWidth - 6f) * progress);
            if (statusText != null && status != null) statusText.text = status;
        }
    }
}
