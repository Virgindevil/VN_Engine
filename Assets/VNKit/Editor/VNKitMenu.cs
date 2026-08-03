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

        [MenuItem("Tools/VNKit/Create Resource Folders")]
        public static void CreateFolders()
        {
            string[] paths =
            {
                "Assets/Resources/VN/Backgrounds",
                "Assets/Resources/VN/Characters",
                "Assets/Resources/VN/Audio/BGM",
                "Assets/Resources/VN/Audio/SFX",
                "Assets/Resources/VN/Audio/Voice",
                "Assets/VNScripts"
            };
            foreach (var p in paths) Directory.CreateDirectory(p);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("VNKit", "Created folders:\n\n" + string.Join("\n", paths), "OK");
        }

        [MenuItem("Tools/VNKit/About VNKit")]
        public static void About()
        {
            EditorUtility.DisplayDialog("VNKit",
                "VNKit - lightweight visual novel engine.\n\n" +
                "Quick start:\n" +
                "1. GameObject > VNKit > Visual Novel Engine\n" +
                "2. Write a .vns script (see Assets/VNKit/Demo/Scripts)\n" +
                "3. Assign it as the engine's Start Script\n" +
                "4. Press Play\n\n" +
                "Tip: enable 'Use Placeholder Graphics' to prototype without any art.",
                "OK");
        }
    }
}
