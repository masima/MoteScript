using System;

namespace MoteScript
{
	public sealed class FlowControlOperatorYield<T> : FlowControlOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "yield";

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> value = Statement.EvaluateInner(context);
			MoteExecution<T> execution = MoteExecution<T>.Get(context);
			execution.Yield(value);
			return new MoteValue<T>(execution);
		}
	}
}
