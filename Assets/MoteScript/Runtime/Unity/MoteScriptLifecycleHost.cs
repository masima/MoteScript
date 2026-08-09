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
		void Enable();
		void Start();
		void Update();
		void Disable();
		void Destroy();
	}

	internal sealed class MoteScriptLifecycleHost<T> : IMoteScriptLifecycleHost
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		, IMoteDebugTarget
#endif
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly Context<T> _context;
		private readonly MoteValue<T> _definitionScript;
		private readonly MoteValue<T> _awakeCall;
		private readonly MoteValue<T> _enableCall;
		private readonly MoteValue<T> _startCall;
		private readonly MoteValue<T> _updateCall;
		private readonly MoteValue<T> _disableCall;
		private readonly MoteValue<T> _destroyCall;
		private MoteExecution<T> _activeExecution;
		private Exception _exception;
		private MoteScriptLifecycleState _stateBeforeDisable;
		private bool _startInvoked;
		private bool _enabled;
		private bool _destroyed;
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		private readonly MoteDebugSession<T> _debugSession;
		private readonly UnityEngine.Object _owner;
		private readonly TextAsset _source;
		private readonly string _displayName;
		private readonly bool _isManager;
#endif

		public MoteScriptLifecycleHost(
			TextAsset source,
			Context<T> context,
			UnityEngine.Object owner,
			string displayName,
			bool isManager)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			var decoder = new MoteDecoder<T>();
			if (source != null && !string.IsNullOrWhiteSpace(source.text))
			{
				#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
				_definitionScript = decoder.DecodeDebug(
					source.text,
					source.name,
					out MoteDebugDocument document);
				_debugSession = new MoteDebugSession<T>(document);
				#else
				_definitionScript = decoder.Decode(source.text);
				#endif
			}
			_awakeCall = decoder.Decode("Awake()");
			_enableCall = decoder.Decode("Enable()");
			_startCall = decoder.Decode("Start()");
			_updateCall = decoder.Decode("Update()");
			_disableCall = decoder.Decode("Disable()");
			_destroyCall = decoder.Decode("Destroy()");
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			_owner = owner;
			_source = source;
			_displayName = string.IsNullOrEmpty(displayName)
				? source?.name ?? "MoteScript"
				: displayName;
			_isManager = isManager;
			MoteDebugTargetRegistry.Register(this);
#endif
		}

		object IMoteScriptLifecycleHost.Context => _context;
		object IMoteScriptLifecycleHost.ActiveExecution => _activeExecution;
		public MoteExecutionState? ExecutionState => _activeExecution?.State;
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		string IMoteDebugTarget.DisplayName => _displayName;
		UnityEngine.Object IMoteDebugTarget.Owner => _owner;
		TextAsset IMoteDebugTarget.SourceAsset => _source;
		bool IMoteDebugTarget.IsManager => _isManager;
		MoteScriptLifecycleState IMoteDebugTarget.LifecycleState => State;
		IMoteDebugExecution IMoteDebugTarget.Execution => _activeExecution;
		Exception IMoteDebugTarget.Exception =>
			_activeExecution?.Exception ?? _exception;
