namespace MoteScript.Unity
{
	public enum MoteScriptLifecycleState : byte
	{
		Uninitialized,
		Awakened,
		Starting,
		Started,
		Updating,
		Disabled,
		Faulted,
	}
}
