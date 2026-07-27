using System;

namespace MoteScript
{
	public class BinaryOperatorAssignment<T>
		: BinaryOperator<T>, IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Assignment;
		public override string OperatorCode => "=";

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> right = Right.EvaluateInner(context);
			if (right.TryGetExecution(out MoteExecution<T> execution))
			{
				execution.AddContinuation(this);
				return right;
			}
			Left.AssignmentTo(context, right);

			return right;
		}

		MoteValue<T> IExecutionContinuation<T>.Resume(
			IContext<T> context,
			MoteValue<T> result,
			int state)
		{
			Left.AssignmentTo(context, result);
			return result;
		}

		void IExecutionContinuation<T>.Cancel(IContext<T> context)
		{
		}
	}


}
