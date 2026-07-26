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

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> right = Right.EvaluteInner(context);
			if (right.TryGetYield(out MoteYield<T> yield))
			{
				yield.AddContinuation(new AssignmentContinuation(Left));
				return right;
			}
			Left.AssignmentTo(context, right);

			return right;
		}

		private sealed class AssignmentContinuation : IYieldContinuation<T>
		{
			private readonly MoteValue<T> _target;

			public AssignmentContinuation(MoteValue<T> target)
			{
				_target = target;
			}

			public MoteValue<T> Resume(IContext<T> context, MoteValue<T> result)
			{
				_target.AssignmentTo(context, result);
				return result;
			}

			public void Cancel(IContext<T> context)
			{
			}
		}
	}


}
