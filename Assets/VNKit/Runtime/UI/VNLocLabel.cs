using UnityEngine;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// 2.12.3: binds a TMP label to a VNLoc key. While the label is active it
    /// listens to VNLoc.LanguageChanged and re-translates itself immediately —
    /// switching the language in settings updates the UI without a restart.
    /// Attach through UIFactory.LocText / LocButton / LocToggle /
    /// LocalizeWindowTitle instead of writing VNLoc.T(key) into a static label.
    /// </summary>
    public class VNLocLabel : MonoBehaviour
    {
        public string key;
        TextMeshProUGUI label;

        void Awake()
        {
            label = GetComponent<TextMeshProUGUI>();
            Apply();
        }

        // Re-apply on every enable: the language may have changed while the
        // panel was inactive (hidden labels receive no events).
        void OnEnable() { VNLoc.LanguageChanged += Apply; Apply(); }
        void OnDisable() { VNLoc.LanguageChanged -= Apply; }

        public void Apply()
        {
            if (label == null) label = GetComponent<TextMeshProUGUI>();
            if (label != null && !string.IsNullOrEmpty(key)) label.text = VNLoc.T(key);
        }
    }
}
