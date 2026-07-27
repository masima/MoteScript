using System;

namespace MoteScript
{
	internal interface IExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		MoteValue<T> Resume(IContext<T> context, MoteValue<T> result, int state);
		void Cancel(IContext<T> context);
	}
}
