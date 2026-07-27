using System;
using System.Threading;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[Test]
		public void TestExecutionLimits_StopsInfiniteLoop()
		{
			MoteValue<float> script = _decoder.Decode(
				"value=0;while(1){value=value+1}");
			var context = new Context();
			var options = new MoteExecutionOptions(maxSteps: 50);

			MoteExecutionLimitException exception =
				Assert.Throws<MoteExecutionLimitException>(
					() => script.Evaluate(context, options));

			Assert.AreEqual(MoteExecutionLimit.Steps, exception.Limit);
			Assert.AreEqual(51, exception.Actual);
			Assert.AreEqual(50, exception.Maximum);
			Assert.Greater(context["value"].IntegerValue, 0);
		}

		[Test]
		public void TestExecutionLimits_StopsDeepRecursion()
		{
			MoteValue<float> script = _decoder.Decode(
				"recurse=(value)=>{return recurse(value+1)};recurse(0)");
			var options = new MoteExecutionOptions(
				maxSteps: 1000,
				maxFunctionDepth: 8);

			MoteExecutionLimitException exception =
				Assert.Throws<MoteExecutionLimitException>(
					() => script.Evaluate(new Context(), options));

			Assert.AreEqual(MoteExecutionLimit.FunctionDepth, exception.Limit);
			Assert.AreEqual(9, exception.Actual);
			Assert.AreEqual(8, exception.Maximum);
		}

		[Test]
		public void TestExecutionLimits_AllowsExecutionWithinLimits()
		{
			MoteValue<float> script = _decoder.Decode(
				"factorial=(value)=>{if(value<=1){return 1};return factorial(value-1)*value};factorial(5)");
			var options = new MoteExecutionOptions(
				maxSteps: 200,
				maxFunctionDepth: 8);

			Assert.AreEqual(
				120,
				script.Evaluate(new Context(), options).IntegerValue);
		}

		[Test]
		public void TestExecutionLimits_ObservesCancellation()
		{
			MoteValue<float> script = _decoder.Decode("1+2");
			var options = new MoteExecutionOptions(
				cancellationToken: new CancellationToken(canceled: true));

			Assert.Throws<OperationCanceledException>(
				() => script.Evaluate(null, options));
		}

		[Test]
		public void TestExecutionLimits_ResetsAfterFailure()
		{
			MoteValue<float> limitedScript = _decoder.Decode("while(1){}");
			Assert.Throws<MoteExecutionLimitException>(
				() => limitedScript.Evaluate(
					new Context(),
					new MoteExecutionOptions(maxSteps: 10)));

			MoteValue<float> nextScript = _decoder.Decode("1+2");
			Assert.AreEqual(
				3,
				nextScript.Evaluate(
					null,
					new MoteExecutionOptions(maxSteps: 10)).IntegerValue);
		}

		[Test]
		public void TestExecutionLimits_AccumulatesStepsAcrossYieldResumes()
		{
			var context = new Context();
			MoteExecution<float> execution = _decoder
				.Decode("i=0;while(i<100){i=i+1;yield(i)}")
				.CreateExecution(
					context,
					new MoteExecutionOptions(maxSteps: 40));

			execution.Evaluate();
			MoteExecutionLimitException exception = null;
			while (execution.IsYielded)
			{
				try
				{
					execution.Resume();
				}
				catch (MoteExecutionLimitException caught)
				{
					exception = caught;
					break;
				}
			}

			Assert.NotNull(exception);
			Assert.AreEqual(MoteExecutionLimit.Steps, exception.Limit);
			Assert.AreEqual(41, exception.Actual);
			Assert.AreEqual(40, exception.Maximum);
			Assert.AreEqual(MoteExecutionState.Faulted, execution.State);
		}

		[Test]
		public void TestExecutionLimits_CancellationAppliesAfterYield()
		{
			using var cancellation = new CancellationTokenSource();
			var context = new Context();
			MoteExecution<float> execution = _decoder
				.Decode("yield(1);2")
				.CreateExecution(
					context,
					new MoteExecutionOptions(
						cancellationToken: cancellation.Token));

			execution.Evaluate();
			cancellation.Cancel();

			Assert.Throws<OperationCanceledException>(() => execution.Resume());
			Assert.AreEqual(MoteExecutionState.Faulted, execution.State);
		}
	}
}
