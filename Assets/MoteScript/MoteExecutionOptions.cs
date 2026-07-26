using System;
using System.Threading;

namespace MoteScript
{
	/// <summary>
	/// Limits a single MoteScript evaluation or resumable execution.
	/// A value of zero disables the corresponding limit.
	/// </summary>
	public readonly struct MoteExecutionOptions
	{
		public long MaxSteps { get; }
		public int MaxFunctionDepth { get; }
		public CancellationToken CancellationToken { get; }

		public MoteExecutionOptions(
			long maxSteps = 0,
			int maxFunctionDepth = 0,
			CancellationToken cancellationToken = default)
		{
			if (maxSteps < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxSteps));
			}
			if (maxFunctionDepth < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxFunctionDepth));
			}

			MaxSteps = maxSteps;
			MaxFunctionDepth = maxFunctionDepth;
			CancellationToken = cancellationToken;
		}

		internal bool HasLimits =>
			MaxSteps > 0
			|| MaxFunctionDepth > 0
			|| CancellationToken.CanBeCanceled;
	}
}
