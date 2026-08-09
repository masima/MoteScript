using MoteScript;
using MoteScript.Unity;
using UnityEngine;

public sealed class ManagerLifecycleSample : MonoBehaviour
{
	[SerializeField] private MoteScriptManager _manager;
	[SerializeField] private MoteScriptBehaviour _earlyBehaviour;
	[SerializeField] private MoteScriptBehaviour _lateBehaviour;

	private void Awake()
	{
		Context<float> libraries = _manager.GetSharedLibraries<float>();
		StandardMathLibrary<float>.Register(libraries);

		InitializeBehaviour(_earlyBehaviour, identifier: 1, step: 1);
		InitializeBehaviour(_lateBehaviour, identifier: 2, step: 10);
	}

	private static void InitializeBehaviour(
		MoteScriptBehaviour behaviour,
		float identifier,
		float step)
	{
		Context<float> context = behaviour.GetContext<float>();
		context.Set("identifier", identifier);
		context.Set("step", step);
	}

	private void OnGUI()
	{
		GUILayout.BeginArea(new Rect(20, 20, 520, 310), GUI.skin.box);
		GUILayout.Label("MoteScript Manager Lifecycle Sample");
		GUILayout.Space(6);
		GUILayout.Label($"Manager: {_manager.LifecycleState}");

		Context<float> shared = _manager.GetSharedVariables<float>();
		GUILayout.Label(
			$"Shared  ready={Read(shared, "ready")}"
			+ $"  frame={Read(shared, "frame")}"
			+ $"  order={Read(shared, "order")}"
			+ $"  started={Read(shared, "startedCount")}");
		GUILayout.Label(
			$"Enable calls  manager={Read(shared, "managerEnables")}"
			+ $"  behaviours={Read(shared, "behaviourEnables")}");
		GUILayout.Label(
			$"Disable calls  manager={Read(shared, "managerDisables")}"
			+ $"  behaviours={Read(shared, "behaviourDisables")}"
			+ $"  destroyed={Read(shared, "behaviourDestroys")}");

		GUILayout.Space(8);
		DrawBehaviour("Early (priority 0, step 1)", _earlyBehaviour);
		DrawBehaviour("Late (priority 100, step 10)", _lateBehaviour);
		GUILayout.Space(8);
		GUILayout.Label("Expected update order: 12");
		GUILayout.Label("Open Window > MoteScript > Debugger to inspect each target.");
		GUILayout.EndArea();
	}

	private static void DrawBehaviour(
		string label,
		MoteScriptBehaviour behaviour)
	{
		Context<float> context = behaviour.GetContext<float>();
		GUILayout.Label(
			$"{label}: lifecycle={behaviour.LifecycleState}"
			+ $", execution={behaviour.ExecutionState?.ToString() ?? "None"}"
			+ $", started={Read(context, "started")}"
			+ $", value={Read(context, "localValue")}");
	}

	private static string Read(Context<float> context, string key)
	{
		return context.TryGetValue(key, out MoteValue<float> value)
			? value.Value.ToString()
			: "-";
	}
}
