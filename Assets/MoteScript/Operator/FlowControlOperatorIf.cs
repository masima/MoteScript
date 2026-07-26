using System;
using System.Collections.Generic;


namespace MoteScript
{
	public class FlowControlOperatorIf<T>
		: FlowControlOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "if";


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> result = Judge.EvaluateInner(context);

			if (result.ToBool())
			{
				MoteValue<T> r = Statement.EvaluateInner(context);
				if (r.ValueType.IsExecutionControl())
				{
					return r;
				}
			}
			return result;
		}
	}
}
