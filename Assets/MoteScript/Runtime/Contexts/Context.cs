using System;
using System.Collections.Generic;


namespace MoteScript
{
	/// <summary>
	/// デフォルトContext
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Context<T>
		: Dictionary<string, MoteValue<T>>
		, IClonableContext<T>
		, IExecutionContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		private MoteExecution<T> _currentExecution;
		MoteExecution<T> IExecutionContext<T>.CurrentExecution => _currentExecution;

		bool IExecutionContext<T>.TryAcquireExecution(MoteExecution<T> execution)
		{
			if (_currentExecution is not null
				&& !ReferenceEquals(_currentExecution, execution))
			{
				return false;
			}
			_currentExecution = execution;
			return true;
		}

		void IExecutionContext<T>.ReleaseExecution(MoteExecution<T> execution)
		{
			if (ReferenceEquals(_currentExecution, execution))
			{
				_currentExecution = null;
			}
		}

		public Context()
		{

		}
		public Context(Context<T> context) : base(context)
		{

		}


		public IContext<T> Instantiate()
		{
			return new Context<T>();
		}

		public IContext<T> Clone()
		{
			return new Context<T>(this);
		}


		public Context<T> Set(string key, T value)
		{
			this[key] = new MoteValue<T>(value);
			return this;
		}

		public MoteValue<T> GetByPath(string path)
		{
			return GetByPath(path.Split(BinaryOperatorDictionaryAccessor<T>.ConstOperationCode));
		}

		public MoteValue<T> GetByPath(IReadOnlyList<string> path)
		{
			return BinaryOperatorDictionaryAccessor<T>.GetByPath(this, path);
		}
	}
}
