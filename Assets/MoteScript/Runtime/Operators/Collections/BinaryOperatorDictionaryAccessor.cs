using System;
using System.Collections.Generic;

namespace MoteScript
{
	public class BinaryOperatorDictionaryAccessor<T>
		: BinaryOperator<T>
		, IOperatorOnFinalized
		, IAssignmentTo<T>
		, IMoteReferenceSource<T>
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


		public override MoteValue<T> FinalizeOperator(Stack<MoteValue<T>> rpnStack)
		{
			base.FinalizeOperator(rpnStack);
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

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			if (_isDynamicTarget)
			{
				IContext<T> targetContext = _target.EvaluateInner(context).GetDictionary();
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
				IContext<T> targetContext = _target.EvaluateInner(context).GetDictionary();
				if (targetContext is null)
				{
					throw new InvalidOperationException($"undefined {_memberName}");
				}
				if (targetContext.TryGetValue(_memberName, out MoteValue<T> current))
				{
					MoteValue<T>.ValidateAssignment(current, value, _memberName);
				}
				targetContext[_memberName] = value;
				return;
			}
			IContext<T> dictionary = null;
			string contextKey = _splitedPath[0];
			if (context.TryGetValue(contextKey, out MoteValue<T> moteValue))
			{
				dictionary = moteValue.GetDictionary();
				if (dictionary is null)
				{
					MoteValue<T>.ValidateAssignment(
						moteValue,
						new MoteValue<T>(context.Instantiate()),
						contextKey);
				}
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
					if (dictionary is null)
					{
						MoteValue<T>.ValidateAssignment(
							moteValue,
							new MoteValue<T>(context.Instantiate()),
							key);
					}
				}
				else
				{
					var childDictionary = context.Instantiate();
					dictionary[key] = new MoteValue<T>(childDictionary);
					dictionary = childDictionary;
				}
			}
			string memberName = _splitedPath[lastIndex];
			if (dictionary.TryGetValue(memberName, out MoteValue<T> currentValue))
			{
				MoteValue<T>.ValidateAssignment(currentValue, value, memberName);
			}
			dictionary[memberName] = value;
		}

		IMoteReference<T> IMoteReferenceSource<T>.CreateReference(
			IContext<T> context)
		{
			if (_isDynamicTarget)
			{
				IContext<T> targetContext =
					_target.EvaluateInner(context).GetDictionary();
				return CreateMemberReference(
					targetContext,
					_memberName);
			}

			IContext<T> dictionary = context;
			int lastIndex = _splitedPath.Length - 1;
			for (int i = 0; i < lastIndex; ++i)
			{
				if (!dictionary.TryGetValue(
					_splitedPath[i],
					out MoteValue<T> child))
				{
					throw new InvalidOperationException(
						$"ref requires an existing member : {string.Join(ConstOperationCode, _splitedPath)}");
				}
				dictionary = child.GetDictionary();
				if (dictionary is null)
				{
					throw new InvalidOperationException(
						$"ref target is not a dictionary : {_splitedPath[i]}");
				}
			}
			return CreateMemberReference(
				dictionary,
				_splitedPath[lastIndex]);
		}

		private static IMoteReference<T> CreateMemberReference(
			IContext<T> dictionary,
			string memberName)
		{
			if (dictionary is null
				|| !dictionary.TryGetValue(
					memberName,
					out MoteValue<T> _))
			{
				throw new InvalidOperationException(
					$"ref requires an existing member : {memberName}");
			}
			return new DictionaryMemberMoteReference<T>(
				dictionary,
				memberName);
		}
	}


}
