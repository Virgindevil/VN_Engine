using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VNKit.EditorTools
{
    /// <summary>
    /// Keeps scripting define symbols in sync with installed packages:
    ///  - VNKIT_SPINE is added when the spine-unity runtime (SkeletonGraphic) is found,
    ///    so the Spine bridge compiles; removed when the package is uninstalled.
    ///  - Warns when Addressables is missing (VNKit 2.0 loads all content through it).
    /// </summary>
    [InitializeOnLoad]
    public static class VNKitDefines
    {
        static VNKitDefines()
        {
            SetDefine("VNKIT_SPINE", TypeExists("Spine.Unity.SkeletonGraphic"));

            if (!TypeExists("UnityEngine.AddressableAssets.Addressables"))
                Debug.LogError("[VNKit] The Addressables package is required. " +
                               "Install it via Window → Package Manager → Addressables.");
        }

        static bool TypeExists(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetType(fullName) != null) return true;
            return false;
        }

        static void SetDefine(string symbol, bool enabled)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            var list = new List<string>(defines.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
            bool has = list.Contains(symbol);
            if (enabled && !has) list.Add(symbol);
            else if (!enabled && has) list.Remove(symbol);
            else return;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list.ToArray()));
        }
    }
}
