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
		private IMoteScriptRuntime _runtime;

		public static MoteScriptManager Instance { get; private set; }
		public TextAsset Script => _script;
		public MoteNumericType NumericType => _numericType;
		public int RegisteredBehaviourCount => _behaviours.Count;

		private void Awake()
		{
			if (Instance is not null && Instance != this)
			{
				throw new InvalidOperationException(
					"Only one active MoteScriptManager is allowed per scene.");
			}
			Instance = this;
			_runtime = CreateRuntime(_numericType);
		}

		private void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
			}
			_behaviours.Clear();
			_runtime = null;
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
			_behaviours.Add(behaviour);
			_behaviours.Sort(CompareBehaviours);
			behaviour.AttachRuntime(this, _runtime.CreateHostContext());
		}

		internal void Unregister(MoteScriptBehaviour behaviour)
		{
			_behaviours.Remove(behaviour);
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

		private static IMoteScriptRuntime CreateRuntime(MoteNumericType numericType)
		{
			return numericType switch
			{
				MoteNumericType.Integer => new MoteScriptRuntime<int>(),
				MoteNumericType.Decimal => new MoteScriptRuntime<decimal>(),
				_ => new MoteScriptRuntime<float>(),
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
