using System;
using System.Collections.Generic;

namespace MoteScript
{
	internal interface IMoteFunction<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		MoteValue<T> Invoke(
			IContext<T> context,
			List<MoteValue<T>> parameters,
			IContext<T> receiver);
	}

	internal interface IFunctionCallTarget<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		MoteValue<T> ResolveFunction(
			IContext<T> context,
			out IContext<T> receiver);
	}
}
