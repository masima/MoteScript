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
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.ArrayAccessor;
		public override string OperatorCode => "[";


		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> left = Left.EvaluteInner(context);

			switch(left.GetObject())
			{
				case List<MoteValue<T>> array:
				{
					MoteValue<T> right = Right.EvaluteInner(context);
					int index = right.IntegerValue;
					return array[index];
				}
				case IContext<T> targetContext:
				{
					MoteValue<T> right = Right.EvaluteInner(context);
					switch (right.ValueType)
					{
						case EValueType.Const:
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
			MoteValue<T> left = Left.EvaluteInner(context);

			switch (left.GetObject())
			{
			case List<MoteValue<T>> array:
				{
					MoteValue<T> right = Right.EvaluteInner(context);
					int index = right.IntegerValue;
					array[index] = value;
					return;
				}
			case IContext<T> targetContext:
				{
					MoteValue<T> right = Right.EvaluteInner(context);
					switch (right.ValueType)
					{
					case EValueType.Const:
					case EValueType.String:
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
	}
}
