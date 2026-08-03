using System;
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
		private IMoteScriptLifecycleHost _host;

		public MoteScriptManager Manager => _manager;
		public TextAsset Script => _script;
		public int ExecutionPriority => _executionPriority;
		public MoteScriptLifecycleState LifecycleState =>
			_host?.State ?? MoteScriptLifecycleState.Uninitialized;

		public Context<T> GetContext<T>()
			where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
		{
			if (_host?.Context is Context<T> context) return context;
			if (_host is null)
			{
				throw new InvalidOperationException(
					"The Context is unavailable until the behaviour is registered.");
			}
			throw new InvalidOperationException(
				$"The behaviour Context does not use {typeof(T).Name}.");
		}

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

		internal void AttachRuntime(
			MoteScriptManager manager,
			IMoteScriptLifecycleHost host)
		{
			_manager = manager;
			_host = host;
		}

		internal void DetachRuntime(MoteScriptManager manager)
		{
			if (_manager != manager) return;
			_host?.Disable();
			_host = null;
		}

		internal void StartLifecycle()
		{
			_host?.Start();
		}

		internal void UpdateLifecycle()
		{
			_host?.Update();
		}
	}
}
