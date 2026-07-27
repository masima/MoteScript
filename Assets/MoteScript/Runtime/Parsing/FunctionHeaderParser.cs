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
			out string[] parameterNames,
			out string[] localNames)
		{
			List<string> sections = StructuredExpressionReader.Split(header, ';');
			if (sections.Count > 2)
			{
				throw new FormatException("delegate local declaration is invalid");
			}

			parameterNames = ParseIdentifierList(
				sections.Count > 0 ? sections[0] : string.Empty,
				"delegate parameters",
				allowEmpty: true);
			localNames = Array.Empty<string>();

			if (sections.Count == 2)
			{
				string localSection = sections[1].Trim();
				if (!IsLocalDeclaration(localSection))
				{
					throw new FormatException(
						"delegate local declaration must start with local");
				}
				localNames = ParseIdentifierList(
					localSection.Substring("local".Length),
					"delegate local variables",
					allowEmpty: false);
			}

			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (string parameterName in parameterNames)
			{
				EnsureUnique(names, parameterName);
			}
			foreach (string localName in localNames)
			{
				EnsureUnique(names, localName);
			}
		}

		private static bool IsLocalDeclaration(string text)
		{
			const string keyword = "local";
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
