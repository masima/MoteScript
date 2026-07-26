using System;
using System.Collections.Generic;

namespace MoteScript
{
	public sealed class ScriptBlockOperator<T> : IOperator<T>
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
				result = _statements[index].EvaluteInner(context);
				if (result.TryGetYield(out MoteYield<T> yield))
				{
					yield.AddContinuation(new Continuation(this, index + 1));
					return result;
				}
				if (result.ValueType.IsLoopControl()) return result;
			}
			return result;
		}

		private sealed class Continuation : IYieldContinuation<T>
		{
			private readonly ScriptBlockOperator<T> _owner;
			private readonly int _nextStatement;

			public Continuation(ScriptBlockOperator<T> owner, int nextStatement)
			{
				_owner = owner;
				_nextStatement = nextStatement;
			}

			public MoteValue<T> Resume(IContext<T> context, MoteValue<T> result)
			{
				return _owner.ExecuteFrom(context, _nextStatement, result);
			}

			public void Cancel(IContext<T> context)
			{
			}
		}
	}
}
