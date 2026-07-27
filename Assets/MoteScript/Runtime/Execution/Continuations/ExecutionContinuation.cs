using System;

namespace MoteScript
{
	internal readonly struct ExecutionContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly IExecutionContinuation<T> _target;
		private readonly int _state;

		public ExecutionContinuation(IExecutionContinuation<T> target, int state)
		{
			_target = target;
			_state = state;
		}

		public MoteValue<T> Resume(IContext<T> context, MoteValue<T> result)
		{
			return _target.Resume(context, result, _state);
		}

		public void Cancel(IContext<T> context)
		{
			_target.Cancel(context);
		}
	}
}
