using System;
using System.Collections.Generic;

namespace MoteScript
{
	/// <summary>
	/// Registers common numerical functions under a child context.
	/// </summary>
	public static class StandardMathLibrary<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		public static void Register(IContext<T> context, string libraryName = "math")
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}
			if (string.IsNullOrEmpty(libraryName))
			{
				throw new ArgumentException("Library name is required.", nameof(libraryName));
			}

			IContext<T> library = context.Instantiate();
			library["abs"] = Function(Abs);
			library["min"] = Function(Min);
			library["max"] = Function(Max);
			library["clamp"] = Function(Clamp);
			library["sign"] = Function(Sign);
			library["pow"] = Function(Pow);
			library["sqrt"] = Function(Sqrt);
			library["exp"] = Function(Exp);
			library["log"] = Function(Log);
			library["log10"] = Function(Log10);
			library["sin"] = Function(Sin);
			library["cos"] = Function(Cos);
			library["tan"] = Function(Tan);
			library["asin"] = Function(Asin);
			library["acos"] = Function(Acos);
			library["atan"] = Function(Atan);
			library["atan2"] = Function(Atan2);
			library["floor"] = Function(Floor);
			library["ceiling"] = Function(Ceiling);
			library["ceil"] = Function(Ceiling);
			library["round"] = Function(Round);
			library["truncate"] = Function(Truncate);
			library["pi"] = Function(Pi);

			var libraryValue = new MoteValue<T>(library);
			if (context.TryGetValue(libraryName, out MoteValue<T> current))
			{
				MoteValue<T>.ValidateAssignment(current, libraryValue, libraryName);
			}
			context[libraryName] = libraryValue;
		}

		private static MoteValue<T> Function(MoteValue<T>.Function function)
		{
			return new MoteValue<T>(function);
		}

		private static MoteValue<T> Abs(IContext<T> context, List<MoteValue<T>> values)
		{
			T value = GetSingleValue(context, values, "math.abs");
			if (typeof(T) == typeof(int))
			{
				return new MoteValue<T>((T)(object)Math.Abs((int)(object)value));
			}
			if (typeof(T) == typeof(decimal))
			{
				return new MoteValue<T>((T)(object)Math.Abs((decimal)(object)value));
			}
			T zero = default;
			return new MoteValue<T>(MoteValue<T>.Calculator.LessThan(value, zero)
				? MoteValue<T>.Calculator.Substruct(zero, value)
				: value);
		}

		private static MoteValue<T> Min(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "math.min");
			T left = GetValue(values[0], context, "math.min");
			T right = GetValue(values[1], context, "math.min");
			return new MoteValue<T>(
				MoteValue<T>.Calculator.LessThan(left, right) ? left : right);
		}

		private static MoteValue<T> Max(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "math.max");
			T left = GetValue(values[0], context, "math.max");
			T right = GetValue(values[1], context, "math.max");
			return new MoteValue<T>(
				MoteValue<T>.Calculator.GreaterThan(left, right) ? left : right);
		}

		private static MoteValue<T> Clamp(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 3, 3, "math.clamp");
			T value = GetValue(values[0], context, "math.clamp");
			T minimum = GetValue(values[1], context, "math.clamp");
			T maximum = GetValue(values[2], context, "math.clamp");
			if (MoteValue<T>.Calculator.GreaterThan(minimum, maximum))
			{
				throw new ArgumentException("math.clamp minimum cannot exceed maximum.");
			}
			if (MoteValue<T>.Calculator.LessThan(value, minimum))
			{
				return new MoteValue<T>(minimum);
			}
			return new MoteValue<T>(
				MoteValue<T>.Calculator.GreaterThan(value, maximum) ? maximum : value);
		}

		private static MoteValue<T> Sign(IContext<T> context, List<MoteValue<T>> values)
		{
			T value = GetSingleValue(context, values, "math.sign");
			T zero = default;
			int result = MoteValue<T>.Calculator.LessThan(value, zero)
				? -1
				: MoteValue<T>.Calculator.GreaterThan(value, zero) ? 1 : 0;
			return new MoteValue<T>(result);
		}

		private static MoteValue<T> Pow(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "math.pow");
			T value = GetValue(values[0], context, "math.pow");
			T exponent = GetValue(values[1], context, "math.pow");
			if (typeof(T) == typeof(int))
			{
				int integerExponent = (int)(object)exponent;
				if (integerExponent < 0)
				{
					throw IntegerNotImplemented("math.pow with a negative exponent");
				}
				int factor = (int)(object)value;
				int result = 1;
				checked
				{
					while (integerExponent > 0)
					{
						if ((integerExponent & 1) != 0)
						{
							result *= factor;
						}
						integerExponent >>= 1;
						if (integerExponent > 0)
						{
							factor *= factor;
						}
					}
				}
				return new MoteValue<T>((T)(object)result);
			}
			return FromDouble(Math.Pow(ToDouble(value), ToDouble(exponent)));
		}

		private static MoteValue<T> Sqrt(IContext<T> context, List<MoteValue<T>> values)
		{
			ThrowIfInteger("math.sqrt");
			return FromDouble(Math.Sqrt(ToDouble(GetSingleValue(context, values, "math.sqrt"))));
		}

		private static MoteValue<T> Exp(IContext<T> context, List<MoteValue<T>> values)
		{
			ThrowIfInteger("math.exp");
			return FromDouble(Math.Exp(ToDouble(GetSingleValue(context, values, "math.exp"))));
		}

		private static MoteValue<T> Log(IContext<T> context, List<MoteValue<T>> values)
		{
			ThrowIfInteger("math.log");
			RequireCount(values, 1, 2, "math.log");
			double value = ToDouble(GetValue(values[0], context, "math.log"));
			return FromDouble(values.Count == 1
				? Math.Log(value)
				: Math.Log(value, ToDouble(GetValue(values[1], context, "math.log"))));
		}

		private static MoteValue<T> Log10(IContext<T> context, List<MoteValue<T>> values)
		{
			ThrowIfInteger("math.log10");
			return FromDouble(Math.Log10(ToDouble(GetSingleValue(context, values, "math.log10"))));
		}

		private static MoteValue<T> Sin(IContext<T> context, List<MoteValue<T>> values)
		{
			return Transcendental(context, values, "math.sin", Math.Sin);
		}

		private static MoteValue<T> Cos(IContext<T> context, List<MoteValue<T>> values)
		{
			return Transcendental(context, values, "math.cos", Math.Cos);
		}

		private static MoteValue<T> Tan(IContext<T> context, List<MoteValue<T>> values)
		{
			return Transcendental(context, values, "math.tan", Math.Tan);
		}

		private static MoteValue<T> Asin(IContext<T> context, List<MoteValue<T>> values)
		{
			return Transcendental(context, values, "math.asin", Math.Asin);
		}

		private static MoteValue<T> Acos(IContext<T> context, List<MoteValue<T>> values)
		{
			return Transcendental(context, values, "math.acos", Math.Acos);
		}

		private static MoteValue<T> Atan(IContext<T> context, List<MoteValue<T>> values)
		{
			return Transcendental(context, values, "math.atan", Math.Atan);
		}

		private static MoteValue<T> Atan2(IContext<T> context, List<MoteValue<T>> values)
		{
			ThrowIfInteger("math.atan2");
			RequireCount(values, 2, 2, "math.atan2");
			double y = ToDouble(GetValue(values[0], context, "math.atan2"));
			double x = ToDouble(GetValue(values[1], context, "math.atan2"));
			return FromDouble(Math.Atan2(y, x));
		}

		private static MoteValue<T> Floor(IContext<T> context, List<MoteValue<T>> values)
		{
			T value = GetSingleValue(context, values, "math.floor");
			if (typeof(T) == typeof(int))
			{
				return new MoteValue<T>(value);
			}
			if (typeof(T) == typeof(decimal))
			{
				return new MoteValue<T>((T)(object)Math.Floor((decimal)(object)value));
			}
			return FromDouble(Math.Floor(ToDouble(value)));
		}

		private static MoteValue<T> Ceiling(IContext<T> context, List<MoteValue<T>> values)
		{
			T value = GetSingleValue(context, values, "math.ceiling");
			if (typeof(T) == typeof(int))
			{
				return new MoteValue<T>(value);
			}
			if (typeof(T) == typeof(decimal))
			{
				return new MoteValue<T>((T)(object)Math.Ceiling((decimal)(object)value));
			}
			return FromDouble(Math.Ceiling(ToDouble(value)));
		}

		private static MoteValue<T> Round(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 1, 2, "math.round");
			T value = GetValue(values[0], context, "math.round");
			if (typeof(T) == typeof(int))
			{
				return new MoteValue<T>(value);
			}
			int digits = values.Count == 2
				? GetInteger(values[1], context, "math.round")
				: 0;
			if (typeof(T) == typeof(decimal))
			{
				return new MoteValue<T>((T)(object)Math.Round(
					(decimal)(object)value,
					digits,
					MidpointRounding.ToEven));
			}
			return FromDouble(Math.Round(ToDouble(value), digits, MidpointRounding.ToEven));
		}

		private static MoteValue<T> Truncate(IContext<T> context, List<MoteValue<T>> values)
		{
			T value = GetSingleValue(context, values, "math.truncate");
			if (typeof(T) == typeof(int))
			{
				return new MoteValue<T>(value);
			}
			if (typeof(T) == typeof(decimal))
			{
				return new MoteValue<T>((T)(object)Math.Truncate((decimal)(object)value));
			}
			return FromDouble(Math.Truncate(ToDouble(value)));
		}

		private static MoteValue<T> Pi(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 0, 0, "math.pi");
			ThrowIfInteger("math.pi");
			return FromDouble(Math.PI);
		}

		private static MoteValue<T> Transcendental(
			IContext<T> context,
			List<MoteValue<T>> values,
			string function,
			Func<double, double> operation)
		{
			ThrowIfInteger(function);
			return FromDouble(operation(ToDouble(GetSingleValue(context, values, function))));
		}

		private static MoteValue<T> FromDouble(double value)
		{
			if (typeof(T) == typeof(float))
			{
				return new MoteValue<T>((T)(object)(float)value);
			}
			if (typeof(T) == typeof(decimal))
			{
				return new MoteValue<T>((T)(object)(decimal)value);
			}
			throw IntegerNotImplemented("floating-point result");
		}

		private static double ToDouble(T value)
		{
			return value.ToDouble(null);
		}

		private static T GetSingleValue(
			IContext<T> context,
			List<MoteValue<T>> values,
			string function)
		{
			RequireCount(values, 1, 1, function);
			return GetValue(values[0], context, function);
		}

		private static T GetValue(
			MoteValue<T> parameter,
			IContext<T> context,
			string function)
		{
			MoteValue<T> value = parameter.EvaluateInner(context);
			return value.GetNumericValue(function);
		}

		private static int GetInteger(
			MoteValue<T> parameter,
			IContext<T> context,
			string function)
		{
			MoteValue<T> value = parameter.EvaluateInner(context);
			value.ThrowIfNotNumeric(function);
			return value.IntegerValue;
		}

		private static void ThrowIfInteger(string function)
		{
			if (typeof(T) == typeof(int))
			{
				throw IntegerNotImplemented(function);
			}
		}

		private static NotImplementedException IntegerNotImplemented(string function)
		{
			return new NotImplementedException(
				$"{function} is not implemented for CalculatorInteger.");
		}

		private static void RequireCount(
			List<MoteValue<T>> values,
			int minimum,
			int maximum,
			string function)
		{
			if (values.Count < minimum || values.Count > maximum)
			{
				string expected = minimum == maximum
					? minimum.ToString()
					: $"{minimum}-{maximum}";
				throw new ArgumentException(
					$"{function} expects {expected} arguments, but received {values.Count}.");
			}
		}
	}
}
