#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MoteScript.Editor
{
	[ScriptedImporter(1, "mote")]
	public sealed class MoteScriptImporter : ScriptedImporter
	{
		public override void OnImportAsset(AssetImportContext context)
		{
			string source = File.ReadAllText(
				Path.GetFullPath(context.assetPath));
			var asset = new TextAsset(source)
			{
				name = Path.GetFileNameWithoutExtension(context.assetPath),
			};
			context.AddObjectToAsset("MoteScript", asset);
			context.SetMainObject(asset);
		}
	}
}
#endif
