using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MoteScript
{
	internal static class ScriptTextReader
	{
		private static readonly Regex s_constValue =
			new Regex(@"-?\d+(\.\d*)?");
		private static readonly Regex s_word =
			new Regex(@"[a-zA-Z0-9_]+");
		private static readonly Regex s_structuredStatement =
			new Regex(@"\b(if|while|return|break|continue|yield)\b|=>");

		public static bool RequiresStructuredParsing(
			string source,
			int start)
		{
			return s_structuredStatement.IsMatch(source, start)
				|| source.IndexOf(';', start) >= 0;
		}

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

		public static string ReadEnclosed(
			string source,
			ref int position,
			char open,
			char close)
		{
			SkipTrivia(source, ref position);
			if (position >= source.Length || source[position] != open)
			{
				throw new FormatException($"expected {open}");
			}
			int start = ++position;
			int depth = 1;
			while (position < source.Length && depth > 0)
			{
				char c = source[position++];
				if (c == '"')
				{
					position =
						FindStringLiteralEnd(source, position - 1) + 1;
				}
				else if (c == open)
				{
					++depth;
				}
				else if (c == close)
				{
					--depth;
				}
			}
			if (depth != 0)
			{
				throw new FormatException($"expected {close}");
			}
			return source.Substring(start, position - start - 1);
		}

		public static string ReadStatementExpression(
			string source,
			ref int position,
			char terminator)
		{
			int start = position;
			int round = 0;
			int square = 0;
			int curly = 0;
			while (position < source.Length)
			{
				char c = source[position];
				if (c == '"')
				{
					position = FindStringLiteralEnd(source, position) + 1;
					continue;
				}
				if (c == '(') ++round;
				else if (c == ')') --round;
				else if (c == '[') ++square;
				else if (c == ']') --square;
				else if (c == '{') ++curly;
				else if (c == '}')
				{
					if (curly == 0 && terminator == '}') break;
					--curly;
				}
				else if (c == ';'
					&& round == 0
					&& square == 0
					&& curly == 0)
				{
					string result =
						source.Substring(start, position - start);
					++position;
					return result;
				}
				++position;
			}
			return source.Substring(start, position - start);
		}

		public static bool IsSpace(char c)
		{
			return c == ' '
				|| c == '\t'
				|| c == '　'
				|| c == '\n'
				|| c == '\r';
		}

		public static bool TrimStart(string source, ref int position)
		{
			while (position < source.Length)
			{
				if (!IsSpace(source[position]))
				{
					return true;
				}
				++position;
			}
			return false;
		}

		public static bool TrySkipComment(
			string source,
			ref int position)
		{
			if (position + 1 >= source.Length
				|| source[position] != '/'
				|| source[position + 1] != '/')
			{
				return false;
			}
			position += 2;
			while (position < source.Length
				&& source[position] != '\r'
				&& source[position] != '\n')
			{
				++position;
			}
			while (position < source.Length
				&& (source[position] == '\r'
					|| source[position] == '\n'))
			{
				++position;
			}
			return true;
		}

		public static bool TryReadConstValue(
			string source,
			int start,
			out string value)
		{
			Match match = s_constValue.Match(source, start);
			if (match.Success && match.Index == start)
			{
				value = source.Substring(start, match.Length);
				return true;
			}
			value = default;
			return false;
		}

		public static bool TryReadWord(
			string source,
			int start,
			out string value)
		{
			Match match = s_word.Match(source, start);
			if (match.Success && match.Index == start)
			{
				value = source.Substring(start, match.Length);
				return true;
			}
			value = default;
			return false;
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
