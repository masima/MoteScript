#if MOTESCRIPT_DEBUGGER
using System;

namespace MoteScript
{
	internal sealed class DebugStatementOperator<T>
		: IOperator<T>, IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private const int ExecuteStatement = 1;
		private const int CompleteStatement = 2;
		private readonly MoteValue<T> _statement;
		private readonly MoteSourceSpan _location;

		public DebugStatementOperator(
			MoteValue<T> statement,
			MoteSourceSpan location)
		{
			_statement = statement;
			_location = location;
		}

		public string OperatorCode => string.Empty;
		public int Priority => OperatorType.SentenceSeparator.GetPriority();
		public bool IsFinalized => true;

		public MoteValue<T> Evaluate(IContext<T> context)
		{
			if (MoteExecution<T>.TryGet(context, out MoteExecution<T> execution)
				&& execution.DebugCheckpoint(_location))
			{
				execution.AddContinuation(this, ExecuteStatement);
				return new MoteValue<T>(execution);
			}
			return EvaluateStatement(context);
		}

		private MoteValue<T> EvaluateStatement(IContext<T> context)
		{
			MoteValue<T> result = _statement.EvaluateInner(context);
			if (result.TryGetExecution(out MoteExecution<T> execution))
			{
				execution.AddContinuation(this, CompleteStatement);
			}
			return result;
		}

		MoteValue<T> IExecutionContinuation<T>.Resume(
			IContext<T> context,
			MoteValue<T> result,
			int state)
		{
			return state == ExecuteStatement
				? EvaluateStatement(context)
				: result;
		}

		void IExecutionContinuation<T>.Cancel(IContext<T> context)
		{
		}
	}
}
#endif
