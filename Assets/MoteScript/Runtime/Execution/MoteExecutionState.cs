namespace MoteScript
{
	public enum MoteExecutionState
	{
		Ready,
		Running,
		Yielded,
#if MOTESCRIPT_DEBUGGER
		Paused,
		Stopped,
#endif
		Completed,
		Faulted,
	}
}
