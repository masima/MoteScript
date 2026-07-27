using System;

namespace MoteScript
{
	public interface IContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		MoteValue<T> this[string key] { get; set; }

		IContext<T> Instantiate();
		int Count { get; }
		bool TryGetValue(string key, out MoteValue<T> value);
		bool Remove(string key);
		void Clear();
	}
}
