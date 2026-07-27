#if MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;

namespace MoteScript
{
	public sealed class MoteDebugDocument
	{
		private readonly IReadOnlyList<MoteSourceSpan> _statements;

		internal MoteDebugDocument(
			string sourceName,
			string source,
			IReadOnlyList<MoteSourceSpan> statements)
		{
			SourceName = sourceName ?? string.Empty;
			Source = source ?? string.Empty;
			_statements = statements ?? Array.Empty<MoteSourceSpan>();
		}

		public string SourceName { get; }
		public string Source { get; }
		public IReadOnlyList<MoteSourceSpan> Statements => _statements;

		public int NormalizeBreakpointLine(int line)
		{
			if (line < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(line));
			}

			int normalized = line;
			int closestStart = -1;
			for (int i = 0; i < _statements.Count; ++i)
			{
				MoteSourceSpan statement = _statements[i];
				if (line < statement.StartLine || line > statement.EndLine)
				{
					continue;
				}
				if (statement.StartLine > closestStart)
				{
					closestStart = statement.StartLine;
					normalized = statement.StartLine;
				}
			}
			return normalized;
		}

		internal sealed class Builder
		{
			private readonly string _sourceName;
			private readonly string _source;
			private readonly List<MoteSourceSpan> _statements = new();

			public Builder(string sourceName, string source)
			{
				_sourceName = sourceName ?? string.Empty;
				_source = source ?? string.Empty;
			}

			public MoteSourceSpan AddStatement(int start, int endExclusive)
			{
				start = Math.Max(0, Math.Min(start, _source.Length));
				endExclusive = Math.Max(start, Math.Min(endExclusive, _source.Length));
				var span = new MoteSourceSpan(
					_sourceName,
					start,
					endExclusive - start,
					GetLine(start),
					GetLine(Math.Max(start, endExclusive - 1)));
				_statements.Add(span);
				return span;
			}

			public MoteDebugDocument Build()
			{
				return new MoteDebugDocument(
					_sourceName,
					_source,
					_statements.ToArray());
			}

			private int GetLine(int position)
			{
				int line = 1;
				for (int i = 0; i < position && i < _source.Length; ++i)
				{
					if (_source[i] == '\n')
					{
						++line;
					}
				}
				return line;
			}
		}
	}
}
#endif
