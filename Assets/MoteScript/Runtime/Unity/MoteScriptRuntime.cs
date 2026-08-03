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
		IMoteScriptLifecycleHost CreateHost(
			TextAsset source,
			UnityEngine.Object owner,
			string displayName,
			bool isManager = false);
	}

	internal sealed class MoteScriptRuntime<T> : IMoteScriptRuntime
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly Context<T> _sharedVariables = new();
		private readonly Context<T> _sharedLibraries = new();
		private readonly Context<T> _managerContext;

		public MoteScriptRuntime(
			TextAsset managerSource,
			UnityEngine.Object managerOwner,
			string managerName)
		{
			MoteDecoder<T>.Setup();
			_managerContext = CreateContext();
			ManagerHost = new MoteScriptLifecycleHost<T>(
				managerSource,
				_managerContext,
				managerOwner,
				managerName,
				isManager: true);
		}

		object IMoteScriptRuntime.ManagerContext => _managerContext;
		object IMoteScriptRuntime.SharedVariables => _sharedVariables;
		object IMoteScriptRuntime.SharedLibraries => _sharedLibraries;
		public IMoteScriptLifecycleHost ManagerHost { get; }
		public IMoteScriptLifecycleHost CreateHost(
			TextAsset source,
			UnityEngine.Object owner,
			string displayName,
			bool isManager = false)
		{
			return new MoteScriptLifecycleHost<T>(
				source,
				CreateContext(),
				owner,
				displayName,
				isManager);
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
