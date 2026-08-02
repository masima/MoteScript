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
		}

		private void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
			}
			_behaviours.Clear();
		}

		internal void Register(MoteScriptBehaviour behaviour)
		{
			if (behaviour is null || _behaviours.Contains(behaviour)) return;
			_behaviours.Add(behaviour);
			_behaviours.Sort(CompareBehaviours);
		}

		internal void Unregister(MoteScriptBehaviour behaviour)
		{
			_behaviours.Remove(behaviour);
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
	}
}
