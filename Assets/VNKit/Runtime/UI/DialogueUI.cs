using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Bottom dialogue panel: name plate, typewriter text, blinking continue marker.</summary>
    public class DialogueUI
    {
        public bool IsTyping { get; private set; }
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly GameObject namePlate;
        readonly Text nameText;
        readonly Text messageText;
        readonly Text continueIcon;
        readonly VNRunner runner;
        readonly VisualNovelEngine engine;

        Coroutine typing;
        Action onComplete;
        RichTextReveal reveal;

        public DialogueUI(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            var rootRT = UIFactory.Rect("VNKit.Dialogue", parent);
            UIFactory.Stretch(rootRT);
            root = rootRT.gameObject;
            runner = root.AddComponent<VNRunner>();

            // Bottom panel (clickable to advance)
            var panel = UIFactory.Rect("Panel", root.transform);
            UIFactory.Anchor(panel, new Vector2(0f, 0f), new Vector2(1f, 0.27f));
            var panelImg = UIFactory.AddImage(panel.gameObject, engine.dialoguePanelColor);
            panelImg.raycastTarget = true;
            var advance = panel.gameObject.AddComponent<Button>();
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(delegate { if (engine.Player != null) engine.Player.Advance(); });

            // Message text
            messageText = UIFactory.Text(panel, "Message", "", 30, TextAnchor.UpperLeft, Color.white);
            var mrt = (RectTransform)messageText.transform;
            mrt.anchorMin = new Vector2(0.03f, 0.10f);
            mrt.anchorMax = new Vector2(0.97f, 0.92f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            messageText.lineSpacing = 1.2f;
            UIFactory.AddOutline(messageText, new Color(0f, 0f, 0f, 0.85f), 1.5f);

            // Name plate
            namePlate = UIFactory.Rect("NamePlate", root.transform).gameObject;
            var nprt = (RectTransform)namePlate.transform;
            UIFactory.Anchor(nprt, new Vector2(0.015f, 0.272f), new Vector2(0.30f, 0.35f));
            var plateImg = UIFactory.AddImage(namePlate, engine.accentColor);
            plateImg.sprite = UIFactory.UISprite;
            plateImg.type = Image.Type.Sliced;
            nameText = UIFactory.Text(namePlate.transform, "Name", "", 30, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch((RectTransform)nameText.transform);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.AddOutline(nameText, new Color(0f, 0f, 0f, 0.6f), 1f);

            // Continue indicator
            continueIcon = UIFactory.Text(panel, "Continue", "»", 30, TextAnchor.MiddleRight, engine.accentColor);
            var crt = (RectTransform)continueIcon.transform;
            crt.anchorMin = new Vector2(0.95f, 0.02f);
            crt.anchorMax = new Vector2(0.99f, 0.18f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            continueIcon.fontStyle = FontStyle.Bold;
            continueIcon.gameObject.SetActive(false);
            runner.StartCoroutine(BlinkRoutine());

            root.SetActive(false);
        }

        IEnumerator BlinkRoutine()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                if (continueIcon.gameObject.activeSelf)
                {
                    var c = continueIcon.color;
                    c.a = 0.3f + 0.7f * Mathf.PingPong(t * 1.6f, 1f);
                    continueIcon.color = c;
                }
                yield return null;
            }
        }

        public void PlayLine(string speaker, string text, Action onDone)
        {
            root.SetActive(true);
            onComplete = onDone;
            bool hasName = !string.IsNullOrEmpty(speaker);
            namePlate.SetActive(hasName);
            if (hasName) nameText.text = speaker;
            if (typing != null) runner.StopCoroutine(typing);
            typing = runner.StartCoroutine(TypeRoutine(text ?? ""));
        }

        IEnumerator TypeRoutine(string full)
        {
            IsTyping = true;
            continueIcon.gameObject.SetActive(false);
            reveal = new RichTextReveal(full);
            float speed = Mathf.Max(5f, engine.Settings.textSpeed);
            float acc = 0f;
            int shown = 0;
            messageText.text = "";
            while (shown < reveal.Total)
            {
                acc += Time.deltaTime * speed;
                int n = Mathf.Min(reveal.Total, Mathf.FloorToInt(acc));
                if (n != shown)
                {
                    shown = n;
                    messageText.text = reveal.Get(shown);
                }
                yield return null;
            }
            FinishLine();
        }

        public void CompleteLine()
        {
            if (!IsTyping) return;
            if (typing != null) runner.StopCoroutine(typing);
            typing = null;
            if (reveal != null) messageText.text = reveal.Get(reveal.Total);
            FinishLine();
        }

        void FinishLine()
        {
            IsTyping = false;
            typing = null;
            continueIcon.gameObject.SetActive(true);
            var cb = onComplete;
            onComplete = null;
            if (cb != null) cb();
        }

        public void Hide()
        {
            if (typing != null) runner.StopCoroutine(typing);
            typing = null;
            IsTyping = false;
            onComplete = null;
            root.SetActive(false);
        }

        public void SetHudVisible(bool visible)
        {
            root.SetActive(visible);
        }
    }
}
