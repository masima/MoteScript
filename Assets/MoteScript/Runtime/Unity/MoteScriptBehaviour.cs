using System;
using System.Collections;
using System.Collections.Generic;
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
		[SerializeField] private List<MoteInitialVariable> _initialVariables = new();
		private IMoteScriptLifecycleHost _host;

		public MoteScriptManager Manager => _manager;
		public TextAsset Script => _script;
		public int ExecutionPriority => _executionPriority;
		public IReadOnlyList<MoteInitialVariable> InitialVariables =>
			_initialVariables;
		public MoteScriptLifecycleState LifecycleState =>
			_host?.State ?? MoteScriptLifecycleState.Uninitialized;
		public MoteExecutionState? ExecutionState => _host?.ExecutionState;

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

		private void OnDestroy()
		{
			_manager?.Release(this);
		}

		internal void AttachRuntime(
			MoteScriptManager manager,
			IMoteScriptLifecycleHost host)
		{
			_manager = manager;
			_host = host;
		}

		internal void ApplyInitialVariables()
		{
			if (_host is null || _initialVariables is null) return;
			var names = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < _initialVariables.Count; ++i)
			{
				MoteInitialVariable variable = _initialVariables[i];
				if (variable is null || string.IsNullOrWhiteSpace(variable.Name))
				{
					throw new InvalidOperationException(
						$"Initial variable at index {i} on '{name}' must have a name.");
				}
				if (!names.Add(variable.Name))
				{
					throw new InvalidOperationException(
						$"Initial variable '{variable.Name}' is duplicated on '{name}'.");
				}
				if (((IDictionary)_host.Context).Contains(variable.Name))
				{
					throw new InvalidOperationException(
						$"Initial variable '{variable.Name}' on '{name}' conflicts with a reserved Context value.");
				}
			}
			for (int i = 0; i < _initialVariables.Count; ++i)
			{
				MoteInitialVariable variable = _initialVariables[i];
				ApplyInitialVariable(variable, _host.Context);
			}
		}

		private static void ApplyInitialVariable(
			MoteInitialVariable variable,
			object context)
		{
			switch (context)
			{
				case Context<float> floatContext:
					variable.Apply(floatContext);
					break;
				case Context<int> integerContext:
					variable.Apply(integerContext);
					break;
				case Context<decimal> decimalContext:
					variable.Apply(decimalContext);
					break;
				default:
					throw new InvalidOperationException(
						"The behaviour Context uses an unsupported numeric type.");
			}
		}

		internal void DetachRuntime(MoteScriptManager manager)
		{
			if (_manager != manager) return;
			try
			{
				_host?.Destroy();
			}
			finally
			{
				_host = null;
				_manager = null;
			}
		}

		internal void EnableRuntime(MoteScriptManager manager)
		{
			if (_manager != manager) return;
			_host?.Enable();
		}

		internal void DisableRuntime(MoteScriptManager manager)
		{
			if (_manager != manager) return;
			_host?.Disable();
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
