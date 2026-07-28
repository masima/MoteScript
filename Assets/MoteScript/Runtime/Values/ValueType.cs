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
		Yield,
		Reference = 0x40,

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
		UnaryOperator,
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
		public static bool IsUnaryOperator(this EValueType valueType)
		{
			return valueType == EValueType.UnaryOperator;
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
		public static bool IsYield(this EValueType valueType)
		{
			return valueType == EValueType.Yield;
		}
		public static bool IsExecutionControl(this EValueType valueType)
		{
			return valueType.IsLoopControl() || valueType.IsYield();
		}
	}
}
