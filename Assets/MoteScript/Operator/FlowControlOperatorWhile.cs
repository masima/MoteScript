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


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			return ContinueLoop(context, default);
		}

		private MoteValue<T> ContinueLoop(IContext<T> context, MoteValue<T> result)
		{
			if (result.ValueType.IsLoopControl())
			{
				if (result.TryGetOperator(out ILoopControl control))
				{
					if (control.FlowControlFlag.IsReturn()) return result;
					if (control.FlowControlFlag.IsBreak()) return default;
				}
			}
			while (Judge.EvaluteInner(context).ToBool())
			{
				result = Statement.EvaluteInner(context);
				if (result.TryGetExecution(out MoteExecution<T> execution))
				{
					execution.AddContinuation(new Continuation(this));
					return result;
				}
				if (result.ValueType.IsLoopControl())
				{
					if (result.TryGetOperator(out ILoopControl loopControl))
					{
						if (loopControl.FlowControlFlag.IsReturn()) return result;
						if (loopControl.FlowControlFlag.IsBreak()) return default;
					}
				}
			}
			return default;
		}

		private sealed class Continuation : IExecutionContinuation<T>
		{
			private readonly FlowControlOperatorWhile<T> _owner;

			public Continuation(FlowControlOperatorWhile<T> owner)
			{
				_owner = owner;
			}

			public MoteValue<T> Resume(IContext<T> context, MoteValue<T> result)
			{
				return _owner.ContinueLoop(context, result);
			}

			public void Cancel(IContext<T> context)
			{
			}
		}
	}
}
