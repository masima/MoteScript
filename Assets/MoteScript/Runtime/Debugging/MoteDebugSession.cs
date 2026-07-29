#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;

namespace MoteScript
{
	public sealed class MoteDebugSession<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly HashSet<int> _breakpoints = new();
		private DebugStepMode _stepMode;
		private int _targetDepth;
		private int _resumeLine;

		public MoteDebugSession(MoteDebugDocument document)
		{
			Document = document ?? throw new ArgumentNullException(nameof(document));
		}

		public MoteDebugDocument Document { get; }
		public MoteSourceSpan? CurrentLocation { get; internal set; }
		public IReadOnlyCollection<int> Breakpoints => _breakpoints;

		public event Action<MoteExecution<T>, MoteSourceSpan> Paused;
		public event Action<MoteExecution<T>, MoteSourceSpan?, Exception> ExceptionThrown;

		public int SetBreakpoint(int line)
		{
			int normalized = Document.NormalizeBreakpointLine(line);
			_breakpoints.Add(normalized);
			return normalized;
		}

		public bool RemoveBreakpoint(int line)
		{
			int normalized = Document.NormalizeBreakpointLine(line);
			return _breakpoints.Remove(normalized);
		}

		public bool HasBreakpoint(int line)
		{
			int normalized = Document.NormalizeBreakpointLine(line);
			return _breakpoints.Contains(normalized);
		}

		public void ClearBreakpoints()
		{
			_breakpoints.Clear();
		}

		internal void Continue()
		{
			SkipCurrentLine();
			_stepMode = DebugStepMode.Continue;
		}

		internal void StepInto()
		{
			SkipCurrentLine();
			_stepMode = DebugStepMode.Into;
		}

		internal void StepOver(int functionDepth)
		{
			SkipCurrentLine();
			_stepMode = DebugStepMode.Over;
			_targetDepth = functionDepth;
		}

		internal void StepOut(int functionDepth)
		{
			SkipCurrentLine();
			_stepMode = DebugStepMode.Out;
			_targetDepth = functionDepth - 1;
		}

		internal bool ShouldPause(MoteSourceSpan location, int functionDepth)
		{
			CurrentLocation = location;
			if (_resumeLine > 0)
			{
				if (location.StartLine == _resumeLine)
				{
					return false;
				}
				_resumeLine = 0;
			}
			bool breakpoint = _breakpoints.Contains(location.StartLine);
			bool step = _stepMode switch
			{
				DebugStepMode.Into => true,
				DebugStepMode.Over => functionDepth <= _targetDepth,
				DebugStepMode.Out => functionDepth <= _targetDepth,
				_ => false,
			};
			if (!breakpoint && !step)
			{
				return false;
			}

			_stepMode = DebugStepMode.Continue;
			return true;
		}

		private void SkipCurrentLine()
		{
			_resumeLine = CurrentLocation?.StartLine ?? 0;
		}

		internal void NotifyPaused(
			MoteExecution<T> execution,
			MoteSourceSpan location)
		{
			Paused?.Invoke(execution, location);
		}

		internal void NotifyException(
			MoteExecution<T> execution,
			Exception exception)
		{
			ExceptionThrown?.Invoke(execution, CurrentLocation, exception);
		}

		private enum DebugStepMode : byte
		{
			Continue,
			Into,
			Over,
			Out,
		}
	}
}
#endif
