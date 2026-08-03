namespace MoteScript.Unity
{
	public enum MoteScriptLifecycleState : byte
	{
		Uninitialized,
		Awakened,
		Started,
		Updating,
		Disabled,
		Faulted,
	}
}
