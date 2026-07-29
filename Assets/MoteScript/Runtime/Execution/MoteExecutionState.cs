namespace MoteScript
{
	public enum MoteExecutionState
	{
		Ready,
		Running,
		Yielded,
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		Paused,
		Stopped,
#endif
		Completed,
		Faulted,
	}
}
