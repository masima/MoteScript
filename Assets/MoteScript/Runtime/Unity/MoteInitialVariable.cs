using System;
using UnityEngine;

namespace MoteScript.Unity
{
	public enum MoteInitialValueType : byte
	{
		Number,
		String,
		Null,
	}

	[Serializable]
	public sealed class MoteInitialVariable
	{
		[SerializeField] private string _name;
		[SerializeField] private MoteInitialValueType _type;
		[SerializeField] private double _number;
		[SerializeField] private string _string;

		public string Name => _name;
		public MoteInitialValueType Type => _type;
		public double Number => _number;
		public string String => _string;

		public MoteInitialVariable(
			string name,
			double number)
		{
			_name = name;
			_type = MoteInitialValueType.Number;
			_number = number;
		}

		public MoteInitialVariable(
			string name,
			string value)
		{
			_name = name;
			_type = value is null
				? MoteInitialValueType.Null
				: MoteInitialValueType.String;
			_string = value;
		}

		internal void Apply<T>(Context<T> context)
			where T : struct, IComparable, IFormattable, IConvertible,
			IEquatable<T>, IComparable<T>
		{
			if (_type == MoteInitialValueType.String)
			{
				context[_name] = new MoteValue<T>(_string ?? string.Empty);
				return;
			}
			if (_type == MoteInitialValueType.Null)
			{
				context[_name] = new MoteValue<T>((string)null);
				return;
			}
			context[_name] = new MoteValue<T>(ConvertNumber<T>());
		}

		private T ConvertNumber<T>()
			where T : struct, IComparable, IFormattable, IConvertible,
			IEquatable<T>, IComparable<T>
		{
			try
			{
				object value;
				if (typeof(T) == typeof(int))
				{
					if (!double.IsFinite(_number)
						|| Math.Truncate(_number) != _number)
					{
						throw new OverflowException();
					}
					value = checked((int)_number);
				}
				else if (typeof(T) == typeof(float))
				{
					float converted = (float)_number;
					if (!float.IsFinite(converted)) throw new OverflowException();
					value = converted;
				}
				else if (typeof(T) == typeof(double))
				{
					if (!double.IsFinite(_number)) throw new OverflowException();
					value = _number;
				}
				else if (typeof(T) == typeof(decimal))
				{
					value = checked((decimal)_number);
				}
				else
				{
					value = Convert.ChangeType(_number, typeof(T));
				}
				return (T)value;
			}
			catch (Exception exception)
				when (exception is OverflowException
					|| exception is InvalidCastException
					|| exception is FormatException)
			{
				throw new InvalidOperationException(
					$"Initial variable '{_name}' value {_number} cannot be represented as {typeof(T).Name}.",
					exception);
			}
		}
	}
}
