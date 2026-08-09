#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
namespace MoteScript.Editor
{
	internal enum MoteSourceLinePresentation : byte
	{
		Normal,
		Selected,
		Paused,
		Yielded,
		Faulted,
	}

	internal static class MoteSourceLinePresentationResolver
	{
		internal static MoteSourceLinePresentation Resolve(
			int line,
			int selectedLine,
			int executionLine,
			MoteExecutionState? executionState)
		{
			if (line == executionLine)
			{
				return executionState switch
				{
					MoteExecutionState.Paused => MoteSourceLinePresentation.Paused,
					MoteExecutionState.Yielded => MoteSourceLinePresentation.Yielded,
					MoteExecutionState.Faulted => MoteSourceLinePresentation.Faulted,
					_ => line == selectedLine
						? MoteSourceLinePresentation.Selected
						: MoteSourceLinePresentation.Normal,
				};
			}
			return line == selectedLine
				? MoteSourceLinePresentation.Selected
				: MoteSourceLinePresentation.Normal;
		}
	}
}
#endif
