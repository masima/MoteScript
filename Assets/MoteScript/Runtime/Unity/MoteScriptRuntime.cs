using System;

namespace MoteScript.Unity
{
	internal interface IMoteScriptRuntime
	{
		object ManagerContext { get; }
		object SharedVariables { get; }
		object SharedLibraries { get; }
		object CreateHostContext();
	}

	internal sealed class MoteScriptRuntime<T> : IMoteScriptRuntime
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly Context<T> _sharedVariables = new();
		private readonly Context<T> _sharedLibraries = new();
		private readonly Context<T> _managerContext;

		public MoteScriptRuntime()
		{
			MoteDecoder<T>.Setup();
			_managerContext = CreateContext();
		}

		object IMoteScriptRuntime.ManagerContext => _managerContext;
		object IMoteScriptRuntime.SharedVariables => _sharedVariables;
		object IMoteScriptRuntime.SharedLibraries => _sharedLibraries;
		object IMoteScriptRuntime.CreateHostContext() => CreateContext();

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
