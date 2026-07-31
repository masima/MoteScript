#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;

namespace MoteScript
{
	public readonly struct MoteSourceSpan : IEquatable<MoteSourceSpan>
	{
		public MoteSourceSpan(
			string sourceName,
			int start,
			int length,
			int startLine,
			int endLine)
		{
			SourceName = sourceName ?? string.Empty;
			Start = start;
			Length = length;
			StartLine = startLine;
			EndLine = endLine;
		}

		public string SourceName { get; }
		public int Start { get; }
		public int Length { get; }
		public int StartLine { get; }
		public int EndLine { get; }

		public bool Equals(MoteSourceSpan other)
		{
			return string.Equals(SourceName, other.SourceName, StringComparison.Ordinal)
				&& Start == other.Start
				&& Length == other.Length;
		}

		public override bool Equals(object obj)
		{
			return obj is MoteSourceSpan other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = StringComparer.Ordinal.GetHashCode(SourceName);
				hash = (hash * 397) ^ Start;
				hash = (hash * 397) ^ Length;
				return hash;
			}
		}

		public override string ToString()
		{
			return $"{SourceName}:{StartLine}";
		}
	}
}
#endif
