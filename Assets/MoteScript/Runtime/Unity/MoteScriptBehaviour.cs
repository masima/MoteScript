using UnityEngine;

namespace MoteScript.Unity
{
	[DefaultExecutionOrder(-9000)]
	[DisallowMultipleComponent]
	public sealed class MoteScriptBehaviour : MonoBehaviour
	{
		[SerializeField] private MoteScriptManager _manager;
		[SerializeField] private TextAsset _script;
		[SerializeField] private int _executionPriority;

		public MoteScriptManager Manager => _manager;
		public TextAsset Script => _script;
		public int ExecutionPriority => _executionPriority;

		private void OnEnable()
		{
			if (_manager is null)
			{
				_manager = MoteScriptManager.Instance;
			}
			_manager?.Register(this);
		}

		private void OnDisable()
		{
			_manager?.Unregister(this);
		}
	}
}
