using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoteScript
{
	public abstract class UnaryOperatorCloseBracket<T>
		: OperatorBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public UnaryOperatorCloseBracket(Brackets.TypeInfo typeInfo, Brackets.EState state)
			: base(typeInfo, state)
		{
		}
	}

}
