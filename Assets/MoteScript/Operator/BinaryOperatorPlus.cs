using System;

namespace MoteScript
{
	public class BinaryOperatorPlus<T> 
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Plus;
		public override string OperatorCode => "+";

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluteInner(context);
			MoteValue<T> right = Right.EvaluteInner(context);

			return new MoteValue<T>(MoteValue<T>.Calculator.Add(left.Value, right.Value));
		}
	}


}

