using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[Test]
		public void TestYield_ResumesFollowingStatements()
		{
			var context = new Context();
			MoteExecution<float> execution = _decoder
				.Decode("value=1;yield(value);value=value+2;value")
				.CreateExecution(context);

			execution.Evaluate();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Yielded));
			Assert.That(
				((IExecutionContext<float>)context).CurrentExecution,
				Is.SameAs(execution));
			Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(1));
			Assert.That(execution.Resume().IntegerValue, Is.EqualTo(3));
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Completed));
			Assert.That(
				((IExecutionContext<float>)context).CurrentExecution,
				Is.Null);
			Assert.That(execution.Result.IntegerValue, Is.EqualTo(3));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(3));
		}

		[Test]
		public void TestYield_UsesChangedContextWhenResumed()
		{
			var context = new Context();
			MoteExecution<float> execution = _decoder
				.Decode("value=1;yield(value);value+1")
				.CreateExecution(context);
			execution.Evaluate();

			context["value"] = new MoteValue<float>(10);

			Assert.That(execution.Resume().IntegerValue, Is.EqualTo(11));
		}

		[Test]
		public void TestYield_MultipleYieldsReuseOneExecution()
		{
			MoteExecution<float> execution = _decoder
				.Decode("yield(1);yield(2);3")
				.CreateExecution(new Context());

			execution.Evaluate();
			Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(1));

			execution.Resume();
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Yielded));
			Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(2));

			Assert.That(execution.Resume().IntegerValue, Is.EqualTo(3));
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Completed));
			Assert.Throws<InvalidOperationException>(() => execution.Resume());
			Assert.Throws<InvalidOperationException>(() => execution.Evaluate());
		}

		[Test]
		public void TestYield_WhilePreservesLoopState()
		{
			MoteExecution<float> execution = _decoder
				.Decode("i=0;while(i<3){i=i+1;yield(i)};i")
				.CreateExecution(new Context());
			execution.Evaluate();

			for (int expected = 1; expected <= 3; ++expected)
			{
				Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Yielded));
				Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(expected));
				execution.Resume();
			}
			Assert.That(execution.Result.IntegerValue, Is.EqualTo(3));
		}

		[Test]
		public void TestYield_ContinueAndBreakApplyAfterResume()
		{
			MoteExecution<float> execution = _decoder
				.Decode("i=0;sum=0;while(i<4){i=i+1;yield(i);if(i<3){continue};sum=sum+i;if(i==3){break}};sum")
				.CreateExecution(new Context());
			execution.Evaluate();

			for (int expected = 1; expected <= 3; ++expected)
			{
				Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(expected));
				execution.Resume();
			}
			Assert.That(execution.Result.IntegerValue, Is.EqualTo(3));
		}

		[Test]
		public void TestYield_FunctionRetainsParametersUntilReturn()
		{
			var context = new Context();
			context["value"] = new MoteValue<float>(100);
			MoteExecution<float> execution = _decoder
				.Decode("func=(value)=>{yield(value);return value+1};answer=func(4);answer+value")
				.CreateExecution(context);
			execution.Evaluate();

			Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(4));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(4));
			Assert.That(execution.Resume().IntegerValue, Is.EqualTo(105));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(100));
		}

		[Test]
		public void TestYield_NestedLoopsOnlyBreaksInnerLoop()
		{
			MoteExecution<float> execution = _decoder
				.Decode("outer=0;count=0;while(outer<2){outer=outer+1;inner=0;while(inner<3){inner=inner+1;yield(inner);break};count=count+1};count")
				.CreateExecution(new Context());
			execution.Evaluate();

			for (int i = 0; i < 2; ++i)
			{
				Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(1));
				execution.Resume();
			}
			Assert.That(execution.Result.IntegerValue, Is.EqualTo(2));
		}

		[Test]
		public void TestYield_TailRecursiveFunctionRestoresEachParameterFrame()
		{
			var context = new Context();
			context["value"] = new MoteValue<float>(100);
			MoteExecution<float> execution = _decoder
				.Decode("recurse=(value)=>{yield(value);if(value>0){return recurse(value-1)};return value};answer=recurse(2);answer+value")
				.CreateExecution(context);
			execution.Evaluate();

			for (int expected = 2; expected >= 0; --expected)
			{
				Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(expected));
				execution.Resume();
			}
			Assert.That(execution.Result.IntegerValue, Is.EqualTo(100));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(100));
		}

		[Test]
		public void TestYield_ExceptionOnResumeRestoresFunctionParameter()
		{
			var context = new Context();
			context["value"] = new MoteValue<float>(100);
			MoteExecution<float> execution = _decoder
				.Decode("func=(value)=>{yield(value);missing};func(4)")
				.CreateExecution(context);
			execution.Evaluate();

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => execution.Resume());
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Faulted));
			Assert.That(execution.Exception, Is.SameAs(exception));
			Assert.That(
				((IExecutionContext<float>)context).CurrentExecution,
				Is.Null);
			Assert.That(context["value"].IntegerValue, Is.EqualTo(100));
			Assert.Throws<InvalidOperationException>(() => execution.Resume());
		}

		[Test]
		public void TestYield_ExecutionContextRejectsConcurrentExecution()
		{
			var context = new Context();
			MoteValue<float> script = _decoder.Decode("yield(1);2");
			MoteExecution<float> first = script.CreateExecution(context);
			MoteExecution<float> second = script.CreateExecution(context);
			first.Evaluate();

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => second.Evaluate());
			StringAssert.Contains("already being used", exception.Message);
			Assert.That(second.State, Is.EqualTo(MoteExecutionState.Faulted));
			Assert.That(
				((IExecutionContext<float>)context).CurrentExecution,
				Is.SameAs(first));

			Assert.That(first.Resume().IntegerValue, Is.EqualTo(2));
			Assert.That(
				((IExecutionContext<float>)context).CurrentExecution,
				Is.Null);
		}

		[Test]
		public void TestYield_DirectEvaluateExplainsExecutionRequirement()
		{
			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("yield(1)").Evaluate(new Context()));
			StringAssert.Contains("CreateExecution", exception.Message);
		}

		[Test]
		public void TestYield_RequiresParenthesizedValue()
		{
			Assert.Throws<MoteScriptParseException>(() => _decoder.Decode("yield;"));
			Assert.Throws<MoteScriptParseException>(() => _decoder.Decode("yield();"));
		}
	}
}
