using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MoteScript
{
	/// <summary>
	/// Registers explicit, ordinal string operations under a child context.
	/// </summary>
	public static class StandardStringLibrary<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		public static void Register(IContext<T> context, string libraryName = "string")
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
			library["concat"] = Function(Concat);
			library["format"] = Function(Format);
			library["length"] = Function(Length);
			library["substring"] = Function(Substring);
			library["replace"] = Function(Replace);
			library["trim"] = Function(Trim);
			library["toLower"] = Function(ToLower);
			library["toUpper"] = Function(ToUpper);
			library["indexOf"] = Function(IndexOf);
			library["compare"] = Function(Compare);
			library["equals"] = Function(EqualsOrdinal);
			library["contains"] = Function(Contains);
			library["startsWith"] = Function(StartsWith);
			library["endsWith"] = Function(EndsWith);
			library["isEmpty"] = Function(IsEmpty);
			library["isNull"] = Function(IsNull);
			library["insert"] = Function(Insert);
			library["remove"] = Function(Remove);
			library["parseNumber"] = Function(ParseNumber);
			library["fromNumber"] = Function(FromNumber);
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

		private static MoteValue<T> Concat(IContext<T> context, List<MoteValue<T>> values)
		{
			var builder = new StringBuilder();
			foreach (MoteValue<T> parameter in values)
			{
				builder.Append(GetString(parameter, context, "string.concat"));
			}
			return new MoteValue<T>(builder.ToString());
		}

		private static MoteValue<T> Format(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 1, int.MaxValue, "string.format");
			string format = RequireNonNull(
				GetString(values[0], context, "string.format"),
				"string.format");
			var arguments = new object[values.Count - 1];
			for (int i = 1; i < values.Count; ++i)
			{
				MoteValue<T> value = values[i].EvaluteInner(context);
				switch (value.ValueType)
				{
					case EValueType.String:
						arguments[i - 1] = value.StringValue;
						break;
					case EValueType.Const:
						arguments[i - 1] = value.Value;
						break;
					default:
						throw new InvalidOperationException(
							$"string.format does not support {value.ValueType} arguments.");
				}
			}
			return new MoteValue<T>(
				string.Format(CultureInfo.InvariantCulture, format, arguments));
		}

		private static MoteValue<T> Length(IContext<T> context, List<MoteValue<T>> values)
		{
			string value = RequireNonNull(GetStringParameter(context, values, 0, 1, "string.length"), "string.length");
			return new MoteValue<T>(value.Length);
		}

		private static MoteValue<T> Substring(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 3, "string.substring");
			string value = RequireNonNull(GetString(values[0], context, "string.substring"), "string.substring");
			int start = GetIndex(values[1], context, "string.substring");
			return new MoteValue<T>(values.Count == 2
				? value.Substring(start)
				: value.Substring(start, GetIndex(values[2], context, "string.substring")));
		}

		private static MoteValue<T> Replace(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 3, 3, "string.replace");
			string value = RequireNonNull(GetString(values[0], context, "string.replace"), "string.replace");
			string oldValue = RequireNonNull(GetString(values[1], context, "string.replace"), "string.replace");
			string newValue = GetString(values[2], context, "string.replace");
			return new MoteValue<T>(value.Replace(oldValue, newValue));
		}

		private static MoteValue<T> Trim(IContext<T> context, List<MoteValue<T>> values)
		{
			string value = RequireNonNull(GetStringParameter(context, values, 0, 1, "string.trim"), "string.trim");
			return new MoteValue<T>(value.Trim());
		}

		private static MoteValue<T> ToLower(IContext<T> context, List<MoteValue<T>> values)
		{
			string value = RequireNonNull(GetStringParameter(context, values, 0, 1, "string.toLower"), "string.toLower");
			return new MoteValue<T>(value.ToLower(CultureInfo.InvariantCulture));
		}

		private static MoteValue<T> ToUpper(IContext<T> context, List<MoteValue<T>> values)
		{
			string value = RequireNonNull(GetStringParameter(context, values, 0, 1, "string.toUpper"), "string.toUpper");
			return new MoteValue<T>(value.ToUpper(CultureInfo.InvariantCulture));
		}

		private static MoteValue<T> IndexOf(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "string.indexOf");
			string value = RequireNonNull(GetString(values[0], context, "string.indexOf"), "string.indexOf");
			string search = RequireNonNull(GetString(values[1], context, "string.indexOf"), "string.indexOf");
			return new MoteValue<T>(value.IndexOf(search, StringComparison.Ordinal));
		}

		private static MoteValue<T> Compare(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "string.compare");
			return new MoteValue<T>(Math.Sign(string.Compare(
				GetString(values[0], context, "string.compare"),
				GetString(values[1], context, "string.compare"),
				StringComparison.Ordinal)));
		}

		private static MoteValue<T> EqualsOrdinal(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "string.equals");
			return new MoteValue<T>(string.Equals(
				GetString(values[0], context, "string.equals"),
				GetString(values[1], context, "string.equals"),
				StringComparison.Ordinal));
		}

		private static MoteValue<T> Contains(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "string.contains");
			string value = RequireNonNull(GetString(values[0], context, "string.contains"), "string.contains");
			string search = RequireNonNull(GetString(values[1], context, "string.contains"), "string.contains");
			return new MoteValue<T>(value.IndexOf(search, StringComparison.Ordinal) >= 0);
		}

		private static MoteValue<T> StartsWith(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "string.startsWith");
			string value = RequireNonNull(GetString(values[0], context, "string.startsWith"), "string.startsWith");
			string prefix = RequireNonNull(GetString(values[1], context, "string.startsWith"), "string.startsWith");
			return new MoteValue<T>(value.StartsWith(prefix, StringComparison.Ordinal));
		}

		private static MoteValue<T> EndsWith(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 2, "string.endsWith");
			string value = RequireNonNull(GetString(values[0], context, "string.endsWith"), "string.endsWith");
			string suffix = RequireNonNull(GetString(values[1], context, "string.endsWith"), "string.endsWith");
			return new MoteValue<T>(value.EndsWith(suffix, StringComparison.Ordinal));
		}

		private static MoteValue<T> IsEmpty(IContext<T> context, List<MoteValue<T>> values)
		{
			return new MoteValue<T>(
				GetStringParameter(context, values, 0, 1, "string.isEmpty") == string.Empty);
		}

		private static MoteValue<T> IsNull(IContext<T> context, List<MoteValue<T>> values)
		{
			return new MoteValue<T>(
				GetStringParameter(context, values, 0, 1, "string.isNull") == null);
		}

		private static MoteValue<T> Insert(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 3, 3, "string.insert");
			string value = RequireNonNull(GetString(values[0], context, "string.insert"), "string.insert");
			int index = GetIndex(values[1], context, "string.insert");
			string inserted = RequireNonNull(GetString(values[2], context, "string.insert"), "string.insert");
			return new MoteValue<T>(value.Insert(index, inserted));
		}

		private static MoteValue<T> Remove(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 2, 3, "string.remove");
			string value = RequireNonNull(GetString(values[0], context, "string.remove"), "string.remove");
			int start = GetIndex(values[1], context, "string.remove");
			return new MoteValue<T>(values.Count == 2
				? value.Remove(start)
				: value.Remove(start, GetIndex(values[2], context, "string.remove")));
		}

		private static MoteValue<T> ParseNumber(IContext<T> context, List<MoteValue<T>> values)
		{
			string value = RequireNonNull(
				GetStringParameter(context, values, 0, 1, "string.parseNumber"),
				"string.parseNumber");
			object number;
			if (typeof(T) == typeof(float))
			{
				number = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
			}
			else if (typeof(T) == typeof(decimal))
			{
				number = decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
			}
			else if (typeof(T) == typeof(int))
			{
				number = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
			}
			else
			{
				throw new NotSupportedException($"string.parseNumber does not support {typeof(T).Name}.");
			}
			return new MoteValue<T>((T)number);
		}

		private static MoteValue<T> FromNumber(IContext<T> context, List<MoteValue<T>> values)
		{
			RequireCount(values, 1, 1, "string.fromNumber");
			MoteValue<T> value = values[0].EvaluteInner(context);
			T number = value.GetNumericValue("string.fromNumber");
			return new MoteValue<T>(number.ToString(null, CultureInfo.InvariantCulture));
		}

		private static string GetStringParameter(
			IContext<T> context,
			List<MoteValue<T>> values,
			int index,
			int expectedCount,
			string function)
		{
			RequireCount(values, expectedCount, expectedCount, function);
			return GetString(values[index], context, function);
		}

		private static string GetString(MoteValue<T> parameter, IContext<T> context, string function)
		{
			MoteValue<T> value = parameter.EvaluteInner(context);
			if (value.ValueType != EValueType.String)
			{
				throw new InvalidOperationException(
					$"{function} requires a String value, but received {value.ValueType}.");
			}
			return value.StringValue;
		}

		private static int GetIndex(MoteValue<T> parameter, IContext<T> context, string function)
		{
			MoteValue<T> value = parameter.EvaluteInner(context);
			value.ThrowIfNotNumeric(function);
			return value.IntegerValue;
		}

		private static string RequireNonNull(string value, string function)
		{
			if (value == null)
			{
				throw new InvalidOperationException($"{function} does not accept null.");
			}
			return value;
		}

		private static void RequireCount(
			List<MoteValue<T>> values,
			int minimum,
			int maximum,
			string function)
		{
			if (values.Count < minimum || values.Count > maximum)
			{
				throw new ArgumentException(
					$"{function} expects {(minimum == maximum ? minimum.ToString() : $"{minimum}-{maximum}")}"
					+ $" arguments, but received {values.Count}.");
			}
		}
	}
}
