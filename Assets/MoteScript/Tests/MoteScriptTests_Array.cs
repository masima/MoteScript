using System.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using MoteScript;

namespace MoteScript.Tests
{
	using MoteValue = MoteValue<float>;

	public partial class MoteScriptTests
	{
		/// <summary>
		/// 配列参照
		/// </summary>
		/// <param name="index"></param>
		[Test]
		public void TestArray(
			[Values(0, 1, 2)] float index
			)
		{
			// 変数保存場所生成
			var context = new Context();
			context.Set("index", index);
			{
				var patterns = new (string sentence, float result)[]
				{
					// 初期化と参照
					("a=(1,2,3);a[index]", (new float[] {1, 2, 3})[(int)index]),
					("a=(index,index*2,index*3);a[index]", (new float[] {index, index * 2, index * 3})[(int)index]),
					// 値の設定
					("a=(1,2,3);a[index]=4;a[index]", 4),
				};
				TestPatterns(patterns, context);
			}
		}

		[Test]
		public void TestArray_Edit()
		{
			// 変数保存場所生成
			var context = new Context();
			{
				var patterns = new (string sentence, float[] result)[]
				{
					("a=();a.add(1);a.add(2,3)", new float[] {1,2,3}),

					("a=(1,2,3);a.add(4)", new float[] {1,2,3,4}),
					("a=(1,2,3);a.clear()", new float[] {}),

					("a=(1,2,3);a.insert(0,4)", new float[] {4,1,2,3}),
					("a=(1,2,3);a.insert(1,4)", new float[] {1,4,2,3}),
					("a=(1,1+1,3);a.insert(1,2+2)", new float[] {1,2+2,1+1,3}),
					("a=1;b=2;c=3;array=(a,a+a,c);array.insert(a,b+b)", new float[] {1,2+2,1+1,3}),

					("a=(1,2,3);a.removeat(1)", new float[] {1,3}),
					("a=(1,2,3);(a.pop(),a.pop(),a.pop())", new float[] {3,2,1}),
					("a=(1,2,3);(a.pop()+1,a.pop()+2,a.pop()*3)", new float[] {3+1,2+2,1*3}),
				};

				TestPatterns(patterns, context);
			}
		}

		/// <summary>
		/// 配列クローン
		/// </summary>
		[Test]
		public void TestArray_Clone(
			[Values(0, 1, 2)] float value1
			, [Values(0, 1, 2)] float value2
			)
		{
			// 変数保存場所生成
			var context = new Context();
			context
				.Set(nameof(value1), value1)
				.Set(nameof(value2), value2)
				;

			var patterns = new (string sentence, float result)[]
			{
				//// 配列定義は実行の度に初期化される同一インスタンス
				//(@"
				//	reset = () => {(value1,value2)};
				//	a = reset();
				//	a[0] = 2;
				//	b = reset();
				//	a[0] + b[0]", value1+value1),

				//// newでクローンを生成
				//(@"
				//	clone = () => {new (value1,value2)};
				//	a = clone();
				//	a[0] = 2;
				//	b = clone();
				//	a[0] + b[0]", 2+value1),

				//// newを使用しない場合、評価後のインスタンスも共有される
				//(@"
				//	reset = () => {(v1+v2,value2)};
				//	v1 = value1;
				//	v2 = value2;
				//	a = reset();
				//	v1 = 3;
				//	b = reset();
				//	a[0]+b[0]", (3+value2)*2),

				//// 演算結果をnewする
				//(@"
				//	reset = () => {(v1+v2,value2)};
				//	v1 = value1;
				//	v2 = value2;
				//	a = new(reset());
				//	v1 = 3;
				//	b = reset();
				//	a[0]+b[0]", value1 + value2 + (3+value2)),

				(@"
					reset = () => {(value1,value2)};
					a = new reset();
					a[0]", value1),
			};
			TestPatterns(patterns, context);
		}

	}
}
