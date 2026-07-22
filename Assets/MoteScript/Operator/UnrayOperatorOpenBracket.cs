using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreeEditor;
using UnityEngine;


namespace MoteScript
{
	public abstract class UnrayOperatorOpenBracket<T>
		// : BinaryOperator<T>
		: OperatorBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public UnrayOperatorOpenBracket(Brackets.TypeInfo typeInfo, Brackets.EState state)
			: base(typeInfo, state)
		{
		}


		public bool IsCloseValue(MoteValue<T> value)
		{
			if (!value.TryGetOperator(out UnrayOperatorCloseBracket<T> closeBracket))
			{
				return false;
			}
			if (closeBracket.BracketsType == BracketsType)
			{
				return true;
			}
			return false;
		}
		public bool IsCloseBracket(UnrayOperatorCloseBracket<T> closeBracket)
		{
			if (closeBracket.BracketsType == BracketsType)
			{
				return true;
			}
			return false;
		}

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			return Right.EvaluteInner(context);
		}

	}

	public class OperatorOpenParenthereses<T>
		: UnrayOperatorOpenBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => "(";

		public OperatorOpenParenthereses()
			: base(Brackets.Parenthereses, Brackets.EState.Open)
		{
			
		}

		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			// return base.Finailze(rpnStack);
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
						|| left.TryGetOperator(out BinaryOperatorDictionaryAccessor<T> _))
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
		: UnrayOperatorCloseBracket<T>
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
		: UnrayOperatorOpenBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override string OperatorCode => "[";

		public OperatorOpenSquareBracket()
			: base(Brackets.SquareBrackets, Brackets.EState.Open)
		{
			
		}

		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			// return base.Finailze(rpnStack);
			Right = rpnStack.Pop();
			if (0 < rpnStack.Count)
			{
				MoteValue<T> left = rpnStack.Peek();
				switch (left.ValueType)
				{
					case EValueType.Function:
						#if VERVOSE
						Debug.Log("Function");
						#endif
						break;
					case EValueType.Variable:
					{
						var functionOperator = new BinaryOperatorArrayAccessor<T>();
						functionOperator.Right = Right;
						functionOperator.Left = rpnStack.Pop();
						return new MoteValue<T>(functionOperator);
					}
				}
			}
			return new MoteValue<T>(
				new DefinitionOperatorDictionary<T>(new Context<T>(), Right));
		}
	}
	public class OperatorCloseSquareBracket<T>
		: UnrayOperatorCloseBracket<T>
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
		: UnrayOperatorOpenBracket<T>
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
		: UnrayOperatorCloseBracket<T>
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
