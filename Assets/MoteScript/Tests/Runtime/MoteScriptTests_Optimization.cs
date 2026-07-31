using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[TestCase("(2+3)*4", 20)]
		[TestCase("1+2<4", 1)]
		[TestCase("1+2==3", 1)]
		[TestCase("1<2&&3<4", 1)]
		[TestCase("1>2||3>4", 0)]
		public void TestOptimizer_FoldsConstantExpression(
			string source,
			float expected)
		{
			MoteValue<float> decoded = _decoder.Decode(source);
			MoteValue<float> optimized = MoteOptimizer<float>.Optimize(decoded);

			Assert.That(optimized.ValueType, Is.EqualTo(EValueType.Const));
			Assert.That(optimized.FloatValue, Is.EqualTo(expected));
			Assert.That(decoded.ValueType, Is.Not.EqualTo(EValueType.Const));
		}

		[Test]
		public void TestDecodeOptimized_FoldsConstantsInsideAssignmentAndBlock()
		{
			MoteValue<float> script = _decoder.DecodeOptimized(
				"value=(2+3)*4;value=value+offset;value");
			var context = new Context().Set("offset", 2);

			Assert.That(script.Evaluate(context).FloatValue, Is.EqualTo(22));
		}

		[Test]
		public void TestOptimizer_PreservesVariableExpressions()
		{
			MoteValue<float> optimized = _decoder.DecodeOptimized("value+(2*3)");
			var first = new Context().Set("value", 1);
			var second = new Context().Set("value", 10);

			Assert.That(optimized.Evaluate(first).FloatValue, Is.EqualTo(7));
			Assert.That(optimized.Evaluate(second).FloatValue, Is.EqualTo(16));
		}

		[Test]
		public void TestOptimizer_ReportsConstantTypeErrorDuringOptimization()
		{
			MoteValue<float> decoded = _decoder.Decode("1+\"text\"");

			InvalidOperationException error = Assert.Throws<InvalidOperationException>(
				() => MoteOptimizer<float>.Optimize(decoded));

			StringAssert.Contains("requires a numeric value", error.Message);
		}

		[Test]
		public void TestOptimizer_ReportsConstantDivisionByZeroDuringOptimization()
		{
			MoteDecoder<decimal>.Setup();
			var decoder = new MoteDecoder<decimal>();
			MoteValue<decimal> decoded = decoder.Decode("1/0");

			Assert.Throws<DivideByZeroException>(
				() => MoteOptimizer<decimal>.Optimize(decoded));
		}

#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		[Test]
		public void TestDecodeDebug_DoesNotOptimizeAutomatically()
		{
			MoteValue<float> script = _decoder.DecodeDebug(
				"(2+3)*4;",
				"optimization.mote",
				out MoteDebugDocument document);

			Assert.That(script.ValueType, Is.Not.EqualTo(EValueType.Const));
			Assert.That(document.Statements.Count, Is.EqualTo(1));
			Assert.That(script.Evaluate(new Context()).FloatValue, Is.EqualTo(20));
		}
#endif

		[Test]
		public void TestOptimizer_PreservesLogicalShortCircuit()
		{
			MoteValue<float> conjunction = _decoder.DecodeOptimized(
				"0&&(1+\"text\")");
			MoteValue<float> disjunction = _decoder.DecodeOptimized(
				"1||(1+\"text\")");

			Assert.That(conjunction.ToBool(), Is.False);
			Assert.That(disjunction.ToBool(), Is.True);
		}

		[Test]
		public void TestOptimizer_DoesNotAllocateDuringRepeatedEvaluation()
		{
			MoteValue<float> optimized = _decoder.DecodeOptimized(
				"value=value+2*3");
			var context = new Context().Set("value", 0);
			optimized.Evaluate(context);

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 1000; ++i)
			{
				optimized.Evaluate(context);
			}
			long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(allocated, Is.Zero);
			Assert.That(context["value"].IntegerValue, Is.EqualTo(6006));
		}
	}
}
