#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;
using UnityEngine;
using MoteScript.Unity;

namespace MoteScript
{
	public interface IMoteDebugTarget
	{
		string DisplayName { get; }
		UnityEngine.Object Owner { get; }
		TextAsset SourceAsset { get; }
		bool IsManager { get; }
		MoteScriptLifecycleState LifecycleState { get; }
		IMoteDebugExecution Execution { get; }
	}

	public static class MoteDebugTargetRegistry
	{
		private static readonly List<IMoteDebugTarget> TargetsInternal = new();

		public static IReadOnlyList<IMoteDebugTarget> Targets => TargetsInternal;
		public static event Action<IMoteDebugTarget> TargetRegistered;
		public static event Action<IMoteDebugTarget> TargetUnregistered;
		public static event Action<IMoteDebugTarget> TargetChanged;

		internal static void Register(IMoteDebugTarget target)
		{
			if (target is null || TargetsInternal.Contains(target)) return;
			TargetsInternal.Add(target);
			TargetRegistered?.Invoke(target);
		}

		internal static void Unregister(IMoteDebugTarget target)
		{
			if (!TargetsInternal.Remove(target)) return;
			TargetUnregistered?.Invoke(target);
		}

		internal static void NotifyChanged(IMoteDebugTarget target)
		{
			TargetChanged?.Invoke(target);
		}
	}
}
#endif
