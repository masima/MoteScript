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
			if (ReturnValue.TryGetYield(out MoteYield<T> yield))
			{
				yield.AddContinuation(new ReturnContinuation(this));
				return ReturnValue;
			}
			return new MoteValue<T>(this);
		}

		private sealed class ReturnContinuation : IYieldContinuation<T>
		{
			private readonly FlowControlOperatorReturn<T> _owner;

			public ReturnContinuation(FlowControlOperatorReturn<T> owner)
			{
				_owner = owner;
			}

			public MoteValue<T> Resume(IContext<T> context, MoteValue<T> result)
			{
				_owner.ReturnValue = result;
				return new MoteValue<T>(_owner);
			}

			public void Cancel(IContext<T> context)
			{
			}
		}
	}
}
