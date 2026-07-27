using System;

namespace MoteScript
{
	public class BinaryOperatorEqual<T>
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Equal;
		public override string OperatorCode => "==";

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluateInner(context);
			MoteValue<T> right = Right.EvaluateInner(context);

			return new MoteValue<T>(
				left.GetNumericValue(OperatorCode).Equals(right.GetNumericValue(OperatorCode)));
		}
	}


}
