using System;
using System.Collections.Generic;

namespace MoteScript
{
	public abstract class FlowControlOperator<T>
		: IOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public abstract OperatorType OperatorType { get; }
		public int Priority => OperatorType.GetPriority();
		public abstract string OperatorCode { get; }

		public MoteValue<T> Judge;
		public MoteValue<T> Statement;

		public bool IsFinalized
		{
			get
			{
				return Judge.ValueType.IsValid() && Statement.ValueType.IsValid();
			}
		}

		// public virtual MoteValue<T> SplitSentence(
		// 	MoteDecoder<T> decoder
		// 	, string sentence
		// 	, ref int startat
		// 	)
		// {
		// 	if (!decoder.TryGetStatement(sentence, ref startat, "()", out Judge))
		// 	{
		// 		throw new InvalidOperationException();
		// 	}
		// 	if (!decoder.TryGetStatement(sentence, ref startat, "{}", out Statement))
		// 	{
		// 		throw new InvalidOperationException();
		// 	}

		// 	MoteValue<T> flowControlValue = new(this);
		// 	if (!decoder.TryGetFlowControlOperator(sentence, ref startat, out OperatorInfo operatorInfo))
		// 	{
		// 		return flowControlValue;
		// 	}

		// 	// else
		// 	startat += operatorInfo.OperatorCode.Length;

		// 	var subOperator = Activator.CreateInstance(operatorInfo.Type) as FlowControlOperator<T>;
		// 	subOperator.Judge = flowControlValue;
		// 	return subOperator.SplitSentence(decoder, sentence, ref startat);
		// }


		public abstract MoteValue<T> Evalute(IContext<T> context);
	}

}
