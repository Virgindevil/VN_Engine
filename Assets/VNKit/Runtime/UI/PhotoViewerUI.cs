using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Full-screen viewer for phone photo attachments: tapping a photo bubble (or an
    /// unlocked gallery tile) opens the picture over everything. 2.12.1: the photo can
    /// be examined up close — left click toggles zoom, the wheel zooms smoothly, and
    /// dragging pans while zoomed in; right click or Esc closes the viewer.
    /// Modal — the game is paused while it is open.
    /// </summary>
    public class PhotoViewerUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly RawImage image;
        readonly RectTransform imageRT;
        readonly PhotoViewerInput input;

        internal RectTransform ImageRT { get { return imageRT; } }

        public PhotoViewerUI(Transform parent)
        {
            root = UIFactory.Rect("VNKit.PhotoViewer", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.92f);

            imageRT = UIFactory.Rect("Image", root.transform);
            imageRT.anchorMin = new Vector2(0.5f, 0.5f);
            imageRT.anchorMax = new Vector2(0.5f, 0.5f);
            imageRT.pivot = new Vector2(0.5f, 0.5f);
            image = imageRT.gameObject.AddComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;

            var hint = UIFactory.Text(root.transform, "Hint", VNLoc.T("gallery.hint"), 20,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            var hrt = (RectTransform)hint.transform;
            hrt.anchorMin = new Vector2(0.2f, 0.015f);
            hrt.anchorMax = new Vector2(0.8f, 0.05f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;

            input = root.AddComponent<PhotoViewerInput>();
            input.viewer = this;

            root.SetActive(false);
        }

        public void Show(Sprite s)
        {
            if (s == null) return;
            image.texture = s.texture;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            FitImage(s.rect.width / s.rect.height);
            input.ResetView();
        }

        /// <summary>Fit the image into the viewport (same margins the fixed viewer
        /// used), centered; zoom/pan are applied on top by PhotoViewerInput.</summary>
        void FitImage(float aspect)
        {
            var pv = ((RectTransform)root.transform).rect.size;
            float maxW = pv.x * 0.88f;
            float maxH = pv.y * 0.87f;
            float w = maxW;
            float h = w / Mathf.Max(0.01f, aspect);
            if (h > maxH) { h = maxH; w = h * aspect; }
            imageRT.sizeDelta = new Vector2(w, h);
            imageRT.anchoredPosition = Vector2.zero;
        }

        public void Hide()
        {
            root.SetActive(false);
        }
    }

    /// <summary>
    /// Input for the photo viewer: left click toggles 1x ↔ 2.5x, the scroll wheel
    /// zooms 1x–4x, dragging pans (clamped to the visible area), right click closes.
    /// </summary>
    public class PhotoViewerInput : MonoBehaviour, IPointerClickHandler, IScrollHandler, IDragHandler
    {
        public PhotoViewerUI viewer;

        const float MaxZoom = 4f;
        const float ToggleZoom = 2.5f;
        float zoom = 1f;

        public void ResetView()
        {
            zoom = 1f;
            Apply();
        }

        void Apply()
        {
            var rt = viewer != null ? viewer.ImageRT : null;
            if (rt == null) return;
            rt.localScale = new Vector3(zoom, zoom, 1f);
            ClampPan();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { if (viewer != null) viewer.Hide(); return; }
            if (e.button != PointerEventData.InputButton.Left) return;
            zoom = zoom > 1.01f ? 1f : ToggleZoom;
            Apply();
        }

        public void OnScroll(PointerEventData e)
        {
            zoom = Mathf.Clamp(zoom * (1f + e.scrollDelta.y * 0.15f), 1f, MaxZoom);
            Apply();
        }

        public void OnDrag(PointerEventData e)
        {
            if (zoom <= 1.01f) return;
            var rt = viewer != null ? viewer.ImageRT : null;
            if (rt == null) return;
            rt.anchoredPosition += e.delta;
            ClampPan();
        }

        void ClampPan()
        {
            var rt = viewer != null ? viewer.ImageRT : null;
            if (rt == null) return;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            var pv = parent.rect.size;
            var size = rt.sizeDelta * zoom;
            float mx = Mathf.Max(0f, (size.x - pv.x) * 0.5f);
            float my = Mathf.Max(0f, (size.y - pv.y) * 0.5f);
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, -my, my));
        }
    }
}
