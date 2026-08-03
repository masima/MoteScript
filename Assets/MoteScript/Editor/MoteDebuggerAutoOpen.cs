#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using UnityEditor;
using UnityEngine;

namespace MoteScript.Editor
{
	[InitializeOnLoad]
	internal static class MoteDebuggerAutoOpen
	{
		private static IMoteDebugTarget _pendingTarget;
		private static bool _scheduled;

		static MoteDebuggerAutoOpen()
		{
			MoteDebugTargetRegistry.TargetFaulted += OnTargetFaulted;
		}

		private static void OnTargetFaulted(IMoteDebugTarget target)
		{
			if (Application.isBatchMode) return;
			MoteDebuggerWindow openWindow =
				MoteDebuggerWindow.FindOpenWindow();
			if (openWindow is not null && openWindow.SelectedTargetIsFaulted)
			{
				openWindow.Repaint();
				return;
			}
			if (_scheduled) return;
			_pendingTarget = target;
			_scheduled = true;
			EditorApplication.delayCall += OpenPendingTarget;
		}

		private static void OpenPendingTarget()
		{
			_scheduled = false;
			IMoteDebugTarget target = _pendingTarget;
			_pendingTarget = null;
			if (target is null) return;
			MoteDebuggerWindow.OpenTarget(target);
		}
	}
}
#endif
