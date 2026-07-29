using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreeEditor;
using UnityEngine;


namespace MoteScript
{
	public abstract class UnaryOperatorOpenBracket<T>
		// : BinaryOperator<T>
		: OperatorBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public UnaryOperatorOpenBracket(Brackets.TypeInfo typeInfo, Brackets.EState state)
			: base(typeInfo, state)
		{
		}


		public bool IsCloseValue(MoteValue<T> value)
		{
			if (!value.TryGetOperator(out UnaryOperatorCloseBracket<T> closeBracket))
			{
				return false;
			}
			if (closeBracket.BracketsType == BracketsType)
			{
				return true;
			}
			return false;
		}
		public bool IsCloseBracket(UnaryOperatorCloseBracket<T> closeBracket)
		{
			if (closeBracket.BracketsType == BracketsType)
			{
				return true;
			}
			return false;
		}

		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			return Right.EvaluateInner(context);
		}

	}

	public class OperatorOpenParenthereses<T>
		: UnaryOperatorOpenBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => "(";

		public OperatorOpenParenthereses()
			: base(Brackets.Parenthereses, Brackets.EState.Open)
		{
			
		}

		public override MoteValue<T> FinalizeOperator(Stack<MoteValue<T>> rpnStack)
		{
			// return base.FinalizeOperator(rpnStack);
			Right = rpnStack.Pop();
			if (!Right.ValueType.IsValid() && rpnStack.Count == 0)
			{
				return new MoteValue<T>(new MoteList<T>());
			}
			if (0 < rpnStack.Count)
			{
				MoteValue<T> left = rpnStack.Peek();
					if (left.ValueType == EValueType.Variable
						|| left.ValueType == EValueType.Function
						|| left.TryGetOperator(out BinaryOperatorDictionaryAccessor<T> _)
						|| left.TryGetOperator(out BinaryOperatorArrayAccessor<T> _))
					{
						if (!Right.TryGetOperator(out BinaryOperatorArraySeparater<T> _))
						{
							Right = BinaryOperatorArraySeparater<T>.InstantiateSingleParameter(Right);
						}
						var functionOperator = new BinaryOperatorFunction<T>();
						functionOperator.Right = Right;
						functionOperator.Left = rpnStack.Pop();
						return new MoteValue<T>(functionOperator);
					}
				}
				return new MoteValue<T>(this);
		}

	}
	public class OperatorCloseParenthereses<T>
		: UnaryOperatorCloseBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => ")";

		public OperatorCloseParenthereses()
			: base(Brackets.Parenthereses, Brackets.EState.Close)
		{
			
		}
	}

	public class OperatorOpenSquareBracket<T>
		: UnaryOperatorOpenBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => "[";

		public OperatorOpenSquareBracket()
			: base(Brackets.SquareBrackets, Brackets.EState.Open)
		{
			
		}

		public override MoteValue<T> FinalizeOperator(Stack<MoteValue<T>> rpnStack)
		{
			// return base.FinalizeOperator(rpnStack);
			Right = rpnStack.Pop();
			if (0 < rpnStack.Count)
			{
				MoteValue<T> left = rpnStack.Peek();
				if (left.ValueType == EValueType.Variable
					|| left.TryGetOperator(out BinaryOperatorArrayAccessor<T> _))
				{
					var accessorOperator = new BinaryOperatorArrayAccessor<T>();
					accessorOperator.Right = Right;
					accessorOperator.Left = rpnStack.Pop();
					return new MoteValue<T>(accessorOperator);
				}
				if (left.ValueType == EValueType.Function)
				{
					#if VERVOSE
					Debug.Log("Function");
					#endif
				}
			}
			return new MoteValue<T>(
				new DefinitionOperatorDictionary<T>(new Context<T>(), Right));
		}
	}
	public class OperatorCloseSquareBracket<T>
		: UnaryOperatorCloseBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => "]";

		public OperatorCloseSquareBracket()
			: base(Brackets.SquareBrackets, Brackets.EState.Close)
		{
			
		}
	}

	public class OperatorOpenCurlyBracket<T>
		: UnaryOperatorOpenBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => "{";

		public OperatorOpenCurlyBracket()
			: base(Brackets.CurlyBrackets, Brackets.EState.Open)
		{
			
		}
	}
	public class OperatorCloseCurlyBracket<T>
		: UnaryOperatorCloseBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => "}";

		public OperatorCloseCurlyBracket()
			: base(Brackets.CurlyBrackets, Brackets.EState.Close)
		{
			
		}
	}



}
