using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public class MoteScriptTests_CalculatorDouble
	{
		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			MoteDecoder<double>.Setup();
		}

		[Test]
		public void TestCalculatorDouble_Conversions()
		{
			ICalculator<double> calculator = new CalculatorDouble();

			Assert.AreEqual(1.0, calculator.Convert(true));
			Assert.AreEqual(0.0, calculator.Convert(false));
			Assert.AreEqual(123.0, calculator.Convert(123));
			Assert.AreEqual(1.25, calculator.Convert(1.25f));
			Assert.AreEqual(123.456, calculator.Convert("123.456"));
			Assert.AreEqual(123.456, calculator.Convert("123.456".AsSpan()));
			Assert.AreEqual(1.25f, calculator.ToSingle(1.25));
			Assert.IsTrue(calculator.ToBool(0.1));
			Assert.IsFalse(calculator.ToBool(0.0));
			Assert.IsFalse(calculator.ToBool(-0.1));
		}

		[Test]
		public void TestCalculatorDouble_ArithmeticAndComparisons()
		{
			ICalculator<double> calculator = new CalculatorDouble();

			Assert.AreEqual(0.3, calculator.Add(0.1, 0.2), 1e-15);
			Assert.AreEqual(2.5, calculator.Substruct(4.0, 1.5));
			Assert.AreEqual(3.0, calculator.Multiple(1.5, 2.0));
			Assert.AreEqual(2.5, calculator.Divide(5.0, 2.0));
			Assert.AreEqual(1.0, calculator.Mod(5.0, 2.0));
			Assert.IsTrue(calculator.GreaterThan(2.0, 1.0));
			Assert.IsTrue(calculator.GreaterThanOrEqualTo(2.0, 2.0));
			Assert.IsTrue(calculator.LessThan(1.0, 2.0));
			Assert.IsTrue(calculator.LessThanOrEqualTo(2.0, 2.0));
		}

		[Test]
		public void TestCalculatorDouble_DecoderAndLibraries()
		{
			var decoder = new MoteDecoder<double>();
			var context = new Context<double>();
			StandardMathLibrary<double>.Register(context);
			StandardStringLibrary<double>.Register(context);

			Assert.AreEqual(
				0.3,
				decoder.Decode("0.1+0.2").Evaluate(context).Value,
				1e-15);
			Assert.AreEqual(
				1.0,
				decoder.Decode("math.sin(math.pi()/2)").Evaluate(context).Value,
				1e-15);
			Assert.AreEqual(
				123.456,
				decoder.Decode("string.parseNumber(\"123.456\")")
					.Evaluate(context).Value);

			double random = decoder
				.Decode("random=math.random(42);random.rangeReal(-1,1)")
				.Evaluate(context).Value;
			Assert.That(random, Is.InRange(-1.0, 1.0));
		}
	}
}
