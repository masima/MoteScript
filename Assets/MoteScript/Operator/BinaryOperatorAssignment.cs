using System;

namespace MoteScript
{
	public class BinaryOperatorAssignment<T>
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Assignment;
		public override string OperatorCode => "=";

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> right = Right.EvaluteInner(context);
			Left.AssignmentTo(context, right);

			return right;
		}
	}


}

