using System.Diagnostics;
using System;
using System.Collections.Generic;


namespace MoteScript
{
	public class FlowControlOperatorReturn<T>
		: FlowControlOperator<T>, ILoopControl
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "return";
		public FlowControlFlag FlowControlFlag => FlowControlFlag.Break | FlowControlFlag.Return;

		public MoteValue<T> ReturnValue { get; private set; }

		// public override MoteValue<T> SplitSentence(
		// 	MoteDecoder<T> decoder
		// 	, string sentence
		// 	, ref int startat
		// 	)
		// {
		// 	MoteValue<T> flowControlValue = new(this);
		// 	// Judge = MoteValue<T>.Default;
		// 	Statement = decoder.DecodeChild(
		// 		sentence
		// 		, ref startat
		// 		, endCode: BinaryOperatorSentenceSeparater<T>.OperatorCodeConst
		// 	);
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

			ReturnValue = Statement.EvaluteInner(context);
			return new MoteValue<T>(this);
		}
	}
}
