#if MOTESCRIPT_DEBUGGER
using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace MoteScript.Editor
{
	internal static class MoteScriptAssetOpener
	{
		[OnOpenAsset]
		private static bool OnOpenAsset(int instanceId, int line)
		{
#pragma warning disable CS0618
			// OnOpenAsset still supplies the legacy integer ID.
			string path = AssetDatabase.GetAssetPath(instanceId);
#pragma warning restore CS0618
			if (!path.EndsWith(".mote", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
			if (asset == null)
			{
				return false;
			}

			MoteDebuggerWindow.OpenAsset(asset, Math.Max(1, line));
			return true;
		}
	}
}
#endif
