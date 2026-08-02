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
		private object _context;

		public MoteScriptManager Manager => _manager;
		public TextAsset Script => _script;
		public int ExecutionPriority => _executionPriority;

		public Context<T> GetContext<T>()
			where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
		{
			if (_context is Context<T> context) return context;
			if (_context is null)
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

		internal void AttachRuntime(MoteScriptManager manager, object context)
		{
			_manager = manager;
			_context = context;
		}

		internal void DetachRuntime(MoteScriptManager manager)
		{
			if (_manager != manager) return;
			_context = null;
		}
	}
}
