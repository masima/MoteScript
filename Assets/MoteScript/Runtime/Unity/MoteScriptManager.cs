using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoteScript.Unity
{
	[DefaultExecutionOrder(-10000)]
	[DisallowMultipleComponent]
	public sealed class MoteScriptManager : MonoBehaviour
	{
		[SerializeField] private TextAsset _script;
		[SerializeField] private MoteNumericType _numericType = MoteNumericType.Float;

		private readonly List<MoteScriptBehaviour> _behaviours = new();
		private readonly List<MoteScriptBehaviour> _knownBehaviours = new();
		private IMoteScriptRuntime _runtime;
		private bool _childrenStarted;

		public static MoteScriptManager Instance { get; private set; }
		public TextAsset Script => _script;
		public MoteNumericType NumericType => _numericType;
		public int RegisteredBehaviourCount => _behaviours.Count;
		public MoteScriptLifecycleState LifecycleState =>
			_runtime?.ManagerHost.State ?? MoteScriptLifecycleState.Uninitialized;

		private void Awake()
		{
			if (Instance is not null && Instance != this)
			{
				throw new InvalidOperationException(
					"Only one active MoteScriptManager is allowed per scene.");
			}
			Instance = this;
			_runtime = CreateRuntime(_numericType, _script, this);
			_runtime.ManagerHost.InitializeAndAwake();
		}

		private void OnEnable()
		{
			_runtime?.ManagerHost.Enable();
		}

		private void OnDisable()
		{
			_runtime?.ManagerHost.Disable();
		}

		private void Start()
		{
			_runtime.ManagerHost.Start();
			if (_runtime.ManagerHost.State == MoteScriptLifecycleState.Started)
			{
				StartChildren();
			}
		}

		private void Update()
		{
			_runtime.ManagerHost.Update();
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			if (_runtime.ManagerHost.ExecutionState == MoteExecutionState.Paused)
			{
				return;
			}
#endif
			if (_runtime.ManagerHost.State == MoteScriptLifecycleState.Faulted)
			{
				return;
			}
			if (!_childrenStarted)
			{
				if (_runtime.ManagerHost.State == MoteScriptLifecycleState.Started
					|| _runtime.ManagerHost.State == MoteScriptLifecycleState.Updating)
				{
					StartChildren();
				}
				return;
			}
			for (int i = 0; i < _behaviours.Count; ++i)
			{
				if (_behaviours[i].isActiveAndEnabled)
				{
					_behaviours[i].UpdateLifecycle();
				}
			}
		}

		private void StartChildren()
		{
			for (int i = 0; i < _behaviours.Count; ++i)
			{
				if (_behaviours[i].isActiveAndEnabled)
				{
					_behaviours[i].StartLifecycle();
				}
			}
			_childrenStarted = true;
		}

		private void OnDestroy()
		{
			Exception pendingException = null;
			if (Instance == this)
			{
				Instance = null;
			}
			for (int i = _knownBehaviours.Count - 1; i >= 0; --i)
			{
				try
				{
					_knownBehaviours[i].DetachRuntime(this);
				}
				catch (Exception exception)
				{
					pendingException ??= exception;
				}
			}
			_behaviours.Clear();
			_knownBehaviours.Clear();
			try
			{
				_runtime?.ManagerHost.Destroy();
			}
			catch (Exception exception)
			{
				pendingException ??= exception;
			}
			finally
			{
				_runtime = null;
			}
			if (pendingException is not null)
			{
				throw pendingException;
			}
		}

		public Context<T> GetContext<T>()
			where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
		{
			return GetRuntimeValue<T>(_runtime?.ManagerContext, "manager Context");
		}

		public Context<T> GetSharedVariables<T>()
			where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
		{
			return GetRuntimeValue<T>(_runtime?.SharedVariables, "shared variables");
		}

		public Context<T> GetSharedLibraries<T>()
			where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
		{
			return GetRuntimeValue<T>(_runtime?.SharedLibraries, "shared libraries");
		}

		internal void Register(MoteScriptBehaviour behaviour)
		{
			if (behaviour is null || _behaviours.Contains(behaviour)) return;
			if (!_knownBehaviours.Contains(behaviour))
			{
				_knownBehaviours.Add(behaviour);
				IMoteScriptLifecycleHost host = _runtime.CreateHost(
					behaviour.Script,
					behaviour,
					behaviour.gameObject.name);
				behaviour.AttachRuntime(this, host);
				behaviour.ApplyInitialVariables();
				host.InitializeAndAwake();
			}
			_behaviours.Add(behaviour);
			_behaviours.Sort(CompareBehaviours);
			behaviour.EnableRuntime(this);
			if (_childrenStarted)
			{
				behaviour.StartLifecycle();
			}
		}

		internal void Unregister(MoteScriptBehaviour behaviour)
		{
			_behaviours.Remove(behaviour);
			behaviour.DisableRuntime(this);
		}

		internal void Release(MoteScriptBehaviour behaviour)
		{
			_behaviours.Remove(behaviour);
			_knownBehaviours.Remove(behaviour);
			behaviour.DetachRuntime(this);
		}

		internal MoteScriptBehaviour GetRegisteredBehaviour(int index)
		{
			return _behaviours[index];
		}

		private static int CompareBehaviours(
			MoteScriptBehaviour left,
			MoteScriptBehaviour right)
		{
			int priority = left.ExecutionPriority.CompareTo(right.ExecutionPriority);
			return priority != 0
				? priority
				: left.GetInstanceID().CompareTo(right.GetInstanceID());
		}

		private static IMoteScriptRuntime CreateRuntime(
			MoteNumericType numericType,
			TextAsset source,
			MoteScriptManager owner)
		{
			return numericType switch
			{
				MoteNumericType.Double => new MoteScriptRuntime<double>(
					source, owner, owner.gameObject.name),
				MoteNumericType.Integer => new MoteScriptRuntime<int>(
					source, owner, owner.gameObject.name),
				MoteNumericType.Decimal => new MoteScriptRuntime<decimal>(
					source, owner, owner.gameObject.name),
				_ => new MoteScriptRuntime<float>(
					source, owner, owner.gameObject.name),
			};
		}

		private static Context<T> GetRuntimeValue<T>(object value, string name)
			where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
		{
			if (value is Context<T> context) return context;
			if (value is null)
			{
				throw new InvalidOperationException(
					$"The {name} is unavailable before MoteScriptManager.Awake.");
			}
			throw new InvalidOperationException(
				$"The manager uses {value.GetType().GenericTypeArguments[0].Name}, not {typeof(T).Name}.");
		}
	}
}
