using System.IO;
using UnityEditor;
using UnityEngine;

namespace VNKit.EditorTools
{
    public static class VNKitMenu
    {
        [MenuItem("GameObject/VNKit/Visual Novel Engine", false, 10)]
        public static void CreateEngine(MenuCommand cmd)
        {
            var go = new GameObject("VNKit Engine");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create VNKit Engine");
            go.AddComponent<VisualNovelEngine>();
            Selection.activeObject = go;
        }

        [MenuItem("Tools/VNKit/Create Content Folders")]
        public static void CreateFolders()
        {
            // Content lives outside Resources — mark these as Addressable in the Addressables Groups window.
            string[] paths =
            {
                "Assets/VNContent/Backgrounds",
                "Assets/VNContent/Characters",
                "Assets/VNContent/Audio/BGM",
                "Assets/VNContent/Audio/SFX",
                "Assets/VNContent/Audio/Voice",
                "Assets/VNScripts"
            };
            foreach (var p in paths) Directory.CreateDirectory(p);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("VNKit",
                "Created content folders:\n\n" + string.Join("\n", paths) +
                "\n\nNext steps:\n" +
                "1. Window → Asset Management → Addressables → Groups\n" +
                "2. Mark each asset Addressable\n" +
                "3. Set Address to e.g. VN/Backgrounds/Campus\n" +
                "   (prefix = engine Resources Root, default \"VN\")",
                "OK");
        }

        [MenuItem("Tools/VNKit/About VNKit")]
        public static void About()
        {
            EditorUtility.DisplayDialog("VNKit",
                "VNKit - lightweight visual novel engine.\n\n" +
                "All content (backgrounds, characters, audio) is loaded\n" +
                "asynchronously via Unity Addressables.\n" +
                "A boot loading screen initializes Addressables and can\n" +
                "preload a list of addresses you specify on the engine.\n\n" +
                "Quick start:\n" +
                "1. Install package: com.unity.addressables\n" +
                "2. GameObject > VNKit > Visual Novel Engine\n" +
                "3. Tools > VNKit > Create Content Folders\n" +
                "4. Mark art/audio as Addressable with keys:\n" +
                "   VN/Backgrounds/Name\n" +
                "   VN/Characters/Name/Appearance\n" +
                "   VN/Audio/BGM|SFX|Voice/Name\n" +
                "5. Assign a .vns Start Script and press Play\n\n" +
                "Tip: enable 'Use Placeholder Graphics' to prototype without art.\n" +
                "For itch.io / WebGL / mobile: mark groups Remote or use\n" +
                "local bundles — Addressables keeps the initial download small.",
                "OK");
        }
    }
}