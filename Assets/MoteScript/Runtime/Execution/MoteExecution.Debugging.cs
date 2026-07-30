#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MoteScript
{
	public sealed partial class MoteExecution<T> : IMoteDebugExecution
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly MoteDebugSession<T> _debugSession;
		private int _debugFunctionDepth;
		private readonly List<MoteDebugCallFrame> _debugCallStack = new(4);

		internal MoteExecution(
			MoteValue<T> script,
			IExecutionContext<T> context,
			MoteExecutionOptions options,
			MoteDebugSession<T> debugSession)
			: this(script, context, options)
		{
			_debugSession = debugSession
				?? throw new ArgumentNullException(nameof(debugSession));
			MoteDebugEvents.NotifyExecutionCreated(this);
		}

		public MoteDebugSession<T> DebugSession => _debugSession;
		public MoteDebugDocument DebugDocument => _debugSession?.Document;
		public MoteSourceSpan? CurrentLocation => _debugSession?.CurrentLocation;
		public int FunctionDepth => _debugFunctionDepth;
		string IMoteDebugExecution.ResultText =>
			State == MoteExecutionState.Completed
				? FormatDebugValue(Result)
				: string.Empty;

		public MoteValue<T> ContinueDebugging()
		{
			EnsurePaused();
			_debugSession.Continue();
			return ResumeCore(MoteExecutionState.Paused);
		}

		public MoteValue<T> StepInto()
		{
			if (State == MoteExecutionState.Ready)
			{
				_debugSession.StepInto();
				return Evaluate();
			}
			MoteExecutionState suspendedState = EnsureDebugSuspended();
			_debugSession.StepInto();
			return ResumeCore(suspendedState);
		}

		public MoteValue<T> StepOver()
		{
			MoteExecutionState suspendedState = EnsureDebugSuspended();
			_debugSession.StepOver(_debugFunctionDepth);
			return ResumeCore(suspendedState);
		}

		public MoteValue<T> StepOut()
		{
			MoteExecutionState suspendedState = EnsureDebugSuspended();
			_debugSession.StepOut(_debugFunctionDepth);
			return ResumeCore(suspendedState);
		}

		public void StopDebugging()
		{
			if (State != MoteExecutionState.Paused
				&& State != MoteExecutionState.Yielded)
			{
				throw new InvalidOperationException(
					"Only a paused or yielded debug execution can be stopped.");
			}

			for (int i = 0; i < _continuations.Count; ++i)
			{
				_continuations[i].Cancel(_context);
			}
			for (int i = 0; i < _resumeContinuations.Count; ++i)
			{
				_resumeContinuations[i].Cancel(_context);
			}
			_continuations.Clear();
			_resumeContinuations.Clear();
			_debugCallStack.Clear();
			_debugFunctionDepth = 0;
			State = MoteExecutionState.Stopped;
			_context.ReleaseExecution(this);
		}

		internal bool DebugCheckpoint(MoteSourceSpan location)
		{
			if (_debugSession is null
				|| !_debugSession.ShouldPause(location, _debugFunctionDepth))
			{
				return false;
			}

			State = MoteExecutionState.Paused;
			_debugSession.NotifyPaused(this, location);
			return true;
		}

		internal void DebugEnterFunction(string functionName)
		{
			++_debugFunctionDepth;
			_debugCallStack.Add(new MoteDebugCallFrame(
				string.IsNullOrEmpty(functionName)
					? "<function>"
					: functionName,
				_debugSession?.CurrentLocation));
		}

		internal void DebugExitFunction()
		{
			--_debugFunctionDepth;
			if (_debugCallStack.Count > 0)
			{
				_debugCallStack.RemoveAt(_debugCallStack.Count - 1);
			}
		}

		private void EnsurePaused()
		{
			if (State != MoteExecutionState.Paused)
			{
				throw new InvalidOperationException(
					"Debug execution can only continue while paused.");
			}
		}

		private MoteExecutionState EnsureDebugSuspended()
		{
			if (State != MoteExecutionState.Paused
				&& State != MoteExecutionState.Yielded)
			{
				throw new InvalidOperationException(
					"Debug execution can only step while paused or yielded.");
			}
			return State;
		}

		private string FormatDebugValue(MoteValue<T> value)
		{
			return value.ValueType switch
			{
				EValueType.Const => value.Value.ToString(null, CultureInfo.InvariantCulture),
				EValueType.String => value.StringValue is null
					? "null"
					: $"\"{value.StringValue}\"",
				EValueType.Array => $"Count = {value.GetArray()?.Count ?? 0}",
				EValueType.Dictionary => $"Count = {value.GetDictionary()?.Count ?? 0}",
				_ => value.StringValue,
			};
		}

		IReadOnlyList<MoteDebugVariable> IMoteDebugExecution.GetVariables()
		{
			var variables = new List<MoteDebugVariable>(_context.Count);
			var ancestors = new HashSet<object>();
			if (_context is IEnumerable<KeyValuePair<string, MoteValue<T>>> values)
			{
				foreach (KeyValuePair<string, MoteValue<T>> pair in values)
				{
					variables.Add(CreateDebugVariable(
						pair.Key,
						pair.Value,
						ancestors,
						0));
				}
				variables.Sort(
					(left, right) => string.CompareOrdinal(left.Name, right.Name));
			}
			return variables;
		}

		private MoteDebugVariable CreateDebugVariable(
			string name,
			MoteValue<T> value,
			HashSet<object> ancestors,
			int depth)
		{
			IReadOnlyList<MoteDebugVariable> children = null;
			string displayValue = FormatDebugValue(value);
			if (value.ValueType == EValueType.Dictionary)
			{
				IContext<T> dictionary = value.GetDictionary();
				bool added = dictionary is not null && ancestors.Add(dictionary);
				if (dictionary is not null && !added)
				{
					displayValue = "Circular reference";
				}
				else if (dictionary is not null && depth >= 31)
				{
					displayValue = "Maximum depth reached";
				}
				else if (dictionary
					is IEnumerable<KeyValuePair<string, MoteValue<T>>> members)
				{
					var childVariables =
						new List<MoteDebugVariable>(dictionary.Count);
					foreach (KeyValuePair<string, MoteValue<T>> member in members)
					{
						childVariables.Add(CreateDebugVariable(
							member.Key,
							member.Value,
							ancestors,
							depth + 1));
					}
					childVariables.Sort(
						(left, right) =>
							string.CompareOrdinal(left.Name, right.Name));
					children = childVariables;
				}
				if (added)
				{
					ancestors.Remove(dictionary);
				}
			}
			else if (value.ValueType == EValueType.Array)
			{
				MoteList<T> array = value.GetArray();
				bool added = array is not null && ancestors.Add(array);
				if (array is not null && !added)
				{
					displayValue = "Circular reference";
				}
				else if (array is not null && depth >= 31)
				{
					displayValue = "Maximum depth reached";
				}
				else if (array is not null)
				{
					var childVariables =
						new List<MoteDebugVariable>(array.Count);
					for (int i = 0; i < array.Count; ++i)
					{
						childVariables.Add(CreateDebugVariable(
							$"[{i}]",
							array[i],
							ancestors,
							depth + 1));
					}
					children = childVariables;
				}
				if (added)
				{
					ancestors.Remove(array);
				}
			}
			return new MoteDebugVariable(
				name,
				value.ValueType.ToString(),
				displayValue,
				children);
		}

		IReadOnlyList<MoteDebugCallFrame> IMoteDebugExecution.GetCallStack()
		{
			return _debugCallStack.ToArray();
		}

		void IMoteDebugExecution.Start()
		{
			Evaluate();
		}

		void IMoteDebugExecution.Continue()
		{
			ContinueDebugging();
		}

		void IMoteDebugExecution.StepInto()
		{
			StepInto();
		}

		void IMoteDebugExecution.StepOver()
		{
			StepOver();
		}

		void IMoteDebugExecution.StepOut()
		{
			StepOut();
		}

		void IMoteDebugExecution.Stop()
		{
			StopDebugging();
		}

		int IMoteDebugExecution.SetBreakpoint(int line)
		{
			return _debugSession.SetBreakpoint(line);
		}

		bool IMoteDebugExecution.RemoveBreakpoint(int line)
		{
			return _debugSession.RemoveBreakpoint(line);
		}

		bool IMoteDebugExecution.HasBreakpoint(int line)
		{
			return _debugSession.HasBreakpoint(line);
		}
	}
}
#endif
