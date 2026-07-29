#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System.Collections.Generic;

namespace MoteScript
{
	public interface IMoteDebugExecution
	{
		MoteExecutionState State { get; }
		MoteDebugDocument DebugDocument { get; }
		MoteSourceSpan? CurrentLocation { get; }
		int FunctionDepth { get; }

		void Start();
		void Continue();
		void StepInto();
		void StepOver();
		void StepOut();
		void Stop();
		int SetBreakpoint(int line);
		bool RemoveBreakpoint(int line);
		bool HasBreakpoint(int line);
		IReadOnlyList<MoteDebugVariable> GetVariables();
		IReadOnlyList<MoteDebugCallFrame> GetCallStack();
	}

	public readonly struct MoteDebugVariable
	{
		public MoteDebugVariable(string name, string type, string value)
		{
			Name = name;
			Type = type;
			Value = value;
		}

		public string Name { get; }
		public string Type { get; }
		public string Value { get; }
	}

	public readonly struct MoteDebugCallFrame
	{
		public MoteDebugCallFrame(
			string functionName,
			MoteSourceSpan? callSite)
		{
			FunctionName = functionName;
			CallSite = callSite;
		}

		public string FunctionName { get; }
		public MoteSourceSpan? CallSite { get; }
	}

	public static class MoteDebugEvents
	{
		public static event System.Action<IMoteDebugExecution> ExecutionCreated;

		internal static void NotifyExecutionCreated(IMoteDebugExecution execution)
		{
			ExecutionCreated?.Invoke(execution);
		}
	}
}
#endif
