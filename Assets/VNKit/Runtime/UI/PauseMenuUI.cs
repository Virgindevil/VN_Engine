using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Classic pause menu: a dimmed screen with a plain centered box of buttons
    /// (Continue / Save / Load / Settings / Title). Used as the in-game menu while
    /// the phone menu is OFF — i.e. before the script runs @phoneOn, and again
    /// after @phoneOff.
    /// </summary>
    public class PauseMenuUI
    {
        readonly GameObject root;
        readonly VisualNovelEngine engine;
        bool dialogueWasVisible;

        public PauseMenuUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            root = UIFactory.Rect("VNKit.PauseMenu", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.55f);

            Button closeButton;
            var win = UIFactory.Window(root.transform, VNLoc.T("menu.title"),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), out closeButton);
            win.sizeDelta = new Vector2(380f, 386f);
            closeButton.onClick.AddListener(Hide);

            var col = UIFactory.Rect("Buttons", win);
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(1f, 1f);
            col.offsetMin = new Vector2(28f, 24f);
            col.offsetMax = new Vector2(-28f, -72f); // below the window header
            var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;

            AddButton(col, VNLoc.T("menu.resume"), Hide);
            // The pause menu itself is a blocking modal, so the engine's Open*
            // gates would refuse to open a panel on top of it — close the menu
            // first, then open the target panel.
            AddButton(col, VNLoc.T("menu.save"), delegate { Hide(); engine.OpenSavePanel(); });
            AddButton(col, VNLoc.T("menu.load"), delegate { Hide(); engine.OpenLoadPanel(); });
            AddButton(col, VNLoc.T("qm.settings"), delegate { Hide(); engine.OpenSettings(); });
            AddButton(col, VNLoc.T("qm.title"), delegate { Hide(); engine.ReturnToTitle(); });

            root.SetActive(false);
        }

        void AddButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var b = UIFactory.Button(parent, "Btn." + label, label, 24, action);
            UIFactory.Layout(b.gameObject, 0f, 48f);
        }

        public bool IsOpen { get { return root.activeSelf; } }

        public void Show()
        {
            // The menu covers the scene like a real pause screen: the dialogue
            // panel must not peek out from underneath it (no Ren'Py-style stack).
            dialogueWasVisible = engine.Dialogue != null && engine.Dialogue.IsOpen;
            if (dialogueWasVisible) engine.Dialogue.SetHudVisible(false);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            root.SetActive(false);
            if (dialogueWasVisible)
            {
                dialogueWasVisible = false;
                if (engine.Dialogue != null && !engine.HudHidden) engine.Dialogue.SetHudVisible(true);
            }
        }
    }
}
