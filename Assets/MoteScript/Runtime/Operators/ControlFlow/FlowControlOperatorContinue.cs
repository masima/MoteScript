using System;


namespace MoteScript
{
	public class FlowControlOperatorContinue<T>
		: FlowControlOperator<T>, ILoopControl
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "continue";
		public FlowControlType FlowControlType => FlowControlType.Continue;


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			if (Judge.ValueType.IsValid())
			{
				MoteValue<T> result = Judge.EvaluateInner(context);
				if (result.ValueType.IsLoopControl())
				{
					return result;
				}
			}
			return new MoteValue<T>(this);
		}
	}
}
