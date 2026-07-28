using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	using MoteValue = MoteValue<float>;

	public partial class MoteScriptTests
	{
		[Test]
		public void TestReference_UpdatesVariables()
		{
			MoteValue script = _decoder.Decode(
				"swap=(ref left,ref right;local temporary)=>{"
				+ "temporary=left;left=right;right=temporary};"
				+ "a=10;b=20;swap(ref a,ref b);a*100+b");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(2010));
		}

		[Test]
		public void TestReference_RequiresRefAtDefinitionAndCall()
		{
			MoteValue missingCallRef = _decoder.Decode(
				"function=(ref value)=>{value=2};"
				+ "target=1;function(target)");
			MoteValue unexpectedCallRef = _decoder.Decode(
				"function=(value)=>{value=2};"
				+ "target=1;function(ref target)");

			InvalidOperationException missing = Assert.Throws<InvalidOperationException>(
				() => missingCallRef.Evaluate(new Context()));
			InvalidOperationException unexpected = Assert.Throws<InvalidOperationException>(
				() => unexpectedCallRef.Evaluate(new Context()));

			StringAssert.Contains("requires a ref argument", missing.Message);
			StringAssert.Contains("cannot receive a ref argument", unexpected.Message);
		}

		[Test]
		public void TestReference_RequiresExistingAssignableTarget()
		{
			MoteValue undefined = _decoder.Decode(
				"function=(ref value)=>{value=2};"
				+ "function(ref missing)");
			MoteValue constant = _decoder.Decode(
				"function=(ref value)=>{value=2};"
				+ "function(ref 1)");

			InvalidOperationException undefinedException =
				Assert.Throws<InvalidOperationException>(
					() => undefined.Evaluate(new Context()));
			InvalidOperationException constantException =
				Assert.Throws<InvalidOperationException>(
					() => constant.Evaluate(new Context()));

			StringAssert.Contains(
				"ref requires an existing variable",
				undefinedException.Message);
			StringAssert.Contains(
				"ref requires an assignable",
				constantException.Message);
		}

		[Test]
		public void TestReference_PreservesTargetType()
		{
			MoteValue script = _decoder.Decode(
				"function=(ref value)=>{value=\"text\"};"
				+ "target=1;function(ref target)");

			InvalidOperationException exception =
				Assert.Throws<InvalidOperationException>(
					() => script.Evaluate(new Context()));

			StringAssert.Contains("Cannot assign String", exception.Message);
		}

		[Test]
		public void TestReference_UpdatesArrayElement()
		{
			MoteValue script = _decoder.Decode(
				"set=(ref target)=>{target=9};"
				+ "values=(1,2,3);"
				+ "set(ref values[1]);values[1]");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(9));
		}

		[Test]
		public void TestReference_UpdatesDictionaryMember()
		{
			MoteValue script = _decoder.Decode(
				"set=(ref target)=>{target=9};"
				+ "data.child.value=2;"
				+ "set(ref data.child.value);data.child.value");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(9));
		}

		[Test]
		public void TestReference_RequiresExistingCollectionMember()
		{
			MoteValue array = _decoder.Decode(
				"set=(ref target)=>{target=9};"
				+ "values=(1,2);set(ref values[2])");
			MoteValue dictionary = _decoder.Decode(
				"set=(ref target)=>{target=9};"
				+ "data=[];set(ref data.missing)");

			Assert.Throws<ArgumentOutOfRangeException>(
				() => array.Evaluate(new Context()));
			InvalidOperationException dictionaryException =
				Assert.Throws<InvalidOperationException>(
					() => dictionary.Evaluate(new Context()));
			StringAssert.Contains(
				"ref requires an existing member",
				dictionaryException.Message);
		}

		[Test]
		public void TestReference_SupportsRecursion()
		{
			MoteValue script = _decoder.Decode(
				"increment=(ref value,count)=>{"
				+ "if(count<=0){return value};"
				+ "value=value+1;"
				+ "return increment(ref value,count-1)};"
				+ "target=0;increment(ref target,3);target");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(3));
		}

		[Test]
		public void TestReference_AllowsAliasing()
		{
			MoteValue script = _decoder.Decode(
				"incrementBoth=(ref left,ref right)=>{"
				+ "left=left+1;right=right+1};"
				+ "target=1;incrementBoth(ref target,ref target);target");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(3));
		}

		[Test]
		public void TestReference_PersistsChangesWhenFunctionThrows()
		{
			MoteValue script = _decoder.Decode(
				"function=(ref value)=>{value=value+1;missing};"
				+ "target=1;function(ref target)");
			var context = new Context();

			Assert.Throws<InvalidOperationException>(
				() => script.Evaluate(context));
			Assert.That(context["target"].IntegerValue, Is.EqualTo(2));
			Assert.That(context.ContainsKey("value"), Is.False);
		}

		[Test]
		public void TestReference_SurvivesYield()
		{
			MoteValue script = _decoder.Decode(
				"function=(ref value)=>{"
				+ "value=value+1;yield(value);"
				+ "value=value+1;return value};"
				+ "target=1;function(ref target);target");
			var context = new Context();
			MoteExecution<float> execution =
				script.CreateExecution(context);

			execution.Evaluate();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Yielded));
			Assert.That(execution.YieldValue.IntegerValue, Is.EqualTo(2));
			Assert.That(context["target"].IntegerValue, Is.EqualTo(2));

			context["target"] = new MoteValue<float>(10);
			execution.Resume();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Completed));
			Assert.That(context["target"].IntegerValue, Is.EqualTo(11));
			Assert.That(context.ContainsKey("value"), Is.False);
		}

		[Test]
		public void TestReference_CapturesCollectionAcrossYield()
		{
			MoteValue script = _decoder.Decode(
				"function=(ref value)=>{"
				+ "yield(value);value=value+1};"
				+ "function(ref data.value)");
			var original = new Context();
			original.Set("value", 1);
			var replacement = new Context();
			replacement.Set("value", 100);
			var context = new Context();
			context["data"] = new MoteValue(original);
			MoteExecution<float> execution =
				script.CreateExecution(context);

			execution.Evaluate();
			context["data"] = new MoteValue(replacement);
			execution.Resume();

			Assert.That(original["value"].IntegerValue, Is.EqualTo(2));
			Assert.That(replacement["value"].IntegerValue, Is.EqualTo(100));
		}
	}
}
