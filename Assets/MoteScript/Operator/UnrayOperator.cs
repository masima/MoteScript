using System;
using System.Collections.Generic;

namespace MoteScript
{
	public abstract class UnrayOperator<T>
		: IOperator<T>
		, IRpnOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public abstract OperatorType OperatorType { get; }
		public int Priority => OperatorType.GetPriority();
		public abstract string OperatorCode { get; }

		public MoteValue<T> Right;

		public bool IsFinalized
		{
			get
			{
				return Right.ValueType.IsValid();
			}
		}

		public override string ToString()
		{
			return (OperatorCode ?? "null") + " " + base.ToString();
		}

		public virtual bool ConvertToRpn(
			Queue<MoteValue<T>> queue
			, List<MoteValue<T>> rpn
			, out int insertIndex
			)
		{
			insertIndex = MoteDecoder<T>.GetInsertPosition(rpn, Priority);
			rpn.Insert(insertIndex++, new MoteValue<T>(this));

			return true;
		}

		public virtual MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			// Right = rpnStack.Pop();
			// return new MoteValue<T>(this);
			return rpnStack.Pop();
		}


		public abstract MoteValue<T> Evalute(IContext<T> context);
	}

}