#endif
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
			catch (Exception exception)
			{
				_exception = _activeExecution?.Exception ?? exception;
				State = MoteScriptLifecycleState.Faulted;
				NotifyFaulted();
				throw;
			}
		}

		public void Enable()
		{
			if (State == MoteScriptLifecycleState.Faulted)
			{
				return;
			}
			if (State != MoteScriptLifecycleState.Awakened
				&& State != MoteScriptLifecycleState.Disabled)
			{
				return;
			}
			MoteScriptLifecycleState enabledState =
				State == MoteScriptLifecycleState.Disabled
					? _stateBeforeDisable
					: MoteScriptLifecycleState.Enabled;
			try
			{
				InvokeSynchronousLifecycle("Enable", _enableCall);
				_enabled = true;
				State = enabledState;
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
				MoteDebugTargetRegistry.Register(this);
#endif
			}
			catch (Exception exception)
			{
				_exception = _activeExecution?.Exception ?? exception;
				State = MoteScriptLifecycleState.Faulted;
				NotifyFaulted();
				throw;
			}
		}

		private void InvokeSynchronousLifecycle(
			string name,
			MoteValue<T> call)
		{
			IExecutionContext<T> executionContext = _context;
			MoteExecution<T> suspendedExecution =
				executionContext.CurrentExecution;
			if (suspendedExecution is not null)
			{
				executionContext.ReleaseExecution(suspendedExecution);
			}
			try
			{
				InvokeIfDefined(name, call);
			}
			finally
			{
				if (suspendedExecution is not null
					&& !executionContext.TryAcquireExecution(suspendedExecution))
				{
					throw new InvalidOperationException(
						"The suspended lifecycle execution could not reacquire its Context after Enable.");
				}
			}
		}

		public void Start()
		{
			if (_startInvoked
				&& State != MoteScriptLifecycleState.Starting)
			{
				return;
			}
			if (State == MoteScriptLifecycleState.Starting)
			{
				ResumeStart();
				return;
			}
			EnsureState(MoteScriptLifecycleState.Enabled);
			_startInvoked = true;
			if (!HasFunction("Start"))
			{
				State = MoteScriptLifecycleState.Started;
				return;
			}
			try
			{
				_activeExecution = CreateExecution(_startCall);
				_activeExecution.Evaluate();
				State = _activeExecution.IsYielded
					? MoteScriptLifecycleState.Starting
					: MoteScriptLifecycleState.Started;
			}
			catch (Exception exception)
			{
				_exception = _activeExecution?.Exception ?? exception;
				State = MoteScriptLifecycleState.Faulted;
				NotifyFaulted();
				throw;
			}
		}

		public void Update()
		{
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			if (_activeExecution?.State == MoteExecutionState.Paused)
			{
				return;
			}
#endif
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
					_activeExecution = CreateExecution(_updateCall);
					_activeExecution.Evaluate();
				}
				State = MoteScriptLifecycleState.Updating;
			}
			catch (Exception exception)
			{
				_exception = _activeExecution?.Exception ?? exception;
				State = MoteScriptLifecycleState.Faulted;
				NotifyFaulted();
				throw;
			}
		}

		public void Disable()
		{
			if (_destroyed || !_enabled)
			{
				UnregisterDebugTarget();
				return;
			}
			MoteScriptLifecycleState previousState = State;
			_enabled = false;
			try
			{
				InvokeSynchronousLifecycle("Disable", _disableCall);
				if (previousState != MoteScriptLifecycleState.Faulted)
				{
					_stateBeforeDisable = previousState;
					State = MoteScriptLifecycleState.Disabled;
				}
			}
			catch (Exception exception)
			{
				RecordFault(exception);
				throw;
			}
			finally
			{
				UnregisterDebugTarget();
			}
		}

		public void Destroy()
		{
			if (_destroyed) return;
			Exception pendingException = null;
			try
			{
				Disable();
			}
			catch (Exception exception)
			{
				pendingException = exception;
			}

			_destroyed = true;
			try
			{
				InvokeSynchronousLifecycle("Destroy", _destroyCall);
			}
			catch (Exception exception)
			{
				RecordFault(exception);
				pendingException ??= exception;
			}
			finally
			{
				StopSuspendedExecution();
				State = MoteScriptLifecycleState.Destroyed;
				UnregisterDebugTarget();
			}
			if (pendingException is not null)
			{
				throw pendingException;
			}
		}

		private void ResumeStart()
		{
			try
			{
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
				if (_activeExecution.State == MoteExecutionState.Paused) return;
#endif
				if (_activeExecution.State == MoteExecutionState.Completed)
				{
					State = MoteScriptLifecycleState.Started;
					return;
				}
				_activeExecution.Resume();
				if (!_activeExecution.IsYielded)
				{
					State = MoteScriptLifecycleState.Started;
				}
			}
			catch (Exception exception)
			{
				_exception = _activeExecution?.Exception ?? exception;
				State = MoteScriptLifecycleState.Faulted;
				NotifyFaulted();
				throw;
			}
		}

		private bool HasFunction(string name)
		{
			return _context.TryGetValue(name, out MoteValue<T> function)
				&& function.ValueType == EValueType.Function;
		}

		private MoteExecution<T> CreateExecution(MoteValue<T> call)
		{
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			MoteExecution<T> execution = _debugSession is null
				? call.CreateExecution(_context)
				: call.CreateDebugExecution(_context, _debugSession);
			_activeExecution = execution;
			MoteDebugTargetRegistry.NotifyChanged(this);
			return execution;
#else
			_activeExecution = call.CreateExecution(_context);
			return _activeExecution;
#endif
		}

		private void NotifyFaulted()
		{
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			MoteDebugTargetRegistry.NotifyFaulted(this);
#endif
		}

		private void RecordFault(Exception exception)
		{
			_exception = _activeExecution?.Exception ?? exception;
			State = MoteScriptLifecycleState.Faulted;
			NotifyFaulted();
		}

		private void StopSuspendedExecution()
		{
			if (_activeExecution is null || !_activeExecution.IsSuspended) return;
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			_activeExecution.StopDebugging();
#else
			_activeExecution.CancelSuspended();
#endif
		}

		private void UnregisterDebugTarget()
		{
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			MoteDebugTargetRegistry.Unregister(this);
#endif
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
