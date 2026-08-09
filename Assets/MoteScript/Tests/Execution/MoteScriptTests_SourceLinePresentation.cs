using MoteScript.Editor;
using NUnit.Framework;

namespace MoteScript.Tests
{
	internal sealed class MoteScriptTests_SourceLinePresentation
	{
		[TestCase(
			12,
			12,
			-1,
			null,
			MoteSourceLinePresentation.Selected)]
		[TestCase(
			12,
			12,
			12,
			MoteExecutionState.Paused,
			MoteSourceLinePresentation.Paused)]
		[TestCase(
			12,
			12,
			12,
			MoteExecutionState.Yielded,
			MoteSourceLinePresentation.Yielded)]
		[TestCase(
			12,
			12,
			12,
			MoteExecutionState.Faulted,
			MoteSourceLinePresentation.Faulted)]
		[TestCase(
			12,
			12,
			12,
			MoteExecutionState.Completed,
			MoteSourceLinePresentation.Selected)]
		[TestCase(
			12,
			-1,
			13,
			MoteExecutionState.Paused,
			MoteSourceLinePresentation.Normal)]
		public void Resolve_UsesExecutionStateBeforeSelection(
			int line,
			int selectedLine,
			int executionLine,
			MoteExecutionState? executionState,
			MoteSourceLinePresentation expected)
		{
			Assert.That(
				MoteSourceLinePresentationResolver.Resolve(
					line,
					selectedLine,
					executionLine,
					executionState),
				Is.EqualTo(expected));
		}
	}
}
