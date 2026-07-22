using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoteScript
{
	public abstract class UnrayOperatorCloseBracket<T>
		: OperatorBracket<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public UnrayOperatorCloseBracket(Brackets.TypeInfo typeInfo, Brackets.EState state)
			: base(typeInfo, state)
		{
		}
	}

}