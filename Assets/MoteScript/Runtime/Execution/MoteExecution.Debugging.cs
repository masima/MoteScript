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
		private readonly Dictionary<int, DebugVariableTarget>
			_debugVariableTargets = new();
		private MoteDebugFaultSnapshot _faultSnapshot;

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
		public MoteDebugFaultSnapshot FaultSnapshot => _faultSnapshot;
		string IMoteDebugExecution.ResultText =>
			State switch
			{
				MoteExecutionState.Completed => FormatDebugValue(Result),
				MoteExecutionState.Yielded =>
					$"Yield: {FormatDebugValue(YieldValue)}",
				MoteExecutionState.Faulted => Exception is null
					? "Faulted"
					: $"{Exception.GetType().Name}: {Exception.Message}",
				_ => string.Empty,
			};

		public MoteValue<T> ContinueDebugging()
		{
			MoteExecutionState suspendedState = EnsureDebugSuspended();
			_debugSession.Continue();
			return ResumeCore(suspendedState);
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
			if (State == MoteExecutionState.Faulted
				&& _faultSnapshot is not null)
			{
				return _faultSnapshot.Variables;
			}
			return BuildDebugVariables();
		}

		private IReadOnlyList<MoteDebugVariable> BuildDebugVariables()
		{
			var variables = new List<MoteDebugVariable>(_context.Count);
			var ancestors = new HashSet<object>();
			_debugVariableTargets.Clear();
			int nextId = 1;
			if (_context is IEnumerable<KeyValuePair<string, MoteValue<T>>> values)
			{
				foreach (KeyValuePair<string, MoteValue<T>> pair in values)
				{
					variables.Add(CreateDebugVariable(
						pair.Key,
						pair.Value,
						new DebugVariableTarget(_context, pair.Key),
						ancestors,
						0,
						ref nextId));
				}
				variables.Sort(
					(left, right) => string.CompareOrdinal(left.Name, right.Name));
			}
			return variables;
		}

		private MoteDebugVariable CreateDebugVariable(
			string name,
			MoteValue<T> value,
			DebugVariableTarget target,
			HashSet<object> ancestors,
			int depth,
			ref int nextId)
		{
			int id = nextId++;
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
							new DebugVariableTarget(dictionary, member.Key),
							ancestors,
							depth + 1,
							ref nextId));
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
							new DebugVariableTarget(array, i),
							ancestors,
							depth + 1,
							ref nextId));
					}
					children = childVariables;
				}
				if (added)
				{
					ancestors.Remove(array);
				}
			}
			bool canEdit = (State == MoteExecutionState.Paused
					|| State == MoteExecutionState.Yielded)
				&& (value.ValueType == EValueType.Const
					|| value.ValueType == EValueType.String);
			if (canEdit)
			{
				_debugVariableTargets[id] = target;
			}
			string editValue = value.ValueType switch
			{
				EValueType.Const =>
					value.Value.ToString(null, CultureInfo.InvariantCulture),
				EValueType.String => value.StringValue,
				_ => null,
			};
			return new MoteDebugVariable(
				name,
				value.ValueType.ToString(),
				displayValue,
				children,
				id,
				canEdit,
				editValue);
		}

		bool IMoteDebugExecution.TrySetVariableValue(
			int id,
			string value,
			out string error)
		{
			if (State != MoteExecutionState.Paused
				&& State != MoteExecutionState.Yielded)
			{
				error = "Variables can only be edited while paused or yielded.";
				return false;
			}
			if (!_debugVariableTargets.TryGetValue(
				id,
				out DebugVariableTarget target))
			{
				error = "The variable is no longer available.";
				return false;
			}
			MoteValue<T> current = target.GetValue();
			try
			{
				MoteValue<T> updated = current.ValueType switch
				{
					EValueType.Const => EvaluateNumericEdit(value),
					EValueType.String => new MoteValue<T>(value),
					_ => throw new InvalidOperationException(
						"Only numeric and string values can be edited."),
				};
				target.SetValue(updated);
				error = null;
				return true;
			}
			catch (Exception exception)
			{
				error = exception.Message;
				return false;
			}
		}

		private static MoteValue<T> EvaluateNumericEdit(string expression)
		{
			if (string.IsNullOrWhiteSpace(expression))
			{
				throw new FormatException("A numeric expression is required.");
			}
			var decoder = new MoteDecoder<T>();
			MoteValue<T> result = decoder
				.Decode(expression)
				.Evaluate(new Context<T>());
			if (result.ValueType != EValueType.Const)
			{
				throw new InvalidOperationException(
					"The expression must return a numeric value.");
			}
			return new MoteValue<T>(result.Value);
		}

		private readonly struct DebugVariableTarget
		{
			private readonly IContext<T> _dictionary;
			private readonly MoteList<T> _array;
			private readonly string _key;
			private readonly int _index;

			public DebugVariableTarget(IContext<T> dictionary, string key)
			{
				_dictionary = dictionary;
				_array = null;
				_key = key;
				_index = -1;
			}

			public DebugVariableTarget(MoteList<T> array, int index)
			{
				_dictionary = null;
				_array = array;
				_key = null;
				_index = index;
			}

			public MoteValue<T> GetValue()
			{
				return _dictionary is not null
					? _dictionary[_key]
					: _array[_index];
			}

			public void SetValue(MoteValue<T> value)
			{
				if (_dictionary is not null)
				{
					_dictionary[_key] = value;
				}
				else
				{
					_array[_index] = value;
				}
			}
		}

		IReadOnlyList<MoteDebugCallFrame> IMoteDebugExecution.GetCallStack()
		{
			if (State == MoteExecutionState.Faulted
				&& _faultSnapshot is not null)
			{
				return _faultSnapshot.CallStack;
			}
			return _debugCallStack.ToArray();
		}

		internal void DebugCaptureException(Exception exception)
		{
			if (_faultSnapshot is not null) return;
			_faultSnapshot = new MoteDebugFaultSnapshot(
				exception,
				_debugSession?.CurrentLocation,
				BuildDebugVariables(),
				_debugCallStack.ToArray());
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
