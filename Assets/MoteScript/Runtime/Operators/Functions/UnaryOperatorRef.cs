using System;
using System.Collections.Generic;

namespace MoteScript
{
	internal sealed class UnaryOperatorRef<T> : UnaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
        public override OperatorType OperatorType => OperatorType.Reference;
		public override string OperatorCode => "ref";

        public override MoteValue<T> Evaluate(IContext<T> context)
        {
            return new MoteValue<T>(Right.CreateReference(context));
        }

        public override MoteValue<T> FinalizeOperator(Stack<MoteValue<T>> rpnStack)
        {
            Right = rpnStack.Pop();
            return new MoteValue<T>(this);
        }
    }
}
