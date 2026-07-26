using System;
using System.Collections;
using System.Collections.Generic;

namespace MoteScript
{
	public class BinaryOperatorFunction<T> 
		: BinaryOperator<T>, IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Function;
		public override string OperatorCode => "";


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluateInner(context);
			MoteValue<T> right = Right.EvaluateInner(context);

			var func = left.GetObject<MoteValue<T>.Function>();
			var parameters = right.GetObject<List<MoteValue<T>>>();

			using (MoteExecutionGuard<T>.FunctionScope scope =
				MoteExecutionGuard<T>.EnterFunction())
			{
				MoteValue<T> result = func.Invoke(context, parameters);
				if (result.TryGetExecution(out MoteExecution<T> execution))
				{
					execution.AddContinuation(this);
					return result;
				}
				if (result.TryGetOperator(out FlowControlOperatorReturn<T> operatorReturn))
				{
					return operatorReturn.ReturnValue;
				}
				return result;
			}
		}

		MoteValue<T> IExecutionContinuation<T>.Resume(
			IContext<T> context,
			MoteValue<T> result,
			int state)
		{
			if (result.TryGetOperator(out FlowControlOperatorReturn<T> operatorReturn))
			{
				return operatorReturn.ReturnValue;
			}
			return result;
		}

		void IExecutionContinuation<T>.Cancel(IContext<T> context)
		{
		}
	}


}
