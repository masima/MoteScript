using System;
using UnityEngine;

namespace MoteScript.Unity
{
	internal interface IMoteScriptLifecycleHost
	{
		object Context { get; }
		MoteScriptLifecycleState State { get; }
		void InitializeAndAwake();
		void Start();
		void Update();
		void Disable();
	}

	internal sealed class MoteScriptLifecycleHost<T> : IMoteScriptLifecycleHost
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly Context<T> _context;
		private readonly MoteValue<T> _definitionScript;
		private readonly MoteValue<T> _awakeCall;
		private readonly MoteValue<T> _startCall;
		private readonly MoteValue<T> _updateCall;

		public MoteScriptLifecycleHost(TextAsset source, Context<T> context)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			var decoder = new MoteDecoder<T>();
			if (source is not null && !string.IsNullOrWhiteSpace(source.text))
			{
				_definitionScript = decoder.Decode(source.text);
			}
			_awakeCall = decoder.Decode("Awake()");
			_startCall = decoder.Decode("Start()");
			_updateCall = decoder.Decode("Update()");
		}

		object IMoteScriptLifecycleHost.Context => _context;
		public MoteScriptLifecycleState State { get; private set; }

		public void InitializeAndAwake()
		{
			EnsureState(MoteScriptLifecycleState.Uninitialized);
			try
			{
				if (_definitionScript.ValueType.IsValid())
				{
					_definitionScript.Evaluate(_context);
				}
				InvokeIfDefined("Awake", _awakeCall);
				State = MoteScriptLifecycleState.Awakened;
			}
			catch
			{
				State = MoteScriptLifecycleState.Faulted;
				throw;
			}
		}

		public void Start()
		{
			EnsureState(MoteScriptLifecycleState.Awakened);
			InvokeWithFaultHandling("Start", _startCall);
			State = MoteScriptLifecycleState.Started;
		}

		public void Update()
		{
			if (State != MoteScriptLifecycleState.Started
				&& State != MoteScriptLifecycleState.Updating)
			{
				return;
			}
			InvokeWithFaultHandling("Update", _updateCall);
			State = MoteScriptLifecycleState.Updating;
		}

		public void Disable()
		{
			if (State != MoteScriptLifecycleState.Faulted)
			{
				State = MoteScriptLifecycleState.Disabled;
			}
		}

		private void InvokeWithFaultHandling(string name, MoteValue<T> call)
		{
			try
			{
				InvokeIfDefined(name, call);
			}
			catch
			{
				State = MoteScriptLifecycleState.Faulted;
				throw;
			}
		}

		private void InvokeIfDefined(string name, MoteValue<T> call)
		{
			if (!_context.TryGetValue(name, out MoteValue<T> function)
				|| function.ValueType != EValueType.Function)
			{
				return;
			}
			MoteValue<T> result = call.Evaluate(_context);
			if (result.ValueType == EValueType.Yield)
			{
				throw new InvalidOperationException(
					$"{name} yielded before lifecycle scheduling was enabled.");
			}
		}

		private void EnsureState(MoteScriptLifecycleState expected)
		{
			if (State != expected)
			{
				throw new InvalidOperationException(
					$"Lifecycle state must be {expected}, but was {State}.");
			}
		}
	}
}
