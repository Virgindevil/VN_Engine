using UnityEngine;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// UI customization without touching code (Naninovel-style "UI customization"):
    /// assign a theme asset to VisualNovelEngine.uiTheme and every panel, button and
    /// the title menu follow it — colors, TMP font, and the position / size / spacing
    /// of the main-menu buttons. Create via: Assets → Create → VNKit → UI Theme.
    /// Leave uiTheme empty to keep the built-in default look.
    /// </summary>
    [CreateAssetMenu(fileName = "VNUITheme", menuName = "VNKit/UI Theme")]
    public class VNUITheme : ScriptableObject
    {
        [Header("Fonts")]
        [Tooltip("Overrides the default TMP font everywhere.")]
        public TMP_FontAsset font;

        [Header("Colors")]
        public Color panelColor = new Color(0.09f, 0.10f, 0.14f, 0.98f);
        public Color buttonColor = new Color(0.16f, 0.17f, 0.23f, 0.96f);
        public Color accentColor = new Color(0.85f, 0.45f, 0.65f, 1f);
        public Color textColor = Color.white;
        public Color dialoguePanelColor = new Color(0f, 0f, 0f, 0.72f);

        [Header("Title Screen")]
        [Tooltip("Fractional anchors of the main-menu button column. Move the whole menu anywhere.")]
        public Vector2 titleMenuAnchorMin = new Vector2(0.38f, 0.16f);
        public Vector2 titleMenuAnchorMax = new Vector2(0.62f, 0.54f);
        public float titleButtonHeight = 66f;
        public float titleButtonSpacing = 16f;
        public int titleButtonFontSize = 28;
        public int titleFontSize = 76;
        public bool showTitleFooter = true;

        [Header("Dialogue")]
        public int messageFontSize = 30;
        public int nameFontSize = 30;

        [Header("Quick Menu")]
        [Tooltip("Width of one quick-menu button in reference pixels.")]
        public float quickMenuButtonWidth = 112f;
    }
}
