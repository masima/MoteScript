using System;

namespace MoteScript
{
	public enum MoteExecutionLimit
	{
		Steps,
		FunctionDepth,
	}

	/// <summary>
	/// Thrown when a MoteScript evaluation exceeds a configured execution limit.
	/// </summary>
	public sealed class MoteExecutionLimitException : InvalidOperationException
	{
		public MoteExecutionLimit Limit { get; }
		public long Actual { get; }
		public long Maximum { get; }

		internal MoteExecutionLimitException(
			MoteExecutionLimit limit,
			long actual,
			long maximum)
			: base(CreateMessage(limit, actual, maximum))
		{
			Limit = limit;
			Actual = actual;
			Maximum = maximum;
		}

		private static string CreateMessage(
			MoteExecutionLimit limit,
			long actual,
			long maximum)
		{
			switch (limit)
			{
				case MoteExecutionLimit.Steps:
					return $"MoteScript execution exceeded the step limit ({actual} > {maximum}).";
				case MoteExecutionLimit.FunctionDepth:
					return $"MoteScript execution exceeded the function depth limit ({actual} > {maximum}).";
				default:
					return $"MoteScript execution exceeded a limit ({actual} > {maximum}).";
			}
		}
	}
}
