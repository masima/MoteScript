using System;
using System.Collections.Generic;


namespace MoteScript
{
	public class FlowControlOperatorWhile<T>
		: FlowControlOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "while";


		public override MoteValue<T> Evalute(IContext<T> context)
		{
			ILoopControl loopControl = null;
			while (Judge.EvaluteInner(context).ToBool())
			{
				MoteValue<T> result = Statement.EvaluteInner(context);
				if (result.ValueType.IsLoopControl())
				{
					if (result.TryGetOperator(out loopControl)
						&& loopControl.FlowControlFlag.IsBreak()
						)
					{
						break;
					}
				}
			}

			if (loopControl is not null
				&& loopControl.FlowControlFlag.IsReturn())
			{
				return new MoteValue<T>(loopControl);
			}
			return default;
		}
	}
}
