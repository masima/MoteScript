using System;
using System.Collections.Generic;

namespace MoteScript
{
	public class MoteList<T>
		: List<MoteValue<T>>
		, IClonableContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public MoteValue<T> this[string key]
		{
			get
			{
				throw new InvalidOperationException();
			}
			set
			{
				throw new InvalidOperationException();
			}
		}

		public IContext<T> Instantiate()
		{
			return new MoteList<T>();
		}

		public MoteList()
		{
		}
		public MoteList(MoteList<T> list) : base(list)
		{
		}

		public IContext<T> Clone()
		{
			var clone = new MoteList<T>(this);
			return clone;
		}

		// public int Count { get; }
		public bool TryGetValue(string key, out MoteValue<T> value)
		{
			switch (key)
			{
				case "add":
					value = new MoteValue<T>(Add);
					return true;
				case "clear":
					value = new MoteValue<T>(ClearList);
					return true;
				case "count":
					value = new MoteValue<T>(Count);
					return true;
				case "insert":
					value = new MoteValue<T>(Insert);
					return true;
				case "pop":
					value = new MoteValue<T>(Pop);
					return true;
				case "removeat":
					value = new MoteValue<T>(RemoveAt);
					return true;
				default:
					throw new InvalidOperationException();
			}
		}
		public bool Remove(string key)
		{
			return false;
		}
		private MoteValue<T> Add(IContext<T> context, List<MoteValue<T>> values)
		{
			foreach (MoteValue<T> value in values)
			{
				Add(value.EvaluteInner(context));
			}
			return new MoteValue<T>(this);
		}
		private MoteValue<T> ClearList(IContext<T> context, List<MoteValue<T>> values)
		{
			Clear();
			return new MoteValue<T>(this);
		}
		private MoteValue<T> Insert(IContext<T> context, List<MoteValue<T>> values)
		{
			int index = values[0].EvaluteInner(context).IntegerValue;
			for (int i = 1; i < values.Count; i++)
			{
				Insert(index + i - 1, values[i].EvaluteInner(context));
			}
			return new MoteValue<T>(this);
		}
		private MoteValue<T> RemoveAt(IContext<T> context, List<MoteValue<T>> values)
		{
			int index = values[0].EvaluteInner(context).IntegerValue;
			RemoveAt(index);
			return new MoteValue<T>(this);
		}
		private MoteValue<T> Pop(IContext<T> context, List<MoteValue<T>> values)
		{
			int lastIndex = Count - 1;
			MoteValue<T> lastValue = this[lastIndex].EvaluteInner(context);
			RemoveAt(lastIndex);
			return lastValue;
		}
	}
}
