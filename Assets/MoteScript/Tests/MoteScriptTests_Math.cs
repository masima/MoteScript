using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[Test]
		public void TestMath_FloatBasicFunctions()
		{
			var context = new Context();
			StandardMathLibrary<float>.Register(context);

			Assert.AreEqual(3f, EvaluateMathFloat("math.abs(-3)", context));
			Assert.AreEqual(2f, EvaluateMathFloat("math.min(2,5)", context));
			Assert.AreEqual(5f, EvaluateMathFloat("math.max(2,5)", context));
			Assert.AreEqual(3f, EvaluateMathFloat("math.clamp(5,1,3)", context));
			Assert.AreEqual(-1f, EvaluateMathFloat("math.sign(-10)", context));
			Assert.AreEqual(8f, EvaluateMathFloat("math.pow(2,3)", context));
		}

		[Test]
		public void TestMath_FloatTranscendentalFunctionsAndPi()
		{
			var context = new Context();
			StandardMathLibrary<float>.Register(context);

			Assert.AreEqual((float)Math.PI, EvaluateMathFloat("math.pi()", context));
			Assert.AreEqual(3f, EvaluateMathFloat("math.sqrt(9)", context));
			Assert.AreEqual((float)Math.E, EvaluateMathFloat("math.exp(1)", context), 0.00001f);
			Assert.AreEqual(1f, EvaluateMathFloat("math.log(math.exp(1))", context), 0.00001f);
			Assert.AreEqual(3f, EvaluateMathFloat("math.log(8,2)", context), 0.00001f);
			Assert.AreEqual(2f, EvaluateMathFloat("math.log10(100)", context), 0.00001f);
			Assert.AreEqual(1f, EvaluateMathFloat("math.sin(math.pi()/2)", context), 0.00001f);
			Assert.AreEqual(1f, EvaluateMathFloat("math.cos(0)", context), 0.00001f);
			Assert.AreEqual(1f, EvaluateMathFloat("math.tan(math.pi()/4)", context), 0.00001f);
			Assert.AreEqual(
				(float)(Math.PI / 2),
				EvaluateMathFloat("math.asin(1)", context),
				0.00001f);
			Assert.AreEqual(0f, EvaluateMathFloat("math.acos(1)", context), 0.00001f);
			Assert.AreEqual(
				(float)(Math.PI / 4),
				EvaluateMathFloat("math.atan(1)", context),
				0.00001f);
			Assert.AreEqual(
				(float)(Math.PI / 4),
				EvaluateMathFloat("math.atan2(1,1)", context),
				0.00001f);
		}

		[Test]
		public void TestMath_FloatRoundingFunctions()
		{
			var context = new Context();
			StandardMathLibrary<float>.Register(context);

			Assert.AreEqual(1f, EvaluateMathFloat("math.floor(1.9)", context));
			Assert.AreEqual(2f, EvaluateMathFloat("math.ceiling(1.1)", context));
			Assert.AreEqual(2f, EvaluateMathFloat("math.ceil(1.1)", context));
			Assert.AreEqual(1.23f, EvaluateMathFloat("math.round(1.234,2)", context));
			Assert.AreEqual(-1f, EvaluateMathFloat("math.truncate(-1.9)", context));
		}

		[Test]
		public void TestMath_DecimalFunctions()
		{
			MoteDecoder<decimal>.Setup();
			var decoder = new MoteDecoder<decimal>();
			var context = new Context<decimal>();
			StandardMathLibrary<decimal>.Register(context);

			Assert.AreEqual(3.5m, decoder.Decode("math.abs(-3.5)").Evaluate(context).Value);
			Assert.AreEqual(2.34m, decoder.Decode("math.round(2.345,2)").Evaluate(context).Value);
			Assert.AreEqual(2m, decoder.Decode("math.floor(2.9)").Evaluate(context).Value);
			Assert.AreEqual(3m, decoder.Decode("math.ceiling(2.1)").Evaluate(context).Value);
			Assert.AreEqual(
				(decimal)Math.PI,
				decoder.Decode("math.pi()").Evaluate(context).Value);
			Assert.AreEqual(
				3m,
				decoder.Decode("math.sqrt(9)").Evaluate(context).Value);
		}

		[Test]
		public void TestMath_IntegerSupportedFunctions()
		{
			MoteDecoder<int>.Setup();
			var decoder = new MoteDecoder<int>();
			var context = new Context<int>();
			StandardMathLibrary<int>.Register(context);

			Assert.AreEqual(3, decoder.Decode("math.abs(-3)").Evaluate(context).Value);
			Assert.AreEqual(2, decoder.Decode("math.min(2,5)").Evaluate(context).Value);
			Assert.AreEqual(5, decoder.Decode("math.max(2,5)").Evaluate(context).Value);
			Assert.AreEqual(3, decoder.Decode("math.clamp(5,1,3)").Evaluate(context).Value);
			Assert.AreEqual(-1, decoder.Decode("math.sign(-3)").Evaluate(context).Value);
			Assert.AreEqual(32, decoder.Decode("math.pow(2,5)").Evaluate(context).Value);
			Assert.AreEqual(3, decoder.Decode("math.floor(3)").Evaluate(context).Value);
			Assert.AreEqual(3, decoder.Decode("math.round(3)").Evaluate(context).Value);
		}

		[TestCase("math.sqrt(4)")]
		[TestCase("math.exp(1)")]
		[TestCase("math.log(1)")]
		[TestCase("math.log10(10)")]
		[TestCase("math.sin(1)")]
		[TestCase("math.cos(1)")]
		[TestCase("math.tan(1)")]
		[TestCase("math.asin(1)")]
		[TestCase("math.acos(1)")]
		[TestCase("math.atan(1)")]
		[TestCase("math.atan2(1,1)")]
		[TestCase("math.pi()")]
		[TestCase("math.pow(2,-1)")]
		public void TestMath_IntegerUnsupportedFunctionsThrow(string sentence)
		{
			MoteDecoder<int>.Setup();
			var decoder = new MoteDecoder<int>();
			var context = new Context<int>();
			StandardMathLibrary<int>.Register(context);

			NotImplementedException exception = Assert.Throws<NotImplementedException>(
				() => decoder.Decode(sentence).Evaluate(context));
			StringAssert.Contains("CalculatorInteger", exception.Message);
		}

		[Test]
		public void TestMath_RejectsInvalidArguments()
		{
			var context = new Context();
			StandardMathLibrary<float>.Register(context);

			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("math.abs(\"1\")").Evaluate(context));
			Assert.Throws<ArgumentException>(
				() => _decoder.Decode("math.clamp(1,5,2)").Evaluate(context));
			Assert.Throws<ArgumentException>(
				() => _decoder.Decode("math.min(1)").Evaluate(context));
		}

		private float EvaluateMathFloat(string sentence, Context context)
		{
			return _decoder.Decode(sentence).Evaluate(context).Value;
		}
	}
}
