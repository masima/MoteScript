using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MoteScript
{
	internal static class FunctionHeaderParser
	{
		private static readonly Regex s_identifier =
			new Regex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");

		public static void Parse(
			string header,
			out FunctionParameterDefinition[] parameters,
			out string selfName,
			out string[] localNames)
		{
			List<string> sections = StructuredExpressionReader.Split(header, ';');

			parameters = ParseParameterList(
				sections.Count > 0 ? sections[0] : string.Empty,
				allowEmpty: true);
			selfName = null;
			localNames = Array.Empty<string>();

			for (int i = 1; i < sections.Count; ++i)
			{
				string section = sections[i].Trim();
				if (IsKeyword(section, "local"))
				{
					if (localNames.Length != 0)
					{
						throw new FormatException(
							"delegate local declaration is duplicated");
					}
					localNames = ParseIdentifierList(
						section.Substring("local".Length),
						"delegate local variables",
						allowEmpty: false);
					continue;
				}
				if (IsKeyword(section, "self"))
				{
					if (selfName is not null)
					{
						throw new FormatException(
							"delegate self declaration is duplicated");
					}
					string[] selfNames = ParseIdentifierList(
						section.Substring("self".Length),
						"delegate self variable",
						allowEmpty: false);
					if (selfNames.Length != 1)
					{
						throw new FormatException(
							"delegate self declaration requires one identifier");
					}
					selfName = selfNames[0];
					continue;
				}
				throw new FormatException(
					"delegate declaration must start with self or local");
			}

			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (FunctionParameterDefinition parameter in parameters)
			{
				EnsureUnique(names, parameter.Name);
			}
			if (selfName is not null)
			{
				EnsureUnique(names, selfName);
			}
			foreach (string localName in localNames)
			{
				EnsureUnique(names, localName);
			}
		}

		private static FunctionParameterDefinition[] ParseParameterList(
			string text,
			bool allowEmpty)
		{
			string trimmed = text.Trim();
			if (trimmed.Length == 0)
			{
				if (allowEmpty)
				{
					return Array.Empty<FunctionParameterDefinition>();
				}
				throw new FormatException("delegate parameters are required");
			}

			string[] entries = trimmed.Split(',');
			var parameters =
				new FunctionParameterDefinition[entries.Length];
			for (int i = 0; i < entries.Length; ++i)
			{
				string entry = entries[i].Trim();
				bool isReference = IsKeyword(entry, "ref");
				string name = isReference
					? entry.Substring("ref".Length).Trim()
					: entry;
				if (!s_identifier.IsMatch(name))
				{
					throw new FormatException(
						$"delegate parameters contain an invalid identifier : {name}");
				}
				parameters[i] = new FunctionParameterDefinition(
					name,
					isReference);
			}
			return parameters;
		}

		private static bool IsKeyword(string text, string keyword)
		{
			return text.StartsWith(keyword, StringComparison.Ordinal)
				&& (text.Length == keyword.Length
					|| char.IsWhiteSpace(text[keyword.Length]));
		}

		private static string[] ParseIdentifierList(
			string text,
			string description,
			bool allowEmpty)
		{
			string trimmed = text.Trim();
			if (trimmed.Length == 0)
			{
				if (allowEmpty)
				{
					return Array.Empty<string>();
				}
				throw new FormatException($"{description} are required");
			}

			string[] names = trimmed.Split(',');
			for (int i = 0; i < names.Length; ++i)
			{
				names[i] = names[i].Trim();
				if (!s_identifier.IsMatch(names[i]))
				{
					throw new FormatException(
						$"{description} contain an invalid identifier : {names[i]}");
				}
			}
			return names;
		}

		private static void EnsureUnique(HashSet<string> names, string name)
		{
			if (!names.Add(name))
			{
				throw new FormatException($"duplicate delegate variable : {name}");
			}
		}
	}
}
