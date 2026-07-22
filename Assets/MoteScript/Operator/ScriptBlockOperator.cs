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

		public MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> result = default;
			foreach (MoteValue<T> statement in _statements)
			{
				result = statement.EvaluteInner(context);
				if (result.ValueType.IsLoopControl())
				{
					return result;
				}
			}
			return result;
		}
	}
}
