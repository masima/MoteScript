using System;
using System.Collections;
using System.Collections.Generic;

namespace MoteScript
{
	public enum MoteExecutionState
	{
		Ready,
		Running,
		Yielded,
		Completed,
		Faulted,
	}

	internal interface IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		MoteValue<T> Resume(IContext<T> context, MoteValue<T> result);
		void Cancel(IContext<T> context);
	}

	public sealed class MoteExecution<T> : IExecutionControl
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly MoteValue<T> _script;
		private readonly ExecutionContext _executionContext;
		private List<IExecutionContinuation<T>> _continuations = new(4);
		private List<IExecutionContinuation<T>> _resumeContinuations = new(4);

		internal MoteExecution(MoteValue<T> script, IContext<T> context)
		{
			_script = script;
			_executionContext = new ExecutionContext(this, context);
			State = MoteExecutionState.Ready;
		}

		public MoteExecutionState State { get; private set; }
		public bool IsYielded => State == MoteExecutionState.Yielded;
		public MoteValue<T> YieldValue { get; private set; }
		public MoteValue<T> Result { get; private set; }
		public Exception Exception { get; private set; }

		public MoteValue<T> Evaluate()
		{
			if (State != MoteExecutionState.Ready)
			{
				throw new InvalidOperationException("Execution can only be started once.");
			}
			State = MoteExecutionState.Running;
			try
			{
				return CompleteOrYield(_script.Evaluate(_executionContext));
			}
			catch (Exception exception)
			{
				Fault(exception);
				throw;
			}
		}

		public MoteValue<T> Resume()
		{
			if (State != MoteExecutionState.Yielded)
			{
				throw new InvalidOperationException("Only a yielded execution can be resumed.");
			}

			State = MoteExecutionState.Running;
			YieldValue = default;
			List<IExecutionContinuation<T>> current = _continuations;
			_continuations = _resumeContinuations;
			_resumeContinuations = current;
			_continuations.Clear();

			MoteValue<T> result = default;
			int index = 0;
			try
			{
				for (; index < current.Count; ++index)
				{
					result = current[index].Resume(_executionContext, result);
					if (State == MoteExecutionState.Yielded)
					{
						for (int remaining = index + 1; remaining < current.Count; ++remaining)
						{
							_continuations.Add(current[remaining]);
						}
						current.Clear();
						return result;
					}
				}
				current.Clear();
				return CompleteOrYield(result);
			}
			catch (Exception exception)
			{
				for (; index < current.Count; ++index)
				{
					current[index].Cancel(_executionContext);
				}
				current.Clear();
				Fault(exception);
				throw;
			}
		}

		internal void Yield(MoteValue<T> value)
		{
			if (State != MoteExecutionState.Running)
			{
				throw new InvalidOperationException("Yield is only valid while an execution is running.");
			}
			YieldValue = value;
			State = MoteExecutionState.Yielded;
		}

		internal void AddContinuation(IExecutionContinuation<T> continuation)
		{
			_continuations.Add(continuation);
		}

		private MoteValue<T> CompleteOrYield(MoteValue<T> result)
		{
			if (State == MoteExecutionState.Yielded) return result;
			Result = result;
			State = MoteExecutionState.Completed;
			return result;
		}

		private void Fault(Exception exception)
		{
			Exception = exception;
			State = MoteExecutionState.Faulted;
			_continuations.Clear();
			_resumeContinuations.Clear();
		}

		internal static MoteExecution<T> Get(IContext<T> context)
		{
			if (context is ExecutionContext executionContext) return executionContext.Execution;
			throw new InvalidOperationException(
				"Scripts containing yield must be run through CreateExecution(context).");
		}

		private sealed class ExecutionContext : IContext<T>, IEnumerable<KeyValuePair<string, MoteValue<T>>>
		{
			private readonly IContext<T> _inner;

			public ExecutionContext(MoteExecution<T> execution, IContext<T> inner)
			{
				Execution = execution;
				_inner = inner ?? throw new ArgumentNullException(nameof(inner));
			}

			public MoteExecution<T> Execution { get; }
			public MoteValue<T> this[string key]
			{
				get => _inner[key];
				set => _inner[key] = value;
			}
			public int Count => _inner.Count;
			public IContext<T> Instantiate() => _inner.Instantiate();
			public bool TryGetValue(string key, out MoteValue<T> value) => _inner.TryGetValue(key, out value);
			public bool Remove(string key) => _inner.Remove(key);
			public void Clear() => _inner.Clear();

			public IEnumerator<KeyValuePair<string, MoteValue<T>>> GetEnumerator()
			{
				if (_inner is IEnumerable<KeyValuePair<string, MoteValue<T>>> values)
				{
					return values.GetEnumerator();
				}
				return ((IEnumerable<KeyValuePair<string, MoteValue<T>>>)Array.Empty<KeyValuePair<string, MoteValue<T>>>()).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
