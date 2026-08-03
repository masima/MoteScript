using System;
using UnityEngine;

namespace MoteScript.Unity
{
	internal interface IMoteScriptRuntime
	{
		object ManagerContext { get; }
		object SharedVariables { get; }
		object SharedLibraries { get; }
		IMoteScriptLifecycleHost ManagerHost { get; }
		IMoteScriptLifecycleHost CreateHost(TextAsset source);
	}

	internal sealed class MoteScriptRuntime<T> : IMoteScriptRuntime
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly Context<T> _sharedVariables = new();
		private readonly Context<T> _sharedLibraries = new();
		private readonly Context<T> _managerContext;

		public MoteScriptRuntime(TextAsset managerSource)
		{
			MoteDecoder<T>.Setup();
			_managerContext = CreateContext();
			ManagerHost = new MoteScriptLifecycleHost<T>(
				managerSource,
				_managerContext);
		}

		object IMoteScriptRuntime.ManagerContext => _managerContext;
		object IMoteScriptRuntime.SharedVariables => _sharedVariables;
		object IMoteScriptRuntime.SharedLibraries => _sharedLibraries;
		public IMoteScriptLifecycleHost ManagerHost { get; }
		public IMoteScriptLifecycleHost CreateHost(TextAsset source)
		{
			return new MoteScriptLifecycleHost<T>(source, CreateContext());
		}

		private Context<T> CreateContext()
		{
			return new Context<T>
			{
				["shared"] = new MoteValue<T>(_sharedVariables),
				["library"] = new MoteValue<T>(_sharedLibraries),
			};
		}
	}
}
