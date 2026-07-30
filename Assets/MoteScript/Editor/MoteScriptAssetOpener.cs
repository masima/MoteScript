#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using UnityEditor;
using UnityEngine;

namespace MoteScript.Editor
{
	internal static class MoteScriptAssetOpener
	{
		private const string OpenDebuggerMenu =
			"Assets/Open in MoteScript Debugger";

		[MenuItem(OpenDebuggerMenu, false, 2000)]
		private static void OpenInDebugger()
		{
			if (!TryGetSelectedMoteAsset(out TextAsset asset))
			{
				return;
			}
			MoteDebuggerWindow.OpenAsset(asset, 1);
		}

		[MenuItem(OpenDebuggerMenu, true)]
		private static bool ValidateOpenInDebugger()
		{
			return TryGetSelectedMoteAsset(out _);
		}

		private static bool TryGetSelectedMoteAsset(out TextAsset asset)
		{
			asset = Selection.activeObject as TextAsset;
			if (asset is null)
			{
				return false;
			}
			string path = AssetDatabase.GetAssetPath(asset);
			return path.EndsWith(
				".mote",
				StringComparison.OrdinalIgnoreCase);
		}
	}
}
#endif
