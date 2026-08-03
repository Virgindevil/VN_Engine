using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace VNKit.EditorTools
{
    /// <summary>Imports .vns files as TextAssets so they can be assigned to the engine in the Inspector.</summary>
    [ScriptedImporter(1, "vns")]
    public class VNScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var asset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }
    }
}
