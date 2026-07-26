using System;
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
		MoteValue<T> Resume(IContext<T> context, MoteValue<T> result, int state);
		void Cancel(IContext<T> context);
	}

	internal readonly struct ExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly IExecutionContinuation<T> _target;
		private readonly int _state;

		public ExecutionContinuation(IExecutionContinuation<T> target, int state)
		{
			_target = target;
			_state = state;
		}

		public MoteValue<T> Resume(IContext<T> context, MoteValue<T> result)
		{
			return _target.Resume(context, result, _state);
		}

		public void Cancel(IContext<T> context)
		{
			_target.Cancel(context);
		}
	}

	public sealed class MoteExecution<T> : IExecutionControl
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly MoteValue<T> _script;
		private readonly IExecutionContext<T> _context;
		private List<ExecutionContinuation<T>> _continuations = new(4);
		private List<ExecutionContinuation<T>> _resumeContinuations = new(4);

		internal MoteExecution(MoteValue<T> script, IExecutionContext<T> context)
		{
			_script = script;
			_context = context ?? throw new ArgumentNullException(nameof(context));
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
				AcquireContext();
				return CompleteOrYield(_script.Evaluate(_context));
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
			if (!ReferenceEquals(_context.CurrentExecution, this))
			{
				throw new InvalidOperationException(
					"The execution no longer owns its execution context.");
			}

			State = MoteExecutionState.Running;
			YieldValue = default;
			List<ExecutionContinuation<T>> current = _continuations;
			_continuations = _resumeContinuations;
			_resumeContinuations = current;
			_continuations.Clear();

			MoteValue<T> result = default;
			int index = 0;
			try
			{
				for (; index < current.Count; ++index)
				{
					result = current[index].Resume(_context, result);
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
					current[index].Cancel(_context);
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

		internal void AddContinuation(IExecutionContinuation<T> target, int state = 0)
		{
			_continuations.Add(new ExecutionContinuation<T>(target, state));
		}

		private MoteValue<T> CompleteOrYield(MoteValue<T> result)
		{
			if (State == MoteExecutionState.Yielded) return result;
			Result = result;
			State = MoteExecutionState.Completed;
			ReleaseContext();
			return result;
		}

		private void Fault(Exception exception)
		{
			Exception = exception;
			State = MoteExecutionState.Faulted;
			_continuations.Clear();
			_resumeContinuations.Clear();
			ReleaseContext();
		}

		internal static MoteExecution<T> Get(IContext<T> context)
		{
			if (context is IExecutionContext<T> executionContext
				&& executionContext.CurrentExecution is MoteExecution<T> execution
				&& execution.State == MoteExecutionState.Running)
			{
				return execution;
			}
			throw new InvalidOperationException(
				"Scripts containing yield require an IExecutionContext<T> and must be run through CreateExecution(context).");
		}

		private void AcquireContext()
		{
			if (!_context.TryAcquireExecution(this))
			{
				throw new InvalidOperationException(
					"The execution context is already being used by another execution.");
			}
		}

		private void ReleaseContext()
		{
			_context.ReleaseExecution(this);
		}
	}
}
