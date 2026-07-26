using System;
using System.Collections.Generic;

namespace MoteScript
{
	internal sealed class DecodedScriptCache<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly Dictionary<string, MoteValue<T>> _values = new();

		public bool TryGetValue(string source, out MoteValue<T> value)
		{
			return _values.TryGetValue(source, out value);
		}

		public void Add(string source, MoteValue<T> value)
		{
			_values.Add(source, value);
		}

		public void Clear()
		{
			_values.Clear();
		}
	}
}
