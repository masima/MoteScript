using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public class MoteScriptTests_OperatorDecimal
	{
		private MoteDecoder<decimal> _decoder;

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			MoteDecoder<decimal>.Setup();
			_decoder = new MoteDecoder<decimal>();
		}

		[Test]
		public void TestOperatorDecimal()
		{
			var patterns = new (string sentence, decimal result)[]
			{
				("1+2-3", 1m+2m-3m),
				("1+(2-3)", 1m+(2m-3m)),
				("1+2*3", 1m+2m*3m),
				("(1+2)*3", (1m+2m)*3m),
				("1*2+3", 1m*2m+3m),
				("1+2/3", 1m+2m/3m),
				("1/2+3", 1m/2m+3m),
				("3%2", 3m%2m),
				("0.1+0.2", 0.1m+0.2m),
			};
			TestPatterns(patterns);
		}

		[Test]
		public void TestOperatorDecimal_Compare()
		{
			var patterns = new (string sentence, decimal result)[]
			{
				("1<2", Convert(1<2)),
				("1<1", Convert(1<1)),
				("2<1", Convert(2<1)),
				("1>2", Convert(1>2)),
				("1>1", Convert(1>1)),
				("2>1", Convert(2>1)),
				("1<=2", Convert(1<=2)),
				("1<=1", Convert(1<=1)),
				("2<=1", Convert(2<=1)),
				("1>=2", Convert(1>=2)),
				("1>=1", Convert(1>=1)),
				("2>=1", Convert(2>=1)),
				("1==2", Convert(1==2)),
				("1==1", Convert(1==1)),
				("2==1", Convert(2==1)),
				("1!=2", Convert(1!=2)),
				("1!=1", Convert(1!=1)),
				("2!=1", Convert(2!=1)),
				("1+2<3", Convert(1+2<3)),
				("1+2>3", Convert(1+2>3)),
				("1+2==3", Convert(1+2==3)),
				("1<2+3", Convert(1<2+3)),
				("1>2+3", Convert(1>2+3)),
				("1==2+3", Convert(1==2+3)),
				("5==2+3", Convert(5==2+3)),
				("0.1+0.2==0.3", 1m),
			};
			TestPatterns(patterns);
		}

		[Test]
		public void TestVariableDecimal(
			[Values(1,2,3,5,7,11)] int aValue,
			[Values(1,2,3,5,7,11)] int bValue,
			[Values(1,2,3,5,7,11)] int cValue)
		{
			decimal a = aValue;
			decimal b = bValue;
			decimal c = cValue;
			var context = new Context<decimal>();
			context
				.Set(nameof(a), a)
				.Set(nameof(b), b)
				.Set(nameof(c), c);

			var patterns = new (string sentence, decimal result)[]
			{
				("a+b+c", a+b+c),
				("a-b+c", a-b+c),
				("a+b-c", a+b-c),
				("a+b*c", a+b*c),
				("a*b+c", a*b+c),
				("a+b/c", a+b/c),
				("a/b+c", a/b+c),
			};
			TestPatterns(patterns, context);
		}

		[Test]
		public void TestVariableDecimal_Logical(
			[Values(1,2,3,5,7,11)] int aValue,
			[Values(1,2,3,5,7,11)] int bValue,
			[Values(1,2,3,5,7,11)] int cValue)
		{
			decimal a = aValue;
			decimal b = bValue;
			decimal c = cValue;
			var context = new Context<decimal>();
			context
				.Set(nameof(a), a)
				.Set(nameof(b), b)
				.Set(nameof(c), c);

			var patterns = new (string sentence, decimal result)[]
			{
				("a+b==c || a==b+c", Convert(a+b==c || a==b+c)),
				("a*b==c || a==b*c", Convert(a*b==c || a==b*c)),
				("a==b || b==c", Convert(a==b || b==c)),
				("a==b || b==c && a==c", Convert(a==b || b==c && a==c)),
				("a==b && b==c || a==c", Convert(a==b && b==c || a==c)),
			};
			TestPatterns(patterns, context);
		}

		[Test]
		public void TestOperatorDecimal_LogicalShortCircuit()
		{
			var context = new Context<decimal>().Set("callCount", 0m);

			Assert.AreEqual(0m, _decoder.Decode("0 && (callCount=callCount+1)").Evaluate(context).Value);
			Assert.AreEqual(0m, context["callCount"].Value,
				"&& must not evaluate its right operand when the left operand is false.");

			Assert.AreEqual(1m, _decoder.Decode("1 || (callCount=callCount+1)").Evaluate(context).Value);
			Assert.AreEqual(0m, context["callCount"].Value,
				"|| must not evaluate its right operand when the left operand is true.");

			Assert.AreEqual(1m, _decoder.Decode("1 && (callCount=callCount+1)").Evaluate(context).Value);
			Assert.AreEqual(1m, context["callCount"].Value,
				"&& must evaluate its right operand when the left operand is true.");

			Assert.AreEqual(1m, _decoder.Decode("0 || (callCount=callCount+1)").Evaluate(context).Value);
			Assert.AreEqual(2m, context["callCount"].Value,
				"|| must evaluate its right operand when the left operand is false.");
		}

		private void TestPatterns(
			(string sentence, decimal result)[] patterns,
			Context<decimal> context = null)
		{
			foreach ((string sentence, decimal expected) in patterns)
			{
				decimal actual = _decoder.Decode(sentence).Evaluate(context).Value;
				Assert.AreEqual(expected, actual, sentence);
			}
		}

		private static decimal Convert(bool value)
		{
			return value ? 1m : 0m;
		}
	}
}
