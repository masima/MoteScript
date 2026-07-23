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
		private MoteValue<T> _target;
		private string _memberName;
		private bool _isDynamicTarget;


		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			base.Finailze(rpnStack);
			BinaryOperatorDictionaryAccessor<T> accessor;
			if (Left.TryGetOperator(out accessor) && accessor._values is not null)
			{
				accessor._values.Add(Right.StringValue);
			}
			else if (Left.ValueType == EValueType.Variable)
			{
				_values = new List<string>();
				_values.Add(Left.StringValue);
				_values.Add(Right.StringValue);
				accessor = this;
			}
			else
			{
				_target = Left;
				_memberName = Right.StringValue;
				_isDynamicTarget = true;
				accessor = this;
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
			if (_isDynamicTarget)
			{
				IContext<T> targetContext = _target.EvaluteInner(context).GetDictionary();
				if (targetContext is not null
					&& targetContext.TryGetValue(_memberName, out MoteValue<T> value))
				{
					return value;
				}
				throw new InvalidOperationException($"undefined {_memberName}");
			}
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
			if (_isDynamicTarget)
			{
				IContext<T> targetContext = _target.EvaluteInner(context).GetDictionary();
				if (targetContext is null)
				{
					throw new InvalidOperationException($"undefined {_memberName}");
				}
				targetContext[_memberName] = value;
				return;
			}
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
