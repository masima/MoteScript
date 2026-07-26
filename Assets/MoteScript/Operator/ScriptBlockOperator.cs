using System;
using System.Collections.Generic;

namespace MoteScript
{
	public sealed class ScriptBlockOperator<T> : IOperator<T>, IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly IReadOnlyList<MoteValue<T>> _statements;

		public ScriptBlockOperator(IReadOnlyList<MoteValue<T>> statements)
		{
			_statements = statements;
		}

		public string OperatorCode => string.Empty;
		public int Priority => OperatorType.SentenceSeparator.GetPriority();
		public bool IsFinalized => true;

		public MoteValue<T> Evaluate(IContext<T> context)
		{
			return ExecuteFrom(context, 0, default);
		}

		private MoteValue<T> ExecuteFrom(IContext<T> context, int start, MoteValue<T> result)
		{
			if (result.ValueType.IsExecutionControl())
			{
				return result;
			}
			for (int index = start; index < _statements.Count; ++index)
			{
				result = _statements[index].EvaluateInner(context);
				if (result.TryGetExecution(out MoteExecution<T> execution))
				{
					execution.AddContinuation(this, index + 1);
					return result;
				}
				if (result.ValueType.IsLoopControl()) return result;
			}
			return result;
		}

		MoteValue<T> IExecutionContinuation<T>.Resume(
			IContext<T> context,
			MoteValue<T> result,
			int state)
		{
			return ExecuteFrom(context, state, result);
		}

		void IExecutionContinuation<T>.Cancel(IContext<T> context)
		{
		}
	}
}
