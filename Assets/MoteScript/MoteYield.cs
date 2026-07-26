using System;
using System.Collections.Generic;

namespace MoteScript
{
	internal interface IYieldContinuation<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		MoteValue<T> Resume(IContext<T> context, MoteValue<T> result);
		void Cancel(IContext<T> context);
	}

	public sealed class MoteYield<T> : IExecutionControl
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly IContext<T> _context;
		private List<IYieldContinuation<T>> _continuations;
		private bool _resumed;

		internal MoteYield(MoteValue<T> value, IContext<T> context)
		{
			Value = value;
			_context = context;
		}

		public MoteValue<T> Value { get; }

		internal void AddContinuation(IYieldContinuation<T> continuation)
		{
			(_continuations ??= new List<IYieldContinuation<T>>(4)).Add(continuation);
		}

		public MoteValue<T> Evaluate()
		{
			if (_resumed)
			{
				throw new InvalidOperationException("A yielded execution can only be resumed once.");
			}
			_resumed = true;

			MoteValue<T> result = default;
			int index = 0;
			try
			{
				if (_continuations is null)
				{
					return result;
				}
				for (; index < _continuations.Count; ++index)
				{
					result = _continuations[index].Resume(_context, result);
					if (result.TryGetYield(out MoteYield<T> nextYield))
					{
						for (int remaining = index + 1; remaining < _continuations.Count; ++remaining)
						{
							nextYield.AddContinuation(_continuations[remaining]);
						}
						return result;
					}
				}
				return result;
			}
			catch
			{
				for (; index < (_continuations?.Count ?? 0); ++index)
				{
					_continuations[index].Cancel(_context);
				}
				throw;
			}
			finally
			{
				_continuations = null;
			}
		}
	}
}
