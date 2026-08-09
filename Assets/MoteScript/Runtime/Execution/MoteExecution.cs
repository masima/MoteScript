using System;
using System.Collections.Generic;

namespace MoteScript
{
	public sealed partial class MoteExecution<T> : IExecutionControl
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly MoteValue<T> _script;
		private readonly IExecutionContext<T> _context;
		private readonly MoteExecutionOptions _options;
		private List<ExecutionContinuation<T>> _continuations = new(4);
		private List<ExecutionContinuation<T>> _resumeContinuations = new(4);
		private long _steps;
		private int _functionDepth;

		internal MoteExecution(
			MoteValue<T> script,
			IExecutionContext<T> context,
			MoteExecutionOptions options)
		{
			_script = script;
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_options = options;
			State = MoteExecutionState.Ready;
		}

		public MoteExecutionState State { get; private set; }
		public bool IsYielded => State == MoteExecutionState.Yielded;
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		public bool IsPaused => State == MoteExecutionState.Paused;
		internal bool IsSuspended => IsYielded || IsPaused;
#else
		internal bool IsSuspended => IsYielded;
#endif
		public MoteValue<T> YieldValue { get; private set; }
		public MoteValue<T> Result { get; private set; }
		public Exception Exception { get; private set; }

		public MoteValue<T> Evaluate()
		{
			EnsureReady();
			try
			{
				BeginExecution();
				using (MoteExecutionGuard<T>.EvaluationScope scope =
					MoteExecutionGuard<T>.Begin(this))
				{
					return Finish(_script.Evaluate(_context));
				}
			}
			catch (Exception exception)
			{
				Fail(exception);
				throw;
			}
		}

		public MoteValue<T> Resume()
		{
			return ResumeCore(MoteExecutionState.Yielded);
		}

		private MoteValue<T> ResumeCore(MoteExecutionState expectedState)
		{
			List<ExecutionContinuation<T>> current = BeginResume(expectedState);

			MoteValue<T> result = default;
			int index = 0;
			try
			{
				using (MoteExecutionGuard<T>.EvaluationScope scope =
					MoteExecutionGuard<T>.Begin(this))
				{
					for (; index < current.Count; ++index)
					{
						result = current[index].Resume(_context, result);
						if (IsSuspended)
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
					return Finish(result);
				}
			}
			catch (Exception exception)
			{
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
				DebugCaptureException(exception);
#endif
				for (; index < current.Count; ++index)
				{
					current[index].Cancel(_context);
				}
				current.Clear();
				Fail(exception);
				throw;
			}
		}

		internal void Suspend(MoteValue<T> value)
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

		internal void CancelSuspended()
		{
			if (!IsSuspended) return;
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
			State = MoteExecutionState.Completed;
			_context.ReleaseExecution(this);
		}

		internal bool HasExecutionLimits => _options.HasLimits;

		internal void RecordStep()
		{
			_options.CancellationToken.ThrowIfCancellationRequested();
			long actual = ++_steps;
			long maximum = _options.MaxSteps;
			if (maximum > 0 && actual > maximum)
			{
				throw new MoteExecutionLimitException(
					MoteExecutionLimit.Steps,
					actual,
					maximum);
			}
		}

		internal void EnterFunction()
		{
			_options.CancellationToken.ThrowIfCancellationRequested();
			int actual = ++_functionDepth;
			int maximum = _options.MaxFunctionDepth;
			if (maximum > 0 && actual > maximum)
			{
				--_functionDepth;
				throw new MoteExecutionLimitException(
					MoteExecutionLimit.FunctionDepth,
					actual,
					maximum);
			}
		}

		internal void ExitFunction()
		{
			--_functionDepth;
		}

		private void BeginExecution()
		{
			State = MoteExecutionState.Running;
			if (!_context.TryAcquireExecution(this))
			{
				throw new InvalidOperationException(
					"The execution context is already being used by another execution.");
			}
		}

		private void EnsureReady()
		{
			if (State != MoteExecutionState.Ready)
			{
				throw new InvalidOperationException("Execution can only be started once.");
			}
		}

		private List<ExecutionContinuation<T>> BeginResume(
			MoteExecutionState expectedState)
		{
			if (State != expectedState)
			{
				throw new InvalidOperationException(
					$"Only an execution in the {expectedState} state can be resumed.");
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
			return current;
		}

		private MoteValue<T> Finish(MoteValue<T> result)
		{
			if (IsSuspended) return result;
			Result = result;
			State = MoteExecutionState.Completed;
			_context.ReleaseExecution(this);
			return result;
		}

		private void Fail(Exception exception)
		{
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			DebugCaptureException(exception);
#endif
			Exception = exception;
			State = MoteExecutionState.Faulted;
			_continuations.Clear();
			_resumeContinuations.Clear();
			_context.ReleaseExecution(this);
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			_debugSession?.NotifyException(this, exception);
#endif
		}

		internal static MoteExecution<T> Get(IContext<T> context)
		{
			if (TryGet(context, out MoteExecution<T> execution))
			{
				return execution;
			}
			throw new InvalidOperationException(
				"Scripts containing yield require an IExecutionContext<T> and must be run through CreateExecution(context).");
		}

		internal static bool TryGet(
			IContext<T> context,
			out MoteExecution<T> execution)
		{
			if (context is IExecutionContext<T> executionContext
				&& executionContext.CurrentExecution is MoteExecution<T> current
				&& current.State == MoteExecutionState.Running)
			{
				execution = current;
				return true;
			}
			execution = null;
			return false;
		}

	}
}
