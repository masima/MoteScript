using System;
using System.Collections.Generic;


namespace MoteScript
{
	public class FlowControlOperatorWhile<T>
		: FlowControlOperator<T>, IExecutionContinuation<T>
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
				if (TryResolveLoopControl(result, out MoteValue<T> resolved))
				{
					return resolved;
				}
			}
			while (Judge.EvaluteInner(context).ToBool())
			{
				result = Statement.EvaluteInner(context);
				if (result.TryGetExecution(out MoteExecution<T> execution))
				{
					execution.AddContinuation(this);
					return result;
				}
				if (result.ValueType.IsLoopControl())
				{
					if (TryResolveLoopControl(result, out MoteValue<T> resolved))
					{
						return resolved;
					}
				}
			}
			return default;
		}

		private static bool TryResolveLoopControl(
			MoteValue<T> result,
			out MoteValue<T> resolved)
		{
			resolved = default;
			if (!result.TryGetOperator(out ILoopControl control))
			{
				return false;
			}
			switch (control.FlowControlType)
			{
				case FlowControlType.Break:
					return true;
				case FlowControlType.Return:
					resolved = result;
					return true;
				case FlowControlType.Continue:
				default:
					return false;
			}
		}

		MoteValue<T> IExecutionContinuation<T>.Resume(
			IContext<T> context,
			MoteValue<T> result,
			int state)
		{
			return ContinueLoop(context, result);
		}

		void IExecutionContinuation<T>.Cancel(IContext<T> context)
		{
		}
	}
}
