using System;

namespace MoteScript
{
	/// <summary>
	/// Performs side-effect-free optimizations on a decoded script.
	/// The source tree is not modified.
	/// </summary>
	public sealed class MoteOptimizer<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private readonly Context<T> _constantContext = new();

		public static MoteValue<T> Optimize(MoteValue<T> script)
		{
			return new MoteOptimizer<T>().OptimizeValue(script);
		}

		internal MoteValue<T> OptimizeValue(MoteValue<T> value)
		{
			if (value.TryGetOperator(out OperatorOpenParenthereses<T> parentheses))
			{
				return OptimizeValue(parentheses.Right);
			}
			if (value.TryGetOperator(out ScriptBlockOperator<T> block))
			{
				return block.Optimize(this);
			}
#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
			if (value.TryGetOperator(out DebugStatementOperator<T> debugStatement))
			{
				return debugStatement.Optimize(this);
			}
#endif
			if (value.TryGetOperator(out BinaryOperatorAssignment<T> assignment))
			{
				MoteValue<T> right = OptimizeValue(assignment.Right);
				if (IsSameValue(right, assignment.Right)) return value;
				return new MoteValue<T>(new BinaryOperatorAssignment<T>
				{
					Left = assignment.Left,
					Right = right,
				});
			}
			if (value.TryGetOperator(out FlowControlOperator<T> flow))
			{
				return OptimizeFlow(value, flow);
			}
			if (value.TryGetOperator(out BinaryOperator<T> binary)
				&& IsFoldable(binary))
			{
				return OptimizeBinary(value, binary);
			}
			return value;
		}

		private MoteValue<T> OptimizeFlow(
			MoteValue<T> original,
			FlowControlOperator<T> flow)
		{
			MoteValue<T> judge = OptimizeValue(flow.Judge);
			MoteValue<T> statement = OptimizeValue(flow.Statement);
			if (IsSameValue(judge, flow.Judge)
				&& IsSameValue(statement, flow.Statement))
			{
				return original;
			}
			var copy = (FlowControlOperator<T>)Activator.CreateInstance(flow.GetType());
			copy.Judge = judge;
			copy.Statement = statement;
			return new MoteValue<T>(copy);
		}

		private MoteValue<T> OptimizeBinary(
			MoteValue<T> original,
			BinaryOperator<T> binary)
		{
			MoteValue<T> left = OptimizeValue(binary.Left);

			// Preserve runtime short-circuit behavior, including suppressed errors.
			if (binary is BinaryOperatorLogicalConjunction<T>
				&& left.ValueType == EValueType.Const
				&& !left.ToBool())
			{
				return new MoteValue<T>(false);
			}
			if (binary is BinaryOperatorLogicalDisjunction<T>
				&& left.ValueType == EValueType.Const
				&& left.ToBool())
			{
				return new MoteValue<T>(true);
			}

			MoteValue<T> right = OptimizeValue(binary.Right);
			var copy = (BinaryOperator<T>)Activator.CreateInstance(binary.GetType());
			copy.Left = left;
			copy.Right = right;

			if (IsCompileTimeValue(left)
				&& IsCompileTimeValue(right))
			{
				// Constant-expression errors intentionally surface during optimization.
				return copy.Evaluate(_constantContext);
			}
			if (IsSameValue(left, binary.Left)
				&& IsSameValue(right, binary.Right))
			{
				return original;
			}
			return new MoteValue<T>(copy);
		}

		private static bool IsFoldable(BinaryOperator<T> binary)
		{
			return binary is BinaryOperatorPlus<T>
				|| binary is BinaryOperatorMinus<T>
				|| binary is BinaryOperatorMultiple<T>
				|| binary is BinaryOperatorDivide<T>
				|| binary is BinaryOperatorMod<T>
				|| binary is BinaryOperatorEqual<T>
				|| binary is BinaryOperatorNotEqual<T>
				|| binary is BinaryOperatorLessThan<T>
				|| binary is BinaryOperatorLessThanOrEqualTo<T>
				|| binary is BinaryOperatorGreaterThan<T>
				|| binary is BinaryOperatorGreaterThanOrEqualTo<T>
				|| binary is BinaryOperatorLogicalConjunction<T>
				|| binary is BinaryOperatorLogicalDisjunction<T>;
		}

		private static bool IsCompileTimeValue(MoteValue<T> value)
		{
			return value.ValueType == EValueType.Const
				|| value.ValueType == EValueType.String;
		}

		private static bool IsSameValue(MoteValue<T> left, MoteValue<T> right)
		{
			if (left.ValueType != right.ValueType) return false;
			if (left.ValueType == EValueType.Const) return left.Value.Equals(right.Value);
			return ReferenceEquals(left.GetObject(), right.GetObject());
		}
	}
}
