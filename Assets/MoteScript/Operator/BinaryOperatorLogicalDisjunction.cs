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

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			bool left = Left.EvaluteInner(context).ToBool();
			if (left)
			{
				return new MoteValue<T>(true);
			}
			bool right = Right.EvaluteInner(context).ToBool();

			return new MoteValue<T>(right);
		}
	}


}

