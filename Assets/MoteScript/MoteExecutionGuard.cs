using System;

namespace MoteScript
{
	internal static class MoteExecutionGuard<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		[ThreadStatic]
		private static DirectExecutionState s_directState;
		[ThreadStatic]
		private static DirectExecutionState s_cachedDirectState;
		[ThreadStatic]
		private static MoteExecution<T> s_execution;

		internal static EvaluationScope Begin(MoteExecutionOptions options)
		{
			if (s_directState != null || s_execution != null || !options.HasLimits)
			{
				return default;
			}

			s_cachedDirectState ??= new DirectExecutionState();
			s_directState = s_cachedDirectState;
			s_directState.Reset(options);
			return new EvaluationScope(EvaluationOwner.Direct);
		}

		internal static EvaluationScope Begin(MoteExecution<T> execution)
		{
			if (s_directState != null
				|| s_execution != null
				|| !execution.HasExecutionLimits)
			{
				return default;
			}

			s_execution = execution;
			return new EvaluationScope(EvaluationOwner.Execution);
		}

		internal static void Step()
		{
			if (s_execution != null)
			{
				s_execution.RecordStep();
				return;
			}

			DirectExecutionState state = s_directState;
			if (state == null)
			{
				return;
			}

			state.Options.CancellationToken.ThrowIfCancellationRequested();
			long actual = ++state.Steps;
			long maximum = state.Options.MaxSteps;
			if (maximum > 0 && actual > maximum)
			{
				throw new MoteExecutionLimitException(
					MoteExecutionLimit.Steps,
					actual,
					maximum);
			}
		}

		internal static FunctionScope EnterFunction()
		{
			if (s_execution != null)
			{
				s_execution.EnterFunction();
				return new FunctionScope(true);
			}

			DirectExecutionState state = s_directState;
			if (state == null)
			{
				return default;
			}

			state.Options.CancellationToken.ThrowIfCancellationRequested();
			int actual = ++state.FunctionDepth;
			int maximum = state.Options.MaxFunctionDepth;
			if (maximum > 0 && actual > maximum)
			{
				--state.FunctionDepth;
				throw new MoteExecutionLimitException(
					MoteExecutionLimit.FunctionDepth,
					actual,
					maximum);
			}
			return new FunctionScope(true);
		}

		private static void ExitFunction()
		{
			if (s_execution != null)
			{
				s_execution.ExitFunction();
			}
			else
			{
				--s_directState.FunctionDepth;
			}
		}

		private sealed class DirectExecutionState
		{
			internal MoteExecutionOptions Options;
			internal long Steps;
			internal int FunctionDepth;

			internal void Reset(MoteExecutionOptions options)
			{
				Options = options;
				Steps = 0;
				FunctionDepth = 0;
			}
		}

		internal enum EvaluationOwner : byte
		{
			None,
			Direct,
			Execution,
		}

		internal readonly struct EvaluationScope : IDisposable
		{
			private readonly EvaluationOwner _owner;

			internal EvaluationScope(EvaluationOwner owner)
			{
				_owner = owner;
			}

			public void Dispose()
			{
				switch (_owner)
				{
					case EvaluationOwner.Direct:
						s_directState = null;
						break;
					case EvaluationOwner.Execution:
						s_execution = null;
						break;
				}
			}
		}

		internal readonly struct FunctionScope : IDisposable
		{
			private readonly bool _entered;

			internal FunctionScope(bool entered)
			{
				_entered = entered;
			}

			public void Dispose()
			{
				if (_entered)
				{
					ExitFunction();
				}
			}
		}
	}
}
