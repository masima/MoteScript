using System;
using System.Text;

namespace MoteScript
{
	internal static class ScriptTextReader
	{
		public static int FindStringLiteralEnd(string source, int start)
		{
			bool escaped = false;
			for (int i = start + 1; i < source.Length; ++i)
			{
				char c = source[i];
				if (escaped)
				{
					escaped = false;
					continue;
				}
				if (c == '\\')
				{
					escaped = true;
					continue;
				}
				if (c == '"') return i;
			}
			throw new FormatException("unterminated string literal");
		}

		public static bool TryReadStringLiteral(
			string source,
			ref int position,
			out string value)
		{
			if (position >= source.Length || source[position] != '"')
			{
				value = default;
				return false;
			}

			int end = FindStringLiteralEnd(source, position);
			var builder = new StringBuilder(end - position - 1);
			for (int i = position + 1; i < end; ++i)
			{
				char c = source[i];
				if (c != '\\')
				{
					builder.Append(c);
					continue;
				}
				if (++i >= end) throw new FormatException("invalid string escape");
				switch (source[i])
				{
					case '"': builder.Append('"'); break;
					case '\\': builder.Append('\\'); break;
					case 'n': builder.Append('\n'); break;
					case 'r': builder.Append('\r'); break;
					case 't': builder.Append('\t'); break;
					default:
						throw new FormatException($"unsupported string escape : \\{source[i]}");
				}
			}

			position = end + 1;
			value = builder.ToString();
			return true;
		}

		public static bool IsKeyword(string source, int position, string keyword)
		{
			if (position < 0 || position + keyword.Length > source.Length
				|| string.CompareOrdinal(source, position, keyword, 0, keyword.Length) != 0)
			{
				return false;
			}
			int end = position + keyword.Length;
			return (position == 0 || !char.IsLetterOrDigit(source[position - 1]) && source[position - 1] != '_')
				&& (end == source.Length || !char.IsLetterOrDigit(source[end]) && source[end] != '_');
		}

		public static void SkipTrivia(string source, ref int position)
		{
			while (position < source.Length)
			{
				if (char.IsWhiteSpace(source[position]) || source[position] == ';')
				{
					++position;
					continue;
				}
				if (position + 1 < source.Length && source[position] == '/' && source[position + 1] == '/')
				{
					position += 2;
					while (position < source.Length && source[position] != '\n' && source[position] != '\r') ++position;
					continue;
				}
				break;
			}
		}
	}
}
