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
	using MoteList = MoteList<float>;

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

		[Test]
		public void TestNestedArray_InitializeAndAccess()
		{
			var context = new Context()
				.Set("value", 1)
				.Set("row", 1)
				.Set("column", 0);
			MoteValue script = _decoder.Decode(
				"matrix=((value,value+1),(value+2,value+3));matrix[row][column]");

			MoteValue result = script.Evaluate(context);

			Assert.AreEqual(3, result.IntegerValue);

			context
				.Set("value", 10)
				.Set("row", 0)
				.Set("column", 1);

			Assert.AreEqual(11, script.Evaluate(context).IntegerValue);
		}

		[Test]
		public void TestNestedArray_Assignment()
		{
			var context = new Context();

			MoteValue result = _decoder
				.Decode("matrix=((1,2),(3,4));matrix[1][0]=9;matrix[1][0]")
				.Evaluate(context);

			Assert.AreEqual(9, result.IntegerValue);
			Assert.AreEqual(9, context["matrix"].GetArray()[1].GetArray()[0].IntegerValue);
		}

		[Test]
		public void TestThreeDimensionalArray_InitializeAndAccess()
		{
			var context = new Context()
				.Set("value", 1)
				.Set("depth", 1)
				.Set("row", 0)
				.Set("column", 1);
			MoteValue script = _decoder.Decode(
				"cube=(((value,value+1),(value+2,value+3)),"
				+ "((value+4,value+5),(value+6,value+7)));"
				+ "cube[depth][row][column]");

			Assert.AreEqual(6, script.Evaluate(context).IntegerValue);

			context
				.Set("value", 10)
				.Set("depth", 0)
				.Set("row", 1)
				.Set("column", 0);

			Assert.AreEqual(12, script.Evaluate(context).IntegerValue);
		}

		[Test]
		public void TestThreeDimensionalArray_Assignment()
		{
			var context = new Context();

			MoteValue result = _decoder
				.Decode("cube=(((1,2),(3,4)),((5,6),(7,8)));"
					+ "cube[1][0][1]=10;cube[1][0][1]")
				.Evaluate(context);

			Assert.AreEqual(10, result.IntegerValue);
			Assert.AreEqual(10,
				context["cube"].GetArray()[1].GetArray()[0].GetArray()[1].IntegerValue);
		}

		[Test]
		public void TestNestedArray_AccessorsInCompoundExpressions()
		{
			var context = new Context();
			var patterns = new (string sentence, float result)[]
			{
				("matrix=((1,2),(3,4));matrix[0][0]+matrix[1][1]", 5),
				("matrix=((1,2),(3,4));matrix[0][1]<matrix[1][0]", 1),
				("add=(a,b)=>{a+b};matrix=((1,2),(3,4));"
					+ "add(matrix[0][0],matrix[1][1])", 5),
				("matrix=((1,2),(3,4));result=0;"
					+ "if(matrix[0][0]<matrix[1][0]){result=1};result", 1),
			};

			TestPatterns(patterns, context);
		}

		[Test]
		public void TestNestedArray_Edit()
		{
			var context = new Context();

			_decoder.Decode(
					"matrix=((1,2),(3,4));"
					+ "matrix[0].add(5);"
					+ "matrix[1].insert(1,6);"
					+ "matrix[0].removeat(0)")
				.Evaluate(context);

			MoteList firstRow = context["matrix"].GetArray()[0].GetArray();
			MoteList secondRow = context["matrix"].GetArray()[1].GetArray();
			CollectionAssert.AreEqual(new[] { 2f, 5f }, firstRow.Select(value => value.Value));
			CollectionAssert.AreEqual(new[] { 3f, 6f, 4f }, secondRow.Select(value => value.Value));
		}

		[Test]
		public void TestThreeDimensionalArray_Edit()
		{
			var context = new Context();

			_decoder.Decode(
					"cube=(((1,2),(3,4)),((5,6),(7,8)));"
					+ "cube[1][0].add(9)")
				.Evaluate(context);

			MoteList row = context["cube"].GetArray()[1].GetArray()[0].GetArray();
			CollectionAssert.AreEqual(new[] { 5f, 6f, 9f }, row.Select(value => value.Value));
		}

		[Test]
		public void TestNestedArray_CloneCreatesIndependentRows()
		{
			var context = new Context()
				.Set("value", 1);
			MoteValue initialize = _decoder.Decode(
				"matrix=((value,value+1),(value+2,value+3))");

			initialize.Evaluate(context);
			_decoder.Decode("copy=new matrix;").Evaluate(context);
			Assert.AreNotSame(
				context["matrix"].GetArray(),
				context["copy"].GetArray());
			Assert.AreNotSame(
				context["matrix"].GetArray()[0].GetArray(),
				context["copy"].GetArray()[0].GetArray());
			_decoder.Decode("copy[0][0]=9").Evaluate(context);
			Assert.AreEqual(1, context["matrix"].GetArray()[0].GetArray()[0].IntegerValue);
			Assert.AreEqual(9, context["copy"].GetArray()[0].GetArray()[0].IntegerValue);

			context.Set("value", 10);
			initialize.Evaluate(context);
			_decoder.Decode("copy=new matrix;").Evaluate(context);
			_decoder.Decode("copy[0][0]=9").Evaluate(context);
			Assert.AreEqual(10, context["matrix"].GetArray()[0].GetArray()[0].IntegerValue);
			Assert.AreEqual(9, context["copy"].GetArray()[0].GetArray()[0].IntegerValue);
		}

		[Test]
		public void TestThreeDimensionalArray_CloneCreatesIndependentDepths()
		{
			var context = new Context();

			_decoder.Decode("cube=(((1,2),(3,4)),((5,6),(7,8)));")
				.Evaluate(context);
			_decoder.Decode("copy=new cube;").Evaluate(context);
			_decoder.Decode("copy[1][0][1]=10").Evaluate(context);

			Assert.AreEqual(6,
				context["cube"].GetArray()[1].GetArray()[0].GetArray()[1].IntegerValue);
			Assert.AreEqual(10,
				context["copy"].GetArray()[1].GetArray()[0].GetArray()[1].IntegerValue);
			Assert.AreNotSame(
				context["cube"].GetArray()[1].GetArray(),
				context["copy"].GetArray()[1].GetArray());
			Assert.AreNotSame(
				context["cube"].GetArray()[1].GetArray()[0].GetArray(),
				context["copy"].GetArray()[1].GetArray()[0].GetArray());
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
