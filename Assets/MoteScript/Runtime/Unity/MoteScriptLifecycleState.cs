namespace MoteScript.Unity
{
	public enum MoteScriptLifecycleState : byte
	{
		Uninitialized,
		Awakened,
		Enabled,
		Starting,
		Started,
		Updating,
		Disabled,
		Faulted,
		Destroyed,
	}
}
