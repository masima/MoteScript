using System;
using System.Collections.Generic;


namespace MoteScript
{
	/// <summary>
	/// 配列参照
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class BinaryOperatorArrayAccessor<T>
		: BinaryOperator<T>
		, IAssignmentTo<T>
		, IMoteReferenceSource<T>
		, IFunctionCallTarget<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.ArrayAccessor;
		public override string OperatorCode => "[";


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluateInner(context);

			switch(left.GetObject())
			{
				case List<MoteValue<T>> array:
				{
					MoteValue<T> right = Right.EvaluateInner(context);
					right.ThrowIfNotNumeric(OperatorCode);
					int index = right.IntegerValue;
					return array[index];
				}
				case IContext<T> targetContext:
				{
					MoteValue<T> right = Right.EvaluateInner(context);
					switch (right.ValueType)
					{
						case EValueType.String:
							return targetContext[right.StringValue];
						default:
							throw new InvalidOperationException($"invalid value type : {right.ValueType.ToString()}");
					}
				}
			default:
					throw new Exception($"not support type:{left.GetObject().GetType().ToString()}");
			}
		}

		public void AssignmentTo(IContext<T> context, MoteValue<T> value)
		{
			MoteValue<T> left = Left.EvaluateInner(context);

			switch (left.GetObject())
			{
			case List<MoteValue<T>> array:
				{
					MoteValue<T> right = Right.EvaluateInner(context);
					right.ThrowIfNotNumeric(OperatorCode);
					int index = right.IntegerValue;
					MoteValue<T>.ValidateAssignment(array[index], value, $"[{index}]");
					array[index] = value;
					return;
				}
			case IContext<T> targetContext:
				{
					MoteValue<T> right = Right.EvaluateInner(context);
					switch (right.ValueType)
					{
					case EValueType.String:
						if (targetContext.TryGetValue(right.StringValue, out MoteValue<T> current))
						{
							MoteValue<T>.ValidateAssignment(current, value, $"[{right.StringValue}]");
						}
						targetContext[right.StringValue] = value;
						return;
					default:
						throw new InvalidOperationException($"invalid value type : {right.ValueType.ToString()}");
					}
				}
			default:
				throw new Exception($"not support type:{left.GetObject().GetType().ToString()}");
			}
		}

		MoteValue<T> IFunctionCallTarget<T>.ResolveFunction(
			IContext<T> context,
			out IContext<T> receiver)
		{
			MoteValue<T> left = Left.EvaluateInner(context);
			receiver = left.GetDictionary();
			if (receiver is null)
			{
				throw new InvalidOperationException(
					"function receiver is not a dictionary");
			}
			MoteValue<T> right = Right.EvaluateInner(context);
			if (right.ValueType != EValueType.String)
			{
				throw new InvalidOperationException(
					$"invalid value type : {right.ValueType}");
			}
			if (receiver.TryGetValue(
				right.StringValue,
				out MoteValue<T> function))
			{
				return function;
			}
			throw new InvalidOperationException(
				$"undefined {right.StringValue}");
		}

		IMoteReference<T> IMoteReferenceSource<T>.CreateReference(
			IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluateInner(context);
			MoteValue<T> right = Right.EvaluateInner(context);
			switch (left.GetObject())
			{
				case List<MoteValue<T>> array:
				right.ThrowIfNotNumeric(OperatorCode);
				int index = right.IntegerValue;
				_ = array[index];
				return new ArrayElementMoteReference<T>(
					array,
					index);
				case IContext<T> targetContext
					when right.ValueType == EValueType.String:
				string memberName = right.StringValue;
				if (!targetContext.TryGetValue(
					memberName,
					out MoteValue<T> _))
				{
					throw new InvalidOperationException(
						$"ref requires an existing member : {memberName}");
				}
				return new DictionaryMemberMoteReference<T>(
					targetContext,
					memberName);
				default:
					throw new InvalidOperationException(
						"ref requires an existing array element or dictionary member.");
			}
		}
	}
}
