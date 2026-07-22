using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public class MoteScriptTests_CalculatorDecimal
	{
		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			MoteDecoder<decimal>.Setup();
		}

		[Test]
		public void TestCalculatorDecimal_Conversions()
		{
			ICalculator<decimal> calculator = new CalculatorDecimal();

			Assert.AreEqual(1.0m, calculator.Convert(true));
			Assert.AreEqual(0.0m, calculator.Convert(false));
			Assert.AreEqual(123m, calculator.Convert(123));
			Assert.AreEqual(1.25m, calculator.Convert(1.25f));
			Assert.AreEqual(123.456m, calculator.Convert("123.456"));
			Assert.AreEqual(123.456m, calculator.Convert("123.456".AsSpan()));
			Assert.AreEqual(1.25f, calculator.ToSingle(1.25m));
			Assert.IsTrue(calculator.ToBool(0.1m));
			Assert.IsFalse(calculator.ToBool(0.0m));
			Assert.IsFalse(calculator.ToBool(-0.1m));
		}

		[Test]
		public void TestCalculatorDecimal_ArithmeticAndComparisons()
		{
			ICalculator<decimal> calculator = new CalculatorDecimal();

			Assert.AreEqual(0.3m, calculator.Add(0.1m, 0.2m));
			Assert.AreEqual(2.5m, calculator.Substruct(4.0m, 1.5m));
			Assert.AreEqual(3.0m, calculator.Multiple(1.5m, 2.0m));
			Assert.AreEqual(2.5m, calculator.Divide(5.0m, 2.0m));
			Assert.AreEqual(1.0m, calculator.Mod(5.0m, 2.0m));
			Assert.IsTrue(calculator.GreaterThan(2.0m, 1.0m));
			Assert.IsTrue(calculator.GreaterThanOrEqualTo(2.0m, 2.0m));
			Assert.IsTrue(calculator.LessThan(1.0m, 2.0m));
			Assert.IsTrue(calculator.LessThanOrEqualTo(2.0m, 2.0m));
		}

		[Test]
		public void TestCalculatorDecimal_DecoderUsesDecimalPrecision()
		{
			var decoder = new MoteDecoder<decimal>();
			var context = new Context<decimal>();

			Assert.AreEqual(0.3m, decoder.Decode("0.1+0.2").Evalute(context).Value);
			Assert.AreEqual(2.5m, decoder.Decode("5/2").Evalute(context).Value);
			Assert.AreEqual(1.0m, decoder.Decode("5%2").Evalute(context).Value);
			Assert.AreEqual(1.0m, decoder.Decode("2>=2").Evalute(context).Value);
		}
	}
}
