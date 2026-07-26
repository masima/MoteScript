using System;

namespace MoteScript
{
	public class BinaryOperatorLogicalDisjunction<T> 
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.LogicalDisjunction;
		public override string OperatorCode => "||";

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			bool left = Left.EvaluateInner(context).ToBool();
			if (left)
			{
				return new MoteValue<T>(true);
			}
			bool right = Right.EvaluateInner(context).ToBool();

			return new MoteValue<T>(right);
		}
	}


}
