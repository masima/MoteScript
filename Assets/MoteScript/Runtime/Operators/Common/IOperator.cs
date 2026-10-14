using System;
using System.Collections.Generic;

namespace MoteScript
{
	public interface IOperator
	{
		string OperatorCode { get; }
		int Priority { get; }
		bool IsFinalized { get; }
	}

	public interface IOperator<T>
		: IOperator
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		MoteValue<T> Evaluate(IContext<T> context);
	}

	public interface IOperatorOnFinalized
	{
		public bool IsOnFinishedRequired { get; }
		public void OnFinalized();
	}

	public interface IRpnOperator<T>
		: IOperator
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public bool ConvertToRpn(
			Queue<MoteValue<T>> enumerator
			, List<MoteValue<T>> rpn
			, out int insertIndex
			);
		public MoteValue<T> FinalizeOperator(Stack<MoteValue<T>> rpnStack);
	}

}
