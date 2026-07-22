using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreeEditor;
using UnityEngine;


namespace MoteScript
{
	public abstract class OperatorBracket<T>
		: UnrayOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Bracket;

		private Brackets.TypeInfo _bracketTypeInfo;
		private Brackets.EState _bracketState;
		public Brackets.EType BracketsType => _bracketTypeInfo.Type;
		public Brackets.EState State => _bracketState;


		public OperatorBracket(Brackets.TypeInfo typeInfo, Brackets.EState state)
		{
			_bracketTypeInfo = typeInfo;
			_bracketState = state;
		}

		public override MoteValue<T> Evalute(IContext<T> context)
		{
			return Right.EvaluteInner(context);
		}

	}

}
