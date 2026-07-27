using System.Diagnostics;
using System;
using System.Collections.Generic;


namespace MoteScript
{
	public class FlowControlOperatorReturn<T>
		: FlowControlOperator<T>, ILoopControl, IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.FlowControl;
		public override string OperatorCode => "return";
		public FlowControlType FlowControlType => FlowControlType.Return;

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

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			if (Judge.ValueType.IsValid())
			{
				MoteValue<T> result = Judge.EvaluateInner(context);
				if (result.ValueType.IsLoopControl())
				{
					return result;
				}
			}

			ReturnValue = Statement.EvaluateInner(context);
			if (ReturnValue.TryGetExecution(out MoteExecution<T> execution))
			{
				execution.AddContinuation(this);
				return ReturnValue;
			}
			return new MoteValue<T>(this);
		}

		MoteValue<T> IExecutionContinuation<T>.Resume(
			IContext<T> context,
			MoteValue<T> result,
			int state)
		{
			ReturnValue = result;
			return new MoteValue<T>(this);
		}

		void IExecutionContinuation<T>.Cancel(IContext<T> context)
		{
		}
	}
}
