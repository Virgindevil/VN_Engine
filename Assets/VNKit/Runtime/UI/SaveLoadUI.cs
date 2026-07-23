using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Save / load slot grid with thumbnails, timestamps and line previews.</summary>
    public class SaveLoadUI
    {
        public enum Mode { Save, Load }

        public bool IsOpen { get { return root.activeSelf; } }
        public GameObject Root { get { return root; } }

        readonly GameObject root;
        readonly VisualNovelEngine engine;
        readonly RectTransform grid;
        readonly Text titleText;
        Mode mode;

        public SaveLoadUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            root = UIFactory.Rect("VNKit.SaveLoad", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.6f);

            Button closeBtn;
            var win = UIFactory.Window(root.transform, "Save",
                new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.93f), out closeBtn);
            closeBtn.onClick.AddListener(Hide);

            // Window() created the header text named "Title" — grab it for mode switching.
            titleText = win.GetComponent<RectTransform>().Find("Header/Title").GetComponent<Text>();

            grid = UIFactory.Rect("Grid", win);
            grid.anchorMin = new Vector2(0.03f, 0.03f);
            grid.anchorMax = new Vector2(0.97f, 0.88f);
            grid.offsetMin = Vector2.zero;
            grid.offsetMax = Vector2.zero;

            var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.spacing = new Vector2(14f, 14f);
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.cellSize = new Vector2(455f, 172f);

            root.SetActive(false);
        }

        public void Show(Mode mode)
        {
            this.mode = mode;
            titleText.text = mode == Mode.Save ? "Save Game" : "Load Game";
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Refresh();
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        public void Refresh()
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
                Object.Destroy(grid.GetChild(i).gameObject);

            for (int slot = 1; slot <= SaveLoadManager.SlotCount; slot++)
                BuildSlot(slot);
        }

        void BuildSlot(int slot)
        {
            int captured = slot;
            bool has = engine.Storage.HasSave(slot);
            VNSaveData meta = has ? engine.Storage.Load(slot) : null;

            var btn = UIFactory.Button(grid, "Slot" + slot, "", 20, delegate { OnSlotClicked(captured); });
            var brt = (RectTransform)btn.transform;

            // Thumbnail (left side)
            var thumbRT = UIFactory.Rect("Thumb", brt);
            thumbRT.anchorMin = new Vector2(0.03f, 0.08f);
            thumbRT.anchorMax = new Vector2(0.32f, 0.92f);
            thumbRT.offsetMin = Vector2.zero;
            thumbRT.offsetMax = Vector2.zero;
            var raw = thumbRT.gameObject.AddComponent<RawImage>();
            raw.color = new Color(0.05f, 0.06f, 0.09f, 1f);
            if (has)
            {
                var tex = engine.Storage.LoadThumbnail(slot);
                if (tex != null)
                {
                    raw.texture = tex;
                    raw.color = Color.white;
                }
            }

            // Slot label
            var slotLabel = UIFactory.Text(brt, "SlotLabel", "Slot " + slot, 24,
                TextAnchor.MiddleLeft, Color.white);
            var slrt = (RectTransform)slotLabel.transform;
            slrt.anchorMin = new Vector2(0.36f, 0.68f);
            slrt.anchorMax = new Vector2(0.97f, 0.95f);
            slrt.offsetMin = Vector2.zero;
            slrt.offsetMax = Vector2.zero;

            // Info text
            string info;
            if (meta != null)
            {
                string preview = (meta.preview ?? "").Replace("\n", " ");
                if (preview.Length > 46) preview = preview.Substring(0, 46) + "...";
                info = meta.timestamp + "\n" + preview;
            }
            else info = has ? "..." : "- empty -";

            var infoText = UIFactory.Text(brt, "Info", info, 18, TextAnchor.UpperLeft,
                new Color(1f, 1f, 1f, has ? 0.85f : 0.4f));
            var irt = (RectTransform)infoText.transform;
            irt.anchorMin = new Vector2(0.36f, 0.08f);
            irt.anchorMax = new Vector2(0.97f, 0.66f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;

            if (mode == Mode.Load && !has)
            {
                btn.interactable = false;
            }
        }

        void OnSlotClicked(int slot)
        {
            if (mode == Mode.Save)
            {
                if (engine.SaveGame(slot)) Refresh();
            }
            else
            {
                engine.LoadGame(slot); // on success the engine closes this panel
            }
        }
    }
}
