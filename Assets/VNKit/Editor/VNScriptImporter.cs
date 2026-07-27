using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace VNKit.EditorTools
{
    /// <summary>Импортирует файлы .vns как TextAssets, чтобы их можно было назначить движку в инспекторе.</summary>
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
