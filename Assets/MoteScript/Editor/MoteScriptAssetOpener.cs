#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using UnityEditor;
using UnityEngine;

namespace MoteScript.Editor
{
	internal static class MoteScriptAssetChangeEvents
	{
		public static event Action<string, string> Changed;

		public static void Notify(string oldPath, string newPath)
		{
			Changed?.Invoke(oldPath, newPath);
		}
	}

	internal sealed class MoteScriptAssetPostprocessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			for (int i = 0; i < importedAssets.Length; ++i)
			{
				NotifyIfMote(importedAssets[i], importedAssets[i]);
			}
			for (int i = 0; i < deletedAssets.Length; ++i)
			{
				NotifyIfMote(deletedAssets[i], null);
			}
			for (int i = 0; i < movedAssets.Length; ++i)
			{
				NotifyIfMote(movedFromAssetPaths[i], movedAssets[i]);
			}
		}

		private static void NotifyIfMote(string oldPath, string newPath)
		{
			string path = newPath ?? oldPath;
			if (!string.IsNullOrEmpty(path)
				&& path.EndsWith(
					".mote",
					StringComparison.OrdinalIgnoreCase))
			{
				MoteScriptAssetChangeEvents.Notify(oldPath, newPath);
			}
		}
	}

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
