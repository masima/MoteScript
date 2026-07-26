using System;

namespace MoteScript
{
	public class BinaryOperatorLogicalConjunction<T> 
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.LogicalConjunction;
		public override string OperatorCode => "&&";

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			bool left = Left.EvaluateInner(context).ToBool();
			if (!left)
			{
				return new MoteValue<T>(false);
			}
			bool right = Right.EvaluateInner(context).ToBool();

			return new MoteValue<T>(right);
		}
	}


}
