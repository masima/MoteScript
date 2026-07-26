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
			MoteValue<float> result = _decoder
				.Decode("value=1;yield(value);value=value+2;value")
				.Evaluate(context);

			Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);
			Assert.That(yield.Value.IntegerValue, Is.EqualTo(1));
			Assert.That(yield.Evaluate().IntegerValue, Is.EqualTo(3));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(3));
		}

		[Test]
		public void TestYield_UsesChangedContextWhenResumed()
		{
			var context = new Context();
			MoteValue<float> result = _decoder.Decode("value=1;yield(value);value+1").Evaluate(context);
			Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);

			context["value"] = new MoteValue<float>(10);

			Assert.That(yield.Evaluate().IntegerValue, Is.EqualTo(11));
		}

		[Test]
		public void TestYield_MultipleYieldsAreSeparateOneShotContinuations()
		{
			MoteValue<float> result = _decoder.Decode("yield(1);yield(2);3").Evaluate(new Context());
			Assert.That(result.TryGetYield(out MoteYield<float> first), Is.True);
			Assert.That(first.Value.IntegerValue, Is.EqualTo(1));

			result = first.Evaluate();
			Assert.That(result.TryGetYield(out MoteYield<float> second), Is.True);
			Assert.That(second.Value.IntegerValue, Is.EqualTo(2));
			Assert.Throws<InvalidOperationException>(() => first.Evaluate());
			Assert.That(second.Evaluate().IntegerValue, Is.EqualTo(3));
			Assert.Throws<InvalidOperationException>(() => second.Evaluate());
		}

		[Test]
		public void TestYield_WhilePreservesLoopState()
		{
			var context = new Context();
			MoteValue<float> result = _decoder
				.Decode("i=0;while(i<3){i=i+1;yield(i)};i")
				.Evaluate(context);

			for (int expected = 1; expected <= 3; ++expected)
			{
				Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);
				Assert.That(yield.Value.IntegerValue, Is.EqualTo(expected));
				result = yield.Evaluate();
			}
			Assert.That(result.IntegerValue, Is.EqualTo(3));
		}

		[Test]
		public void TestYield_ContinueAndBreakApplyAfterResume()
		{
			var context = new Context();
			MoteValue<float> result = _decoder
				.Decode("i=0;sum=0;while(i<4){i=i+1;yield(i);if(i<3){continue};sum=sum+i;if(i==3){break}};sum")
				.Evaluate(context);

			for (int expected = 1; expected <= 3; ++expected)
			{
				Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);
				Assert.That(yield.Value.IntegerValue, Is.EqualTo(expected));
				result = yield.Evaluate();
			}
			Assert.That(result.IntegerValue, Is.EqualTo(3));
		}

		[Test]
		public void TestYield_FunctionRetainsParametersUntilReturn()
		{
			var context = new Context();
			context["value"] = new MoteValue<float>(100);
			MoteValue<float> result = _decoder
				.Decode("func=(value)=>{yield(value);return value+1};answer=func(4);answer+value")
				.Evaluate(context);

			Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);
			Assert.That(yield.Value.IntegerValue, Is.EqualTo(4));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(4));

			Assert.That(yield.Evaluate().IntegerValue, Is.EqualTo(105));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(100));
		}

		[Test]
		public void TestYield_NestedLoopsOnlyBreaksInnerLoop()
		{
			MoteValue<float> result = _decoder
				.Decode("outer=0;count=0;while(outer<2){outer=outer+1;inner=0;while(inner<3){inner=inner+1;yield(inner);break};count=count+1};count")
				.Evaluate(new Context());

			for (int i = 0; i < 2; ++i)
			{
				Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);
				Assert.That(yield.Value.IntegerValue, Is.EqualTo(1));
				result = yield.Evaluate();
			}
			Assert.That(result.IntegerValue, Is.EqualTo(2));
		}

		[Test]
		public void TestYield_TailRecursiveFunctionRestoresEachParameterFrame()
		{
			var context = new Context();
			context["value"] = new MoteValue<float>(100);
			MoteValue<float> result = _decoder
				.Decode("recurse=(value)=>{yield(value);if(value>0){return recurse(value-1)};return value};answer=recurse(2);answer+value")
				.Evaluate(context);

			for (int expected = 2; expected >= 0; --expected)
			{
				Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);
				Assert.That(yield.Value.IntegerValue, Is.EqualTo(expected));
				result = yield.Evaluate();
			}
			Assert.That(result.IntegerValue, Is.EqualTo(100));
			Assert.That(context["value"].IntegerValue, Is.EqualTo(100));
		}

		[Test]
		public void TestYield_ExceptionOnResumeRestoresFunctionParameter()
		{
			var context = new Context();
			context["value"] = new MoteValue<float>(100);
			MoteValue<float> result = _decoder
				.Decode("func=(value)=>{yield(value);missing};func(4)")
				.Evaluate(context);
			Assert.That(result.TryGetYield(out MoteYield<float> yield), Is.True);

			Assert.Throws<InvalidOperationException>(() => yield.Evaluate());
			Assert.That(context["value"].IntegerValue, Is.EqualTo(100));
			Assert.Throws<InvalidOperationException>(() => yield.Evaluate());
		}

		[Test]
		public void TestYield_RequiresParenthesizedValue()
		{
			Assert.Throws<MoteScriptParseException>(() => _decoder.Decode("yield;"));
			Assert.Throws<MoteScriptParseException>(() => _decoder.Decode("yield();"));
		}
	}
}
