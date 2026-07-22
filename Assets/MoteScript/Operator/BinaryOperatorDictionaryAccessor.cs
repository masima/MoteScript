using System;
using System.Collections.Generic;

namespace MoteScript
{
	public class BinaryOperatorDictionaryAccessor<T>
		: BinaryOperator<T>
		, IOperatorOnFinalized
		, IAssignmentTo<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public const string ConstOperationCode = ".";
		public override OperatorType OperatorType => OperatorType.DictionaryAccessor;
		public override string OperatorCode => ConstOperationCode;

		private List<string> _values;
		private string[] _splitedPath;


		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			base.Finailze(rpnStack);
			if (!Left.TryGetOperator(out BinaryOperatorDictionaryAccessor<T> accessor))
			{
				_values = new List<string>();
				_values.Add(Left.StringValue);
				_values.Add(Right.StringValue);
				accessor = this;
			}
			else
			{
				accessor._values.Add(Right.StringValue);
			}
			Left = MoteValue<T>.Default;
			Right = MoteValue<T>.Default;
			return new MoteValue<T>(accessor);
		}

		public bool IsOnFinishedRequired => _values is not null;
		public void OnFinalized()
		{
			_splitedPath = _values.ToArray();
			_values = null;
		}

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			return GetByPath(context, _splitedPath);
		}
		public static MoteValue<T> GetByPath(IContext<T> context, IReadOnlyList<string> hierarchy)
		{
			MoteValue<T> moteValue = default;
			int lastIndex = hierarchy.Count - 1;
			for	(int i = 0; i < lastIndex; i++)
			{
				if (context.TryGetValue(hierarchy[i], out MoteValue<T> childValue))
				{
					moteValue = childValue;
					context = moteValue.GetDictionary();
				}
				else
				{
					throw new InvalidOperationException($"undefined {string.Join(ConstOperationCode, hierarchy)}");
				}
			}
			if (context.TryGetValue(hierarchy[lastIndex], out moteValue))
			{
				return moteValue;
			}
			throw new InvalidOperationException($"undefined {string.Join(ConstOperationCode, hierarchy)}");
		}


		public void AssignmentTo(IContext<T> context, MoteValue<T> value)
		{
			IContext<T> dictionary = null;
			string contextKey = _splitedPath[0];
			if (context.TryGetValue(contextKey, out MoteValue<T> moteValue))
			{
				dictionary = moteValue.GetDictionary();
			}
			if (dictionary is null)
			{
				dictionary = context.Instantiate();
				moteValue = new MoteValue<T>(dictionary);
				context[contextKey] = moteValue;
			}

			int lastIndex = _splitedPath.Length - 1;
			for (int i = 1; i < lastIndex; i++)
			{
				string key = _splitedPath[i];
				if (dictionary.TryGetValue(key, out MoteValue<T> childValue))
				{
					moteValue = childValue;
					dictionary = moteValue.GetDictionary();
				}
				else
				{
					var childDictionary = context.Instantiate();
					dictionary[key] = new MoteValue<T>(childDictionary);
					dictionary = childDictionary;
				}
			}
			dictionary[_splitedPath[lastIndex]] = value;
		}
	}


}
