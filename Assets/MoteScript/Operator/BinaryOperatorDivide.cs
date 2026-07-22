using System;

namespace MoteScript
{
	public class BinaryOperatorDivide<T> 
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Div;
		public override string OperatorCode => "/";

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluteInner(context);
			MoteValue<T> right = Right.EvaluteInner(context);

			return new MoteValue<T>(MoteValue<T>.Calculator.Divide(left.Value, right.Value));
		}
	}


}

