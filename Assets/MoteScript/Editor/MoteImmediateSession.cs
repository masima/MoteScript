#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MoteScript.Editor
{
	internal readonly struct MoteImmediateResult
	{
		internal MoteImmediateResult(string input, string output, bool isError)
		{
			Input = input;
			Output = output;
			IsError = isError;
		}

		internal string Input { get; }
		internal string Output { get; }
		internal bool IsError { get; }
	}

	internal interface IMoteImmediateSession
	{
		MoteImmediateResult Execute(string input);
		IReadOnlyList<MoteDebugVariable> GetVariables();
	}

	internal sealed class MoteImmediateSession<T> : IMoteImmediateSession
		where T : struct, IComparable, IFormattable, IConvertible,
		IEquatable<T>, IComparable<T>
	{
		private const long MaximumSteps = 100000;
		private const int MaximumFunctionDepth = 256;
		private readonly MoteDecoder<T> _decoder;
		private readonly Context<T> _context;

		internal MoteImmediateSession()
		{
			MoteDecoder<T>.Setup();
			_decoder = new MoteDecoder<T>();
			_context = new Context<T>();
			StandardMathLibrary<T>.Register(_context);
			StandardStringLibrary<T>.Register(_context);
			StandardRegexLibrary<T>.Register(_context);
		}

		internal Context<T> Context => _context;

		public MoteImmediateResult Execute(string input)
		{
			if (input is null) throw new ArgumentNullException(nameof(input));
			if (input.IndexOfAny(new[] { '\r', '\n' }) >= 0)
			{
				return Error(input, "Immediate mode accepts one line at a time.");
			}
			if (string.IsNullOrWhiteSpace(input))
			{
				return Error(input, "Enter an expression or statement.");
			}
			try
			{
				MoteValue<T> script = _decoder.Decode(input);
				MoteValue<T> result = script.Evaluate(
					_context,
					new MoteExecutionOptions(
						MaximumSteps,
						MaximumFunctionDepth));
				return new MoteImmediateResult(
					input,
					FormatValue(result),
					isError: false);
			}
			catch (Exception exception)
			{
				return Error(
					input,
					$"{exception.GetType().Name}: {exception.Message}");
			}
		}

		public IReadOnlyList<MoteDebugVariable> GetVariables()
		{
			var variables = new List<MoteDebugVariable>(_context.Count);
			var ancestors = new HashSet<object>();
			int nextId = 1;
			foreach (KeyValuePair<string, MoteValue<T>> pair in _context)
			{
				variables.Add(CreateVariable(
					pair.Key,
					pair.Value,
					ancestors,
					depth: 0,
					ref nextId));
			}
			variables.Sort(
				(left, right) => string.CompareOrdinal(left.Name, right.Name));
			return variables;
		}

		private static MoteImmediateResult Error(string input, string message)
		{
			return new MoteImmediateResult(input, message, isError: true);
		}

		private static string FormatValue(MoteValue<T> value)
		{
			return value.ValueType switch
			{
				EValueType.Unknown => "<completed>",
				EValueType.Const => value.Value.ToString(
					null,
					CultureInfo.InvariantCulture),
				EValueType.String => value.StringValue is null
					? "null"
					: $"\"{value.StringValue}\"",
				EValueType.Array => $"Array (Count = {value.GetArray()?.Count ?? 0})",
				EValueType.Dictionary =>
					$"Dictionary (Count = {value.GetDictionary()?.Count ?? 0})",
				EValueType.Function => "<function>",
				_ => value.StringValue,
			};
		}

		private static MoteDebugVariable CreateVariable(
			string name,
			MoteValue<T> value,
			HashSet<object> ancestors,
			int depth,
			ref int nextId)
		{
			int id = nextId++;
			IReadOnlyList<MoteDebugVariable> children = null;
			string displayValue = FormatValue(value);
			if (value.ValueType == EValueType.Dictionary)
			{
				IContext<T> dictionary = value.GetDictionary();
				bool added = dictionary is not null && ancestors.Add(dictionary);
				if (dictionary is not null && !added)
				{
					displayValue = "Circular reference";
				}
				else if (dictionary is not null && depth >= 31)
				{
					displayValue = "Maximum depth reached";
				}
				else if (dictionary
					is IEnumerable<KeyValuePair<string, MoteValue<T>>> members)
				{
					var nested = new List<MoteDebugVariable>(dictionary.Count);
					foreach (KeyValuePair<string, MoteValue<T>> member in members)
					{
						nested.Add(CreateVariable(
							member.Key,
							member.Value,
							ancestors,
							depth + 1,
							ref nextId));
					}
					nested.Sort(
						(left, right) => string.CompareOrdinal(left.Name, right.Name));
					children = nested;
				}
				if (added) ancestors.Remove(dictionary);
			}
			else if (value.ValueType == EValueType.Array)
			{
				MoteList<T> array = value.GetArray();
				bool added = array is not null && ancestors.Add(array);
				if (array is not null && !added)
				{
					displayValue = "Circular reference";
				}
				else if (array is not null && depth >= 31)
				{
					displayValue = "Maximum depth reached";
				}
				else if (array is not null)
				{
					var nested = new List<MoteDebugVariable>(array.Count);
					for (int i = 0; i < array.Count; ++i)
					{
						nested.Add(CreateVariable(
							$"[{i}]",
							array[i],
							ancestors,
							depth + 1,
							ref nextId));
					}
					children = nested;
				}
				if (added) ancestors.Remove(array);
			}
			return new MoteDebugVariable(
				name,
				value.ValueType.ToString(),
				displayValue,
				children,
				id,
				canEdit: false);
		}
	}
}
#endif
