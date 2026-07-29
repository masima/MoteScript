using System;
using System.Collections.Generic;

namespace MoteScript
{
	internal interface IMoteReference<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		MoteValue<T> Get();
		void Set(MoteValue<T> value);
	}

	internal interface IMoteReferenceSource<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		IMoteReference<T> CreateReference(IContext<T> context);
	}

	internal sealed class VariableMoteReference<T> : IMoteReference<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly IContext<T> _context;
		private readonly string _name;

		public VariableMoteReference(IContext<T> context, string name)
		{
			_context = context;
			_name = name;
		}

		public MoteValue<T> Get()
		{
			if (_context.TryGetValue(_name, out MoteValue<T> value))
			{
				return value;
			}
			throw new InvalidOperationException(
				$"ref target is no longer defined : {_name}");
		}

		public void Set(MoteValue<T> value)
		{
			MoteValue<T> current = Get();
			MoteValue<T>.ValidateAssignment(current, value, _name);
			_context[_name] = value;
		}
	}

	internal sealed class ArrayElementMoteReference<T> : IMoteReference<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly List<MoteValue<T>> _array;
		private readonly int _index;

		public ArrayElementMoteReference(
			List<MoteValue<T>> array,
			int index)
		{
			_array = array;
			_index = index;
		}

		public MoteValue<T> Get()
		{
			return _array[_index];
		}

		public void Set(MoteValue<T> value)
		{
			MoteValue<T>.ValidateAssignment(
				_array[_index],
				value,
				$"[{_index}]");
			_array[_index] = value;
		}
	}

	internal sealed class DictionaryMemberMoteReference<T> : IMoteReference<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly IContext<T> _dictionary;
		private readonly string _memberName;

		public DictionaryMemberMoteReference(
			IContext<T> dictionary,
			string memberName)
		{
			_dictionary = dictionary;
			_memberName = memberName;
		}

		public MoteValue<T> Get()
		{
			if (_dictionary.TryGetValue(
				_memberName,
				out MoteValue<T> value))
			{
				return value;
			}
			throw new InvalidOperationException(
				$"ref target is no longer defined : {_memberName}");
		}

		public void Set(MoteValue<T> value)
		{
			MoteValue<T> current = Get();
			MoteValue<T>.ValidateAssignment(
				current,
				value,
				_memberName);
			_dictionary[_memberName] = value;
		}
	}

	internal sealed class ReadOnlyMoteReference<T> : IMoteReference<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private MoteValue<T> _value;
		private string _name;

		public ReadOnlyMoteReference(
			MoteValue<T> value,
			string name)
		{
			_value = value;
			_name = name;
		}

		public void Reset(
			MoteValue<T> value,
			string name)
		{
			_value = value;
			_name = name;
		}

		public MoteValue<T> Get()
		{
			return _value;
		}

		public void Set(MoteValue<T> value)
		{
			throw new InvalidOperationException(
				$"self variable '{_name}' is read-only.");
		}
	}
}
