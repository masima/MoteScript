using System;
using System.Collections.Generic;

namespace MoteScript
{
	internal static class StructuredExpressionReader
	{
		public static int FindAssignment(string expression)
		{
			int round = 0;
			int square = 0;
			for (int i = 0; i < expression.Length; ++i)
			{
				char c = expression[i];
				if (c == '"')
				{
					i = ScriptTextReader.FindStringLiteralEnd(expression, i);
					continue;
				}
				if (c == '(') ++round;
				else if (c == ')') --round;
				else if (c == '[') ++square;
				else if (c == ']') --square;
				else if (c == '=' && round == 0 && square == 0
					&& (i == 0 || expression[i - 1] != '=' && expression[i - 1] != '!' && expression[i - 1] != '<' && expression[i - 1] != '>')
					&& (i + 1 >= expression.Length || expression[i + 1] != '='))
				{
					return i;
				}
			}
			return -1;
		}

		public static int FindToken(string expression, string token)
		{
			int round = 0;
			int square = 0;
			int curly = 0;
			for (int i = 0; i <= expression.Length - token.Length; ++i)
			{
				char c = expression[i];
				if (c == '"')
				{
					i = ScriptTextReader.FindStringLiteralEnd(expression, i);
					continue;
				}
				if (c == '(') ++round;
				else if (c == ')') --round;
				else if (c == '[') ++square;
				else if (c == ']') --square;
				else if (c == '{') ++curly;
				else if (c == '}') --curly;
				else if (round == 0 && square == 0 && curly == 0
					&& string.CompareOrdinal(expression, i, token, 0, token.Length) == 0)
				{
					return i;
				}
			}
			return -1;
		}

		public static bool IsWrappedInParentheses(string expression)
		{
			if (expression.Length < 2 || expression[0] != '(' || expression[expression.Length - 1] != ')')
			{
				return false;
			}
			int depth = 0;
			for (int i = 0; i < expression.Length; ++i)
			{
				if (expression[i] == '"')
				{
					i = ScriptTextReader.FindStringLiteralEnd(expression, i);
				}
				else if (expression[i] == '(') ++depth;
				else if (expression[i] == ')' && --depth == 0 && i != expression.Length - 1) return false;
			}
			return depth == 0;
		}

		public static List<string> Split(string expression, char separator)
		{
			var results = new List<string>();
			int start = 0;
			int round = 0;
			int square = 0;
			int curly = 0;
			for (int i = 0; i < expression.Length; ++i)
			{
				char c = expression[i];
				if (c == '"')
				{
					i = ScriptTextReader.FindStringLiteralEnd(expression, i);
					continue;
				}
				if (c == '(') ++round;
				else if (c == ')') --round;
				else if (c == '[') ++square;
				else if (c == ']') --square;
				else if (c == '{') ++curly;
				else if (c == '}') --curly;
				else if (c == separator && round == 0 && square == 0 && curly == 0)
				{
					results.Add(expression.Substring(start, i - start));
					start = i + 1;
				}
			}
			results.Add(expression.Substring(start));
			return results;
		}
	}
}
