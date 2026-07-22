using System;
using System.Collections.Generic;


namespace MoteScript
{
	public interface IContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public MoteValue<T> this[string key]
		{
			get;
			set;
		}

		public IContext<T> Instantiate();
		public int Count { get; }
		public bool TryGetValue(string key, out MoteValue<T> value);
		public bool Remove(string key);
		public void Clear();
	}


	public interface IClonableContext<T>
		: IContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public IContext<T> Clone();
	}

	/// <summary>
	/// デフォルトContext
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Context<T>
		: Dictionary<string, MoteValue<T>>
		, IClonableContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
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
