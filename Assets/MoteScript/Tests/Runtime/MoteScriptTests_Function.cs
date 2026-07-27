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
		[Test]
		public void TestFunction_RestoresParametersAfterReturn()
		{
			MoteValue script = _decoder.Decode(
				"function=(value)=>{return value+1};function(2)");
			var context = new Context().Set("value", 10);

			Assert.AreEqual(3, script.Evaluate(context).IntegerValue);
			Assert.AreEqual(10, context["value"].IntegerValue);
		}

		[Test]
		public void TestFunction_RestoresParametersAfterException()
		{
			MoteValue script = _decoder.Decode(
				"function=(value)=>{missing};function(2)");
			var context = new Context().Set("value", 10);

			Assert.Throws<InvalidOperationException>(() => script.Evaluate(context));
			Assert.AreEqual(10, context["value"].IntegerValue);
		}

		[Test]
		public void TestFunction_RemovesTemporaryParameterAfterCall()
		{
			MoteValue script = _decoder.Decode("function=(value)=>{value+1};function(2)");
			var context = new Context();

			Assert.AreEqual(3, script.Evaluate(context).IntegerValue);
			Assert.IsFalse(context.ContainsKey("value"));
		}

		[Test]
		public void TestFunction_SupportsNestedCalls()
		{
			MoteValue script = _decoder.Decode(
				"double=(value)=>{value*2};increment=(value)=>{double(value)+1};increment(3)");
			var context = new Context();

			Assert.AreEqual(7, script.Evaluate(context).IntegerValue);
			Assert.IsFalse(context.ContainsKey("value"));
		}

		[Test]
		public void TestFunction_SupportsRecursion()
		{
			MoteValue script = _decoder.Decode(
				"factorial=(value)=>{if(value<=1){return 1};return factorial(value-1)*value};factorial(5)");
			var context = new Context().Set("value", 10);

			Assert.AreEqual(120, script.Evaluate(context).IntegerValue);
			Assert.AreEqual(10, context["value"].IntegerValue);
		}

		[Test]
		public void TestFunction_LocalVariablesDoNotLeak()
		{
			MoteValue script = _decoder.Decode(
				"calculate=(value;local doubled,result)=>{doubled=value*2;result=doubled+1;return result};calculate(4)");
			var context = new Context();

			Assert.AreEqual(9, script.Evaluate(context).IntegerValue);
			Assert.IsFalse(context.ContainsKey("doubled"));
			Assert.IsFalse(context.ContainsKey("result"));
		}

		[Test]
		public void TestFunction_LocalVariablesShadowAndRestoreContextValues()
		{
			MoteValue script = _decoder.Decode(
				"function=(value;local result)=>{result=value*2;return result};function(5)");
			var context = new Context().Set("result", 100);

			Assert.AreEqual(10, script.Evaluate(context).IntegerValue);
			Assert.AreEqual(100, context["result"].IntegerValue);
		}

		[Test]
		public void TestFunction_LocalVariablesRestoreAfterException()
		{
			MoteValue script = _decoder.Decode(
				"function=(;local temporary)=>{temporary=20;missing};function()");
			var context = new Context().Set("temporary", 10);

			Assert.Throws<InvalidOperationException>(() => script.Evaluate(context));
			Assert.AreEqual(10, context["temporary"].IntegerValue);
		}

		[Test]
		public void TestFunction_UnassignedLocalDoesNotReadGlobalValue()
		{
			MoteValue script = _decoder.Decode(
				"function=(;local value)=>{return value};function()");
			var context = new Context().Set("value", 10);

			Assert.Throws<InvalidOperationException>(() => script.Evaluate(context));
			Assert.AreEqual(10, context["value"].IntegerValue);
		}

		[Test]
		public void TestFunction_LocalVariablesSupportRecursion()
		{
			MoteValue script = _decoder.Decode(
				"sum=(value;local current)=>{current=value;if(value<=1){return current};return sum(value-1)+current};sum(4)");
			var context = new Context().Set("current", 100);

			Assert.AreEqual(10, script.Evaluate(context).IntegerValue);
			Assert.AreEqual(100, context["current"].IntegerValue);
		}

		[TestCase("function=(value;value)=>{value}")]
		[TestCase("function=(value;local)=>{value}")]
		[TestCase("function=(value;local value)=>{value}")]
		[TestCase("function=(value,value)=>{value}")]
		[TestCase("function=(value;local temporary,temporary)=>{value}")]
		[TestCase("function=(value;local 1temporary)=>{value}")]
		public void TestFunction_RejectsInvalidLocalDeclarations(string sentence)
		{
			Assert.Throws<MoteScriptParseException>(() => _decoder.Decode(sentence));
		}

		[Test]
		public void TestCustomFunction(
			[Values(1,2,3,5,7,11)] float a
			, [Values(1,2,3,5,7,11)] float b
			, [Values(1,2,3,5,7,11)] float c
			)
		{
			var context = new Context();

			// 関数登録
			context["sum"] = new MoteValue(Sum);
			// 変数定義
			context
				.Set(nameof(a), a)
				.Set(nameof(b), b)
				.Set(nameof(c), c)
				;
			{
				var patterns = new (string sentence, float result)[]
				{
					("sum(1,2,3)", new float[] {1,2,3}.Sum()),
					("sum(a+b+c)", new float[] {a,b,c}.Sum()),
					("sum(a,b,c)", new float[] {a,b,c}.Sum()),
					("sum(a,a+b,b*c)", new float[] {a,a+b,b*c}.Sum()),
				};
				TestPatterns(patterns, context);
			}
		}
		/// <summary>
		/// 合計値を求める
		/// </summary>
		/// <param name="context"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		private static MoteValue Sum(IContext<float> context, List<MoteValue> parameters)
		{
			float sum = 0;
			foreach	(var value in parameters)
			{
				sum += value.Evaluate(context).Value;
			}
			return new MoteValue(sum);
		}

		/// <summary>
		/// delegateテスト
		/// </summary>
		/// <param name="index"></param>
		[Test]
		public void TestFunction_Delegate(
			[Values(0, 1, 2)] float a
			,[Values(0, 1, 2)] float b
			)
		{
			// 変数保存場所生成
			var context = new Context();
			context.Set(nameof(a), a);
			context.Set(nameof(b), b);
			var patterns = new (string sentence, float result)[]
			{
				// delegateの定義と実行
				("addValue=(x,y)=>{x+y};addValue(a,b)", a+b),
				// delegate引数のローカル状態テスト
				("addValue=(a)=>{a=a*2};addValue(a)", a*2),
				("addValue=(a)=>{a=a*2};addValue(a);a", a),
				("x=1;y=2;addValue=(x,y)=>{x+y};addValue(a,b);x+y", 1+2),
			};
			TestPatterns(patterns, context);
		}
	}
}
