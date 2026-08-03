using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// CG gallery (Naninovel-style): grid of every CG listed in VisualNovelEngine.galleryCgs.
    /// Unlocked entries (seen in-game via @cg) show a thumbnail and open full-screen;
    /// locked ones show "???". Unlocks persist in PlayerPrefs across sessions.
    /// </summary>
    public class CGGalleryUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly VisualNovelEngine engine;
        readonly RectTransform grid;
        readonly GameObject viewer;
        readonly RawImage viewerImage;

        public CGGalleryUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            root = UIFactory.Rect("VNKit.Gallery", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.75f);

            Button closeBtn;
            var win = UIFactory.Window(root.transform, VNLoc.T("gallery.title"),
                new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.93f), out closeBtn);
            closeBtn.onClick.AddListener(Hide);

            grid = UIFactory.Rect("Grid", win);
            grid.anchorMin = new Vector2(0.03f, 0.03f);
            grid.anchorMax = new Vector2(0.97f, 0.88f);
            grid.offsetMin = Vector2.zero;
            grid.offsetMax = Vector2.zero;
            var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.spacing = new Vector2(16f, 16f);
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.cellSize = new Vector2(480f, 270f);

            // Full-screen viewer (hidden until an unlocked CG is clicked)
            viewer = UIFactory.Rect("Viewer", root.transform).gameObject;
            UIFactory.Stretch((RectTransform)viewer.transform);
            var vbg = UIFactory.AddImage(viewer, new Color(0f, 0f, 0f, 0.92f));
            vbg.raycastTarget = true;
            var vbtn = viewer.AddComponent<Button>();
            vbtn.transition = Selectable.Transition.None;
            vbtn.onClick.AddListener(delegate { viewer.SetActive(false); });

            var imgRT = UIFactory.Rect("Image", viewer.transform);
            imgRT.anchorMin = new Vector2(0.1f, 0.06f);
            imgRT.anchorMax = new Vector2(0.9f, 0.90f);
            imgRT.offsetMin = Vector2.zero;
            imgRT.offsetMax = Vector2.zero;
            viewerImage = imgRT.gameObject.AddComponent<RawImage>();
            viewerImage.color = Color.white;
            var vfit = imgRT.gameObject.AddComponent<AspectRatioFitter>();
            vfit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            vfit.aspectRatio = 16f / 9f;

            var hint = UIFactory.Text(viewer.transform, "Hint", VNLoc.T("gallery.hint"), 20,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            var hrt = (RectTransform)hint.transform;
            hrt.anchorMin = new Vector2(0.4f, 0.015f);
            hrt.anchorMax = new Vector2(0.6f, 0.05f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;

            viewer.SetActive(false);
            root.SetActive(false);
        }

        public void Show()
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            viewer.SetActive(false);
            Refresh();
        }

        public void Hide()
        {
            viewer.SetActive(false);
            root.SetActive(false);
        }

        void Refresh()
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
                Object.Destroy(grid.GetChild(i).gameObject);

            var catalog = engine.galleryCgs;
            if (catalog == null || catalog.Count == 0)
            {
                var empty = UIFactory.Text(grid, "Empty", VNLoc.T("backlog.empty"), 26,
                    TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.5f));
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                string cgName = catalog[i];
                bool unlocked = engine.IsCgUnlocked(cgName);

                var btn = UIFactory.Button(grid, "CG." + cgName, unlocked ? "" : VNLoc.T("gallery.locked"),
                    32, null);
                var brt = (RectTransform)btn.transform;

                var thumbRT = UIFactory.Rect("Thumb", brt);
                UIFactory.Stretch(thumbRT);
                var raw = thumbRT.gameObject.AddComponent<RawImage>();
                raw.color = unlocked ? Color.white : new Color(0.05f, 0.06f, 0.09f, 1f);

                // Label sits above the thumbnail
                var label = UIFactory.Text(brt, "Label", unlocked ? cgName : VNLoc.T("gallery.locked"),
                    26, TextAnchor.MiddleCenter, unlocked ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.45f));
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0.22f);
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                label.raycastTarget = false;

                if (unlocked)
                {
                    btn.onClick.AddListener(delegate { engine.OpenCgViewer(cgName); });
                    // Async thumbnail load
                    engine.StartCoroutine(LoadThumb(cgName, raw));
                }
                else
                {
                    btn.interactable = false;
                    // keep the "???" text visible: remove the empty label from UIFactory.Button
                    var btnLabel = brt.Find("Label");
                    if (btnLabel != null) btnLabel.gameObject.SetActive(false);
                }
            }
        }

        IEnumerator LoadThumb(string cgName, RawImage raw)
        {
            Sprite s = null;
            yield return engine.LoadCgAsync(cgName, x => s = x);
            if (s != null && raw != null)
            {
                raw.texture = s.texture;
                var fit = raw.GetComponent<AspectRatioFitter>();
                if (fit == null) fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = s.rect.width / s.rect.height;
            }
        }

        /// <summary>Show one CG full-screen (engine routes unlock-checked clicks here).</summary>
        public void ShowViewer(Sprite s)
        {
            if (s == null) return;
            viewerImage.texture = s.texture;
            var fit = viewerImage.GetComponent<AspectRatioFitter>();
            if (fit != null) fit.aspectRatio = s.rect.width / s.rect.height;
            viewer.SetActive(true);
            viewer.transform.SetAsLastSibling();
        }
    }
}
