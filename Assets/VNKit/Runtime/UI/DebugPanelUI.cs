using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// 2.12 developer debug panel (F8). Created ONLY when VisualNovelEngine.
    /// enableDebugTools is on — it is a development tool and never appears in the
    /// release UI. Shows the live variable table and phone data counts, with a few
    /// quick actions to poke the phone apps while testing a scene.
    /// </summary>
    public class DebugPanelUI
    {
        readonly VisualNovelEngine engine;
        readonly GameObject root;
        readonly RectTransform content;
        readonly ScrollRect scroll;

        public bool IsOpen { get; private set; }

        public DebugPanelUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            root = UIFactory.Rect("VNKit.DebugPanel", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            var dim = UIFactory.AddImage(root, new Color(0f, 0f, 0f, 0.6f));

            Button close;
            var win = UIFactory.Window(root.transform, VNLoc.T("debug.title"),
                new Vector2(0.08f, 0.08f), new Vector2(0.55f, 0.92f), out close);
            close.onClick.AddListener(Hide);

            scroll = UIFactory.ScrollView(win, "Scroll", out content);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(12f, 12f);
            srt.offsetMax = new Vector2(-12f, -68f);

            root.SetActive(false);
        }

        public void Show()
        {
            IsOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Rebuild();
        }

        public void Hide()
        {
            IsOpen = false;
            root.SetActive(false);
        }

        void Rebuild()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);

            // ---- phone data summary ----
            var phone = engine.Phone;
            if (phone != null)
            {
                AddLabel("<b>" + VNLoc.T("debug.phone") + "</b>");
                AddLabel(VNLoc.T("debug.counts")
                    .Replace("{notes}", phone.GetNotes().Count.ToString())
                    .Replace("{events}", phone.GetSchedule().Count.ToString())
                    .Replace("{gallery}", phone.GetGalleryItems().Count.ToString())
                    .Replace("{actions}", phone.GetActions().Count.ToString()));
                AddButton("debug.addnote", delegate
                {
                    phone.AddNote("Debug note " + (phone.GetNotes().Count + 1), null, false);
                    Rebuild();
                });
                AddButton("debug.addevent", delegate
                {
                    phone.AddScheduleEvent("18:00", "Debug event " + (phone.GetSchedule().Count + 1), null);
                    Rebuild();
                });
            }

            // ---- live variables ----
            AddLabel("<b>" + VNLoc.T("debug.vars") + "</b>");
            var entries = engine.Variables.ToEntries();
            entries.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var e in entries)
            {
                string val = e.type == (int)VNValueType.Text ? "\"" + e.text + "\""
                    : e.type == (int)VNValueType.Bool ? (e.boolean ? "true" : "false")
                    : e.number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                AddLabel(e.name + " = " + val);
            }
        }

        void AddLabel(string text)
        {
            var t = UIFactory.Text(content, "L", text, 20, TextAnchor.UpperLeft, UIFactory.TextColor);
            t.raycastTarget = false;
            t.enableWordWrapping = true;
            UIFactory.Layout(t.gameObject, 0f, 28f + 24f * (text.Length / 46));
        }

        void AddButton(string locKey, UnityEngine.Events.UnityAction action)
        {
            var b = UIFactory.Button(content, locKey, VNLoc.T(locKey), 22, action);
            UIFactory.Layout(b.gameObject, 0f, 48f);
        }
    }
}
