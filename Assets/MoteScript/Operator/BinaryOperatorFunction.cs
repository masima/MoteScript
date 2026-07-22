using System;
using System.Collections;
using System.Collections.Generic;

namespace MoteScript
{
	public class BinaryOperatorFunction<T> 
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Function;
		public override string OperatorCode => "";


		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluteInner(context);
			MoteValue<T> right = Right.EvaluteInner(context);

			var func = left.GetObject<MoteValue<T>.Function>();
			var parameters = right.GetObject<List<MoteValue<T>>>();

			MoteValue<T> result = func.Invoke(context, parameters);
			if (result.TryGetOperator(out FlowControlOperatorReturn<T> operatorReturn))
			{
				return operatorReturn.ReturnValue;
			}
			return result;
		}
	}


}

