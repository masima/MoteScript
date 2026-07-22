using System;
using System.Collections.Generic;

namespace MoteScript
{
	public abstract class BinaryOperator<T>
		: IOperator<T>
		, IRpnOperator<T>
		//where T : struct, IMValue
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public abstract OperatorType OperatorType { get; }
		public int Priority => OperatorType.GetPriority();
		public abstract string OperatorCode { get; }

		public MoteValue<T> Left;
		public MoteValue<T> Right;

		public bool IsFinalized
		{
			get
			{
				return Left.ValueType.IsValid() && Right.ValueType.IsValid();
			}
		}

		public virtual bool ConvertToRpn(
			Queue<MoteValue<T>> queue
			, List<MoteValue<T>> rpn
			, out int insertIndex
			)
		{
			// Get right value
			if (0 == queue.Count)
			{
				throw new FormatException("right value nothing.");
			}
			insertIndex = MoteDecoder<T>.GetInsertPosition(rpn, Priority);
			rpn.Insert(insertIndex++, queue.Dequeue());
			rpn.Insert(insertIndex, new MoteValue<T>(this));

			return true;
		}

		public virtual MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			Right = rpnStack.Pop();
			Left = rpnStack.Pop();
			return new MoteValue<T>(this);
		}


		public abstract MoteValue<T> Evalute(IContext<T> context);
	}

}
