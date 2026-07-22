using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace MoteScript
{
	[Flags]
	public enum EValueType
	{
		Unknown = 0,
		Const,
		
		String,

		/// <summary>
		/// 変数
		/// </summary>
		Variable,
		Array,
		Dictionary,
		Function,
		LoopControl,

		/// <summary>
		/// ()
		/// </summary>
		ParentheresesOpen,
		ParentheresesClose,
		/// <summary>
		/// []
		/// </summary>
		SquareBracketsOpen,
		SquareBracketsClose,
		/// <summary>
		/// {}
		/// </summary>
		CurlyBracketsOpen,
		CurlyBracketsClose,



		Operator = 0x80,
		PrimaryOpeartor = Operator,
		UnrayOperator,
		BinaryOperator,
	}

	public static class EValueTypeExtensions
	{
		public static bool IsValid(this EValueType valueType)
		{
			return valueType != EValueType.Unknown;
		}
		public static bool IsConst(this EValueType valueType)
		{
			return valueType < EValueType.Variable;
		}
		public static bool IsOperator(this EValueType valueType)
		{
			return (valueType & EValueType.Operator) != 0;
		}
		public static bool IsUnrayOperator(this EValueType valueType)
		{
			return valueType == EValueType.UnrayOperator;
		}
		public static bool IsString(this EValueType valueType)
		{
			return valueType == EValueType.String;
		}
		public static bool IsArray(this EValueType valueType)
		{
			return valueType == EValueType.Array;
		}
		public static bool IsLoopControl(this EValueType valueType)
		{
			return valueType == EValueType.LoopControl;
		}
	}
}
