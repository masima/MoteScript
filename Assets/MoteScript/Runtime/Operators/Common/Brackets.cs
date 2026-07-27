using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoteScript
{
	public static class Brackets
	{
		public enum EType
		{
			Parenthereses,
			SquareBrackets,
			CurlyBrackets,
		}
		public enum EState
		{
			Open,
			Close,
		}
		public class TypeInfo
		{
			public EType Type;
			public char Open;
			public char Close;

			public char GetCode(EState state)
			{
				if (state == EState.Open)
				{
					return Open;
				}
				return Close;
			}
		}

		public static readonly TypeInfo Parenthereses = new()
		{
			Type = EType.Parenthereses,
			Open = '(', Close = ')'
		};
		public static readonly TypeInfo SquareBrackets = new()
		{
			Type = EType.SquareBrackets,
			Open = '[', Close = ']'
		};
		public static readonly TypeInfo CurlyBrackets = new()
		{
			Type = EType.CurlyBrackets,
			Open = '{', Close = '}'
		};
		public static readonly TypeInfo[] TypeInfos = new[]
		{
			Parenthereses,
			SquareBrackets,
			CurlyBrackets,
		};

		public static bool TryGetOpenParentheresesDefine(char c, out TypeInfo define)
		{
			foreach	(var pdef in TypeInfos)
			{
				if (pdef.Open == c)
				{
					define = pdef;
					return true;
				}
			}
			define = null;
			return false;
		}
	}

}