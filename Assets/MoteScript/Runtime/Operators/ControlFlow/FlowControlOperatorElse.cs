using System;


namespace MoteScript
{
	public class FlowControlOperatorElse<T>
		: FlowControlOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "else";


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> result = Judge.EvaluateInner(context);
			if (result.ValueType.IsExecutionControl())
			{
				return result;
			}

			if (!result.ToBool())
			{
				return Statement.EvaluateInner(context);
			}
			return result;
		}
	}
}
