using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Centered stack of choice buttons.</summary>
    public class ChoiceUI
    {
        public bool IsOpen { get { return root.activeSelf; } }

        readonly GameObject root;
        readonly RectTransform content;

        public ChoiceUI(Transform parent, VisualNovelEngine engine)
        {
            root = UIFactory.Rect("VNKit.Choice", parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
            UIFactory.DimBackground(root, 0.55f);

            var win = UIFactory.Rect("Window", root.transform);
            UIFactory.Anchor(win, new Vector2(0.27f, 0.15f), new Vector2(0.73f, 0.85f));
            var vlg = win.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            content = win;

            root.SetActive(false);
        }

        public void Show(List<string> options, Action<int> onPick)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(content.GetChild(i).gameObject);

            root.SetActive(true);
            root.transform.SetAsLastSibling();

            for (int i = 0; i < options.Count; i++)
            {
                int idx = i;
                var btn = UIFactory.Button(content, "Option" + i, options[i], 28, delegate { onPick(idx); });
                UIFactory.Layout(btn.gameObject, 0f, 68f);
            }
        }

        public void Hide()
        {
            root.SetActive(false);
        }
    }
}
