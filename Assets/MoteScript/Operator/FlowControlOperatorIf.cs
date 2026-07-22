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


		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> result = Judge.EvaluteInner(context);

			if (result.ToBool())
			{
				MoteValue<T> r = Statement.EvaluteInner(context);
				if (r.ValueType.IsLoopControl())
				{
					return r;
				}
			}
			return result;
		}
	}
}
