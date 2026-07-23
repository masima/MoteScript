using System;
using System.Collections.Generic;

namespace MoteScript
{
	public class UnrayOperatorNew<T>
		: UnrayOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.New;
		public override string OperatorCode => "new";

		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			Right = rpnStack.Pop();
			return new MoteValue<T>(this);
		}

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> right = Right.EvaluteInner(context);

			switch (right.GetObject())
			{
				case IClonableContext<T> original:
					return new MoteValue<T>(original.Clone());
				default:
					throw new InvalidOperationException();
			}
		}
	}


}

