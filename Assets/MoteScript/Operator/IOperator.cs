using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoteScript
{
	public interface IOperator
	{
		//BinaryOperatorType BinaryOperatorType { get; }
		string OperatorCode { get; }
		int Priority { get; }
		bool IsFinalized { get; }
	}

	public interface IOperator<T>
		: IOperator
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		MoteValue<T> Evalute(IContext<T> context);
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
		public MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack);
	}

}
