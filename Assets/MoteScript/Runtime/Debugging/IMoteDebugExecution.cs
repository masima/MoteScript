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
		string ResultText { get; }
		MoteDebugFaultSnapshot FaultSnapshot { get; }

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
		bool TrySetVariableValue(int id, string value, out string error);
		IReadOnlyList<MoteDebugCallFrame> GetCallStack();
	}

	public sealed class MoteDebugFaultSnapshot
	{
		internal MoteDebugFaultSnapshot(
			System.Exception exception,
			MoteSourceSpan? location,
			IReadOnlyList<MoteDebugVariable> variables,
			IReadOnlyList<MoteDebugCallFrame> callStack)
		{
			Exception = exception;
			Location = location;
			Variables = variables ?? System.Array.Empty<MoteDebugVariable>();
			CallStack = callStack ?? System.Array.Empty<MoteDebugCallFrame>();
		}

		public System.Exception Exception { get; }
		public MoteSourceSpan? Location { get; }
		public IReadOnlyList<MoteDebugVariable> Variables { get; }
		public IReadOnlyList<MoteDebugCallFrame> CallStack { get; }
	}

	public readonly struct MoteDebugVariable
	{
		public MoteDebugVariable(string name, string type, string value)
			: this(name, type, value, null)
		{
		}

		public MoteDebugVariable(
			string name,
			string type,
			string value,
			IReadOnlyList<MoteDebugVariable> children,
			int id = 0,
			bool canEdit = false,
			string editValue = null)
		{
			Name = name;
			Type = type;
			Value = value;
			Children = children;
			Id = id;
			CanEdit = canEdit;
			EditValue = editValue;
		}

		public string Name { get; }
		public string Type { get; }
		public string Value { get; }
		public IReadOnlyList<MoteDebugVariable> Children { get; }
		public int Id { get; }
		public bool CanEdit { get; }
		public string EditValue { get; }
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
