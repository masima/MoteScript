using System;
using System.Collections;
using System.Collections.Generic;

namespace MoteScript
{
	public class BinaryOperatorFunction<T> 
		: BinaryOperator<T>, IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Function;
		public override string OperatorCode => "";


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			IContext<T> receiver = null;
			MoteValue<T> left =
				Left.GetObject() is IFunctionCallTarget<T> callTarget
					? callTarget.ResolveFunction(context, out receiver)
					: Left.EvaluateInner(context);
			MoteValue<T> right = Right.EvaluateInner(context);

			var parameters = right.GetObject<List<MoteValue<T>>>();

#if MOTESCRIPT_DEBUGGER
			MoteExecution<T>.TryGet(
				context,
				out MoteExecution<T> debugExecution);
			debugExecution?.DebugEnterFunction(Left.StringValue);
			bool debugSuspended = false;
#endif
			using (MoteExecutionGuard<T>.FunctionScope scope =
				MoteExecutionGuard<T>.EnterFunction())
			{
				try
				{
					MoteValue<T> result =
						left.InvokeFunction(
							context,
							parameters,
							receiver);
					if (result.TryGetExecution(out MoteExecution<T> execution))
					{
#if MOTESCRIPT_DEBUGGER
						debugSuspended = true;
#endif
						execution.AddContinuation(this);
						return result;
					}
					if (result.TryGetOperator(out FlowControlOperatorReturn<T> operatorReturn))
					{
						return operatorReturn.ReturnValue;
					}
					return result;
				}
				finally
				{
#if MOTESCRIPT_DEBUGGER
					if (!debugSuspended)
					{
						debugExecution?.DebugExitFunction();
					}
#endif
				}
			}
		}

		MoteValue<T> IExecutionContinuation<T>.Resume(
			IContext<T> context,
			MoteValue<T> result,
			int state)
		{
#if MOTESCRIPT_DEBUGGER
			MoteExecution<T>.TryGet(
				context,
				out MoteExecution<T> debugExecution);
			debugExecution?.DebugExitFunction();
#endif
			if (result.TryGetOperator(out FlowControlOperatorReturn<T> operatorReturn))
			{
				return operatorReturn.ReturnValue;
			}
			return result;
		}

		void IExecutionContinuation<T>.Cancel(IContext<T> context)
		{
#if MOTESCRIPT_DEBUGGER
			MoteExecution<T>.TryGet(
				context,
				out MoteExecution<T> debugExecution);
			debugExecution?.DebugExitFunction();
#endif
		}
	}


}
