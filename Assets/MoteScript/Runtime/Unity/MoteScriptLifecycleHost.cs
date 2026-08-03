using System;
using UnityEngine;

namespace MoteScript.Unity
{
	internal interface IMoteScriptLifecycleHost
	{
		object Context { get; }
		object ActiveExecution { get; }
		MoteExecutionState? ExecutionState { get; }
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
		private MoteExecution<T> _activeExecution;

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
		object IMoteScriptLifecycleHost.ActiveExecution => _activeExecution;
		public MoteExecutionState? ExecutionState => _activeExecution?.State;
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
			if (State == MoteScriptLifecycleState.Starting)
			{
				ResumeStart();
				return;
			}
			EnsureState(MoteScriptLifecycleState.Awakened);
			if (!HasFunction("Start"))
			{
				State = MoteScriptLifecycleState.Started;
				return;
			}
			try
			{
				_activeExecution = _startCall.CreateExecution(_context);
				_activeExecution.Evaluate();
				State = _activeExecution.IsYielded
					? MoteScriptLifecycleState.Starting
					: MoteScriptLifecycleState.Started;
			}
			catch
			{
				State = MoteScriptLifecycleState.Faulted;
				throw;
			}
		}

		public void Update()
		{
			if (State == MoteScriptLifecycleState.Starting)
			{
				ResumeStart();
				return;
			}
			if (State != MoteScriptLifecycleState.Started
				&& State != MoteScriptLifecycleState.Updating)
			{
				return;
			}
			if (!HasFunction("Update"))
			{
				State = MoteScriptLifecycleState.Updating;
				return;
			}
			try
			{
				if (_activeExecution?.State == MoteExecutionState.Yielded)
				{
					_activeExecution.Resume();
				}
				else
				{
					_activeExecution = _updateCall.CreateExecution(_context);
					_activeExecution.Evaluate();
				}
				State = MoteScriptLifecycleState.Updating;
			}
			catch
			{
				State = MoteScriptLifecycleState.Faulted;
				throw;
			}
		}

		public void Disable()
		{
			if (State != MoteScriptLifecycleState.Faulted)
			{
				State = MoteScriptLifecycleState.Disabled;
			}
		}

		private void ResumeStart()
		{
			try
			{
				_activeExecution.Resume();
				if (!_activeExecution.IsYielded)
				{
					State = MoteScriptLifecycleState.Started;
				}
			}
			catch
			{
				State = MoteScriptLifecycleState.Faulted;
				throw;
			}
		}

		private bool HasFunction(string name)
		{
			return _context.TryGetValue(name, out MoteValue<T> function)
				&& function.ValueType == EValueType.Function;
		}

		private void InvokeIfDefined(string name, MoteValue<T> call)
		{
			if (!HasFunction(name))
			{
				return;
			}
			call.Evaluate(_context);
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
