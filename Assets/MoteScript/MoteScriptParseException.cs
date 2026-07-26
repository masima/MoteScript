using System;

namespace MoteScript
{
	/// <summary>
	/// Describes a failure to parse MoteScript source code.
	/// </summary>
	public sealed class MoteScriptParseException : FormatException
	{
		public new string Source { get; }
		public int Position { get; }
		public int Line { get; }
		public int Column { get; }
		public string SourceLine { get; }

		internal MoteScriptParseException(string source, int position, Exception innerException)
			: base(CreateMessage(source, position, innerException.Message), innerException)
		{
			Source = source;
			Position = ClampPosition(source, position);
			GetLineInformation(source, Position, out int line, out int column, out string sourceLine);
			Line = line;
			Column = column;
			SourceLine = sourceLine;
		}

		private static string CreateMessage(string source, int position, string reason)
		{
			int clampedPosition = ClampPosition(source, position);
			GetLineInformation(source, clampedPosition, out int line, out int column, out string sourceLine);
			return $"{reason} (line {line}, column {column}, index {clampedPosition})"
				+ Environment.NewLine
				+ sourceLine
				+ Environment.NewLine
				+ new string(' ', Math.Max(0, column - 1))
				+ "^";
		}

		private static int ClampPosition(string source, int position)
		{
			if (string.IsNullOrEmpty(source))
			{
				return 0;
			}
			return Math.Max(0, Math.Min(position, source.Length));
		}

		private static void GetLineInformation(
			string source,
			int position,
			out int line,
			out int column,
			out string sourceLine)
		{
			line = 1;
			int lineStart = 0;
			for (int i = 0; i < position; ++i)
			{
				if (source[i] == '\n')
				{
					++line;
					lineStart = i + 1;
				}
			}

			int lineEnd = source.IndexOf('\n', lineStart);
			if (lineEnd < 0)
			{
				lineEnd = source.Length;
			}
			if (lineEnd > lineStart && source[lineEnd - 1] == '\r')
			{
				--lineEnd;
			}

			column = position - lineStart + 1;
			sourceLine = source.Substring(lineStart, lineEnd - lineStart);
		}
	}
}
