using System;

namespace MoteScript
{
	public interface IExecutionContext<T> : IContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		MoteExecution<T> CurrentExecution { get; }
		bool TryAcquireExecution(MoteExecution<T> execution);
		void ReleaseExecution(MoteExecution<T> execution);
	}
}
