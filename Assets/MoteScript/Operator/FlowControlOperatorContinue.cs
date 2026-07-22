using System;
using System.Collections.Generic;


namespace MoteScript
{
	public class FlowControlOperatorContinue<T>
		: FlowControlOperator<T>, ILoopControl
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "continue";
		public FlowControlFlag FlowControlFlag => FlowControlFlag.Continue;


		// public override MoteValue<T> SplitSentence(
		// 	MoteDecoder<T> decoder
		// 	, string sentence
		// 	, ref int startat
		// 	)
		// {
		// 	MoteValue<T> flowControlValue = new(this);
		// 	// Judge = MoteValue<T>.Default;
		// 	Statement = MoteValue<T>.Default;
		// 	return flowControlValue;
		// }

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			if (Judge.ValueType.IsValid())
			{
				MoteValue<T> result = Judge.EvaluteInner(context);
				if (result.ValueType.IsLoopControl())
				{
					return result;
				}
			}
			return new MoteValue<T>(this);
		}
	}
}
