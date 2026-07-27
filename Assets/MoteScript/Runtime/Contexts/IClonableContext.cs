using System;

namespace MoteScript
{
	public interface IClonableContext<T> : IContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		IContext<T> Clone();
	}
}
