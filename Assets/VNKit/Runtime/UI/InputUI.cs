using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// Modal text-input panel for the @input script command (player name etc.).
    /// Centered window with a prompt, a single-line field and a confirm button;
    /// Enter confirms as well. The value is returned to the ScriptPlayer, which
    /// stores it in a variable — usable afterwards as {variable} in script text.
    /// </summary>
    public class InputUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly TextMeshProUGUI promptText;
        readonly TextMeshProUGUI confirmLabel;
        readonly TextMeshProUGUI placeholderText;
        readonly TMP_InputField field;
        string defaultValue = "";
        Action<string> onDone;

        public InputUI(Transform parent, VisualNovelEngine engine)
        {
            root = UIFactory.Rect("VNKit.TextInput", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.6f);

            var win = UIFactory.Rect("Window", root.transform);
            win.anchorMin = new Vector2(0.5f, 0.5f);
            win.anchorMax = new Vector2(0.5f, 0.5f);
            win.pivot = new Vector2(0.5f, 0.5f);
            win.sizeDelta = new Vector2(720f, 380f);
            win.anchoredPosition = Vector2.zero;
            var bg = UIFactory.AddImage(win.gameObject, UIFactory.PanelColor);
            bg.sprite = UIFactory.UISprite;
            bg.type = UnityEngine.UI.Image.Type.Sliced;

            promptText = UIFactory.Text(win, "Prompt", "", 32, TextAnchor.MiddleCenter, UIFactory.TextColor);
            var prt = (RectTransform)promptText.transform;
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.offsetMin = new Vector2(40f, -160f);
            prt.offsetMax = new Vector2(-40f, -30f);
            promptText.enableWordWrapping = true;

            // ---- single-line input field ----
            var fieldRT = UIFactory.Rect("Field", win);
            fieldRT.anchorMin = new Vector2(0.5f, 0.5f);
            fieldRT.anchorMax = new Vector2(0.5f, 0.5f);
            fieldRT.pivot = new Vector2(0.5f, 0.5f);
            fieldRT.sizeDelta = new Vector2(600f, 72f);
            fieldRT.anchoredPosition = new Vector2(0f, -10f);
            var fieldImg = UIFactory.AddImage(fieldRT.gameObject, UIFactory.DarkSlotColor);
            fieldImg.sprite = UIFactory.UISprite;
            fieldImg.type = UnityEngine.UI.Image.Type.Sliced;
            fieldRT.gameObject.AddComponent<RectMask2D>();

            field = fieldRT.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = fieldRT;

            var text = UIFactory.Text(fieldRT, "Text", "", 30, TextAnchor.MiddleLeft, UIFactory.TextColor);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20f, 0f);
            trt.offsetMax = new Vector2(-20f, 0f);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            field.textComponent = text;

            var ph = UIFactory.Text(fieldRT, "Placeholder", "", 30, TextAnchor.MiddleLeft,
                new Color(1f, 1f, 1f, 0.35f));
            var phrt = (RectTransform)ph.transform;
            phrt.anchorMin = Vector2.zero;
            phrt.anchorMax = Vector2.one;
            phrt.offsetMin = new Vector2(20f, 0f);
            phrt.offsetMax = new Vector2(-20f, 0f);
            ph.fontStyle = FontStyles.Italic;
            field.placeholder = ph;
            placeholderText = ph;

            field.caretColor = Color.white;
            field.caretWidth = 2;
            field.onSubmit.AddListener(delegate (string _) { Confirm(); });

            var confirm = UIFactory.Button(win, "Confirm", "", 30, Confirm);
            var crt = (RectTransform)confirm.transform;
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(280f, 72f);
            crt.anchoredPosition = new Vector2(0f, 30f);
            confirmLabel = confirm.GetComponentInChildren<TextMeshProUGUI>();

            root.SetActive(false);
        }

        public void Show(string prompt, string def, int maxLength, Action<string> onDone)
        {
            this.onDone = onDone;
            defaultValue = def ?? "";
            promptText.text = string.IsNullOrEmpty(prompt) ? VNLoc.T("input.prompt") : prompt;
            confirmLabel.text = VNLoc.T("input.confirm");
            placeholderText.text = string.IsNullOrEmpty(def) ? VNLoc.T("input.hint") : "";
            field.characterLimit = Mathf.Max(1, maxLength);
            field.text = defaultValue;

            root.SetActive(true);
            root.transform.SetAsLastSibling();

            field.ActivateInputField();
            field.Select();
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        void Confirm()
        {
            string value = (field.text ?? "").Trim();
            if (value.Length == 0) value = defaultValue; // never store an empty name
            Hide();
            var cb = onDone;
            onDone = null;
            if (cb != null) cb(value);
        }
    }
}
