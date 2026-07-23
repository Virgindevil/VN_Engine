using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Scrollable history of previously displayed lines.</summary>
    public class BacklogUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly RectTransform content;
        readonly ScrollRect scroll;

        public BacklogUI(Transform parent, VisualNovelEngine engine)
        {
            root = UIFactory.Rect("VNKit.Backlog", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.6f);

            Button closeBtn;
            var win = UIFactory.Window(root.transform, "Backlog",
                new Vector2(0.15f, 0.10f), new Vector2(0.85f, 0.90f), out closeBtn);
            closeBtn.onClick.AddListener(Hide);

            scroll = UIFactory.ScrollView(win, "Scroll", out content);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = new Vector2(0.02f, 0.02f);
            srt.anchorMax = new Vector2(0.98f, 0.88f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            root.SetActive(false);
        }

        public void Show(IReadOnlyList<VNBacklogEntry> entries)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();

            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);

            if (entries == null || entries.Count == 0)
            {
                var empty = UIFactory.Text(content, "Empty", "Nothing yet.", 26,
                    TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.5f));
                UIFactory.Layout(empty.gameObject, 0f, 80f);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    string body = string.IsNullOrEmpty(e.speaker)
                        ? e.text
                        : "<b>" + e.speaker + "</b>   " + e.text;
                    var t = UIFactory.Text(content, "Entry" + i, body, 26, TextAnchor.UpperLeft, Color.white);
                    t.lineSpacing = 1.15f;
                }
            }

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f; // jump to the newest line
        }

        public void Hide()
        {
            root.SetActive(false);
        }
    }
}
