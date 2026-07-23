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
	public class Decoder : MoteDecoder<float> {}
	public class Context : Context<float> {}

	public partial class MoteScriptTests
	{
		Decoder _decoder;



		/// <summary>
		/// Test開始前１度だけ実行
		/// </summary>
		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			// Operatorを登録
			Decoder.Setup();
			// 使い回し用Decoder
			_decoder = new Decoder();

			InitializeDecodedCache();
		}


		[Test]
		public void Test0_0_Simple()
		{
			// Decoder.Setup()実行後
			var decoder = new Decoder();
			float result = decoder.Decode("1+2").Evaluate(null).FloatValue;
			Assert.AreEqual(result, 3f);
		}

		[Test]
		public void TestEvaluate_LegacyEvaluteReturnsSameResult()
		{
			MoteValue script = _decoder.Decode("1+2");

			Assert.AreEqual(3, script.Evaluate(null).IntegerValue);
#pragma warning disable CS0618
			Assert.AreEqual(3, script.Evalute(null).IntegerValue);
#pragma warning restore CS0618
		}

		[Test]
		public void Test0_1_Bracket()
		{
			// Decoder.Setup()実行後
			var decoder = new Decoder();
			float result = decoder.Decode("(1+2)*3").Evaluate(null).FloatValue;
			float correctAnsower = 9f;
			Assert.AreEqual(result, correctAnsower, $"{result} != {correctAnsower}");
		}

		/// <summary>
		/// 変数を使用するサンプル
		/// </summary>
		[Test]
		public void Test0_2_Variable()
		{
			// Decoder.Setup()実行後
			var decoder = new Decoder();
			// 式の解析
			MoteValue moteValue = decoder.Decode("a+b");

			var context = new Context();
			float result;
			// 変数設定
			context
				.Set("a", 1f)
				.Set("b", 2f);
			result = 1f + 2f;
			Assert.AreEqual(moteValue.Evaluate(context).FloatValue, result);

			// 別の値設定
			context
				.Set("a", 2f)
				.Set("b", 3f);
			result = 2f + 3f;
			Assert.AreEqual(moteValue.Evaluate(context).FloatValue, result);
		}

		[Test]
		public void Test0_3_Comment()
		{
			string sentence = @"
			a=1;// comment1
			b=2;
			// comment2
			a+b
			";
			var context = new Context();
			MoteValue moteValue = _decoder.Decode(sentence);
			float result = moteValue.Evaluate(context).Value;
			Assert.AreEqual(result, 3f);
		}

		/// <summary>
		/// 変数に値を代入する
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="c"></param>
		[Test]
		public void TestVariable_Assignment(
			[Values(1,2,3,5,7,11)] float a
			, [Values(1,2,3,5,7,11)] float b
			, [Values(1,2,3,5,7,11)] float c
			)
		{
			// 変数保存場所生成
			var context = new Context();
			// 変数値設定
			float d;
			context
				.Set(nameof(a), a)
				.Set(nameof(b), b)
				.Set(nameof(c), c)
				;
			{
				var patterns = new (string sentence, float result)[]
				{
					("(d=a+b*c)+d", (d=a+b*c)+d),
				};
				TestPatterns(patterns, context);
			}

		}


		[Test]
		public void TestSentences()
		{
			var context = new Context();
			MoteValue moteValue = _decoder.Decode("a=1;b=2;a+b");
			Assert.AreEqual(moteValue.Evaluate(context).Value, 1f + 2f);
		}


		public float Convert(bool value)
		{
			return MoteValue.Convert(value);
		}

		public void TestPatterns(
			(string sentence, float result)[] patterns
			, Context context = null
			)
		{
			foreach (var pattern in patterns)
			{
				string info = $"{pattern.sentence} result:{pattern.result}";
#if CATCH_EXCEPTION
				try
#endif
				{
					MoteValue moteValue = GetDecodedMinValue(pattern.sentence);
					MoteValue resultMoteValue = moteValue.Evaluate(context);
					float result = resultMoteValue.FloatValue;
					Assert.That(
						result,
						Is.EqualTo(pattern.result).Within(0.00001f),
						info);
					UnityEngine.Debug.Log($"{info} -> {result.ToString()}");
				}
#if CATCH_EXCEPTION
				catch (Exception e)
				{
					throw new Exception(info, e);
				}
#endif
			}
		}

		public void TestPatterns(
			(string sentence, float[] result)[] patterns
			, Context context = null
			)
		{
			foreach (var pattern in patterns)
			{
				try
				{
					MoteValue moteValue = GetDecodedMinValue(pattern.sentence);
					MoteList result = moteValue.Evaluate(context).GetArray();
					Assert.AreEqual(pattern.result.Length, result.Count
						, $"{pattern.sentence} not same size:{result.Count},{pattern.result.Length}");
					for (int i = 0; i < result.Count; i++)
					{
						string info = $"{pattern.sentence} not same index:{i} value:{result[i].Value},{pattern.result[i]}";
						Assert.AreEqual(pattern.result[i], result[i].Value, info);
					}
				}
				catch (Exception e)
				{
					throw new Exception(pattern.sentence, e);
				}
			}
		}


		#region DecodedCache

		/// <summary>
		/// Decode結果をキャッシュする
		/// </summary>
		/// <returns></returns>
		private Dictionary<string, MoteValue> _sentenceCache = new();

		private void InitializeDecodedCache()
		{
			_sentenceCache.Clear();
		}
		private MoteValue GetDecodedMinValue(string sentence)
		{
			if (_sentenceCache.TryGetValue(sentence, out MoteValue moteValue))
			{
				return moteValue;
			}
			moteValue = _decoder.Decode(sentence);
			_sentenceCache[sentence] = moteValue;
			return moteValue;
		}

		#endregion
	}

}
