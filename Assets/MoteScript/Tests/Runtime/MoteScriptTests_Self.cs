using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	using MoteValue = MoteValue<float>;

	public partial class MoteScriptTests
	{
		[Test]
		public void TestSelf_ReceivesOwningDictionary()
		{
			MoteValue script = _decoder.Decode(
				"add=(value;self owner)=>{owner.score=owner.score+value};"
				+ "player.score=10;player.add=add;"
				+ "player.add(5);player.score");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(15));
		}

		[Test]
		public void TestSelf_SupportsBracketMemberCall()
		{
			MoteValue script = _decoder.Decode(
				"add=(value;self owner)=>{owner.score=owner.score+value};"
				+ "player.score=10;player.add=add;"
				+ "player[\"add\"](5);player.score");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(15));
		}

		[Test]
		public void TestSelf_UsesImmediateOwningDictionary()
		{
			MoteValue script = _decoder.Decode(
				"read=(;self owner)=>{owner.value};"
				+ "root.value=1;root.child.value=2;"
				+ "root.child.read=read;root.child.read()");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(2));
		}

		[Test]
		public void TestSelf_FunctionCopyUsesCallSiteReceiver()
		{
			MoteValue script = _decoder.Decode(
				"add=(value;self owner)=>{owner.score=owner.score+value};"
				+ "first.score=1;first.add=add;"
				+ "second.score=10;second.add=first.add;"
				+ "second.add(5);first.score*100+second.score");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(115));
		}

		[Test]
		public void TestSelf_RequiresDictionaryMemberCall()
		{
			MoteValue script = _decoder.Decode(
				"read=(;self owner)=>{owner.value};read()");

			InvalidOperationException exception =
				Assert.Throws<InvalidOperationException>(
					() => script.Evaluate(new Context()));

			StringAssert.Contains(
				"requires a dictionary receiver",
				exception.Message);
		}

		[Test]
		public void TestSelf_DictionaryCallDoesNotRequireDeclaration()
		{
			MoteValue script = _decoder.Decode(
				"increment=(value)=>{value+1};"
				+ "dictionary.increment=increment;"
				+ "dictionary.increment(2)");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(3));
		}

		[Test]
		public void TestSelf_BindingIsReadOnly()
		{
			MoteValue script = _decoder.Decode(
				"replace=(;self owner)=>{owner=[]};"
				+ "dictionary.replace=replace;"
				+ "dictionary.replace()");

			InvalidOperationException exception =
				Assert.Throws<InvalidOperationException>(
					() => script.Evaluate(new Context()));

			StringAssert.Contains("is read-only", exception.Message);
		}

		[Test]
		public void TestSelf_AllowsMemberCreation()
		{
			MoteValue script = _decoder.Decode(
				"initialize=(;self owner)=>{owner.created=7};"
				+ "dictionary.initialize=initialize;"
				+ "dictionary.initialize();dictionary.created");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(7));
		}

		[Test]
		public void TestSelf_ValidatesDeclaration()
		{
			Assert.Throws<MoteScriptParseException>(
				() => _decoder.Decode(
					"function=(;self owner;self other)=>{0}"));
			Assert.Throws<MoteScriptParseException>(
				() => _decoder.Decode(
					"function=(value;self value)=>{0}"));
			Assert.Throws<MoteScriptParseException>(
				() => _decoder.Decode(
					"function=(;self first,second)=>{0}"));
		}

		[Test]
		public void TestSelf_CoexistsWithLocalVariables()
		{
			MoteValue script = _decoder.Decode(
				"calculate=(value;self owner;local result)=>{"
				+ "result=owner.base+value;owner.result=result};"
				+ "dictionary.base=3;dictionary.calculate=calculate;"
				+ "dictionary.calculate(4);dictionary.result");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(7));
		}

		[Test]
		public void TestSelf_CoexistsWithReferenceParameters()
		{
			MoteValue script = _decoder.Decode(
				"transfer=(ref target;self owner)=>{"
				+ "target=target+owner.amount};"
				+ "dictionary.amount=4;"
				+ "dictionary.transfer=transfer;"
				+ "value=3;dictionary.transfer(ref value);value");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(7));
		}

		[Test]
		public void TestSelf_SupportsRecursion()
		{
			MoteValue script = _decoder.Decode(
				"increment=(count;self owner)=>{"
				+ "if(count<=0){return owner.value};"
				+ "owner.value=owner.value+1;"
				+ "return owner.increment(count-1)};"
				+ "dictionary.value=0;"
				+ "dictionary.increment=increment;"
				+ "dictionary.increment(3);dictionary.value");

			Assert.That(
				script.Evaluate(new Context()).IntegerValue,
				Is.EqualTo(3));
		}

		[Test]
		public void TestSelf_RestoresExistingContextValueAfterException()
		{
			MoteValue script = _decoder.Decode(
				"fail=(;self owner)=>{owner.value=2;missing};"
				+ "dictionary.value=1;dictionary.fail=fail;"
				+ "dictionary.fail()");
			var context = new Context();
			context.Set("owner", 99);

			Assert.Throws<InvalidOperationException>(
				() => script.Evaluate(context));
			Assert.That(context["owner"].IntegerValue, Is.EqualTo(99));
			Assert.That(
				context.GetByPath("dictionary.value").IntegerValue,
				Is.EqualTo(2));
		}

		[Test]
		public void TestSelf_CapturesReceiverAcrossYield()
		{
			var context = new Context();
			_decoder.Decode(
				"run=(;self owner)=>{"
				+ "yield(owner.value);owner.value=owner.value+1};"
				+ "target.value=1;target.run=run")
				.Evaluate(context);
			IContext<float> original =
				context["target"].GetDictionary();
			MoteValue function = original["run"];
			MoteExecution<float> execution =
				_decoder.Decode("target.run()").CreateExecution(context);

			execution.Evaluate();

			IContext<float> replacement = context.Instantiate();
			replacement["value"] = new MoteValue(100);
			replacement["run"] = function;
			context["target"] = new MoteValue(replacement);
			execution.Resume();

			Assert.That(original["value"].IntegerValue, Is.EqualTo(2));
			Assert.That(replacement["value"].IntegerValue, Is.EqualTo(100));
			Assert.That(context.ContainsKey("owner"), Is.False);
		}

		[Test]
		public void TestCSharpFunction_ReceivesOwningDictionary()
		{
			IContext<float> capturedReceiver = null;
			MoteValue.Function add =
				(executionContext, receiver, parameters) =>
				{
					capturedReceiver = receiver;
					int value = receiver["value"].IntegerValue
						+ parameters[0].Evaluate(executionContext).IntegerValue;
					receiver["value"] = new MoteValue(value);
					return receiver["value"];
				};
			var context = new Context();
			context["add"] = new MoteValue(add);
			MoteValue script = _decoder.Decode(
				"dictionary.value=10;dictionary.add=add;"
				+ "dictionary.add(5);dictionary.value");

			Assert.That(script.Evaluate(context).IntegerValue, Is.EqualTo(15));
			Assert.That(
				capturedReceiver,
				Is.SameAs(context["dictionary"].GetDictionary()));
		}

		[Test]
		public void TestCSharpFunction_SupportsBracketCall()
		{
			IContext<float> capturedReceiver = null;
			MoteValue.Function inspect =
				(executionContext, receiver, parameters) =>
				{
					capturedReceiver = receiver;
					return receiver["value"];
				};
			var context = new Context();
			context["inspect"] = new MoteValue(inspect);
			MoteValue script = _decoder.Decode(
				"dictionary.value=7;dictionary.inspect=inspect;"
				+ "dictionary[\"inspect\"]()");

			Assert.That(script.Evaluate(context).IntegerValue, Is.EqualTo(7));
			Assert.That(
				capturedReceiver,
				Is.SameAs(context["dictionary"].GetDictionary()));
		}

		[Test]
		public void TestCSharpFunction_CopyUsesCallSiteReceiver()
		{
			IContext<float> capturedReceiver = null;
			MoteValue.Function inspect =
				(executionContext, receiver, parameters) =>
				{
					capturedReceiver = receiver;
					return receiver["value"];
				};
			var context = new Context();
			context["inspect"] = new MoteValue(inspect);
			MoteValue script = _decoder.Decode(
				"first.value=1;first.inspect=inspect;"
				+ "second.value=2;second.inspect=first.inspect;"
				+ "second.inspect()");

			Assert.That(script.Evaluate(context).IntegerValue, Is.EqualTo(2));
			Assert.That(
				capturedReceiver,
				Is.SameAs(context["second"].GetDictionary()));
		}

		[Test]
		public void TestCSharpFunction_DirectCallReceivesNull()
		{
			IContext<float> capturedReceiver = null;
			bool invoked = false;
			MoteValue.Function inspect =
				(executionContext, receiver, parameters) =>
				{
					invoked = true;
					capturedReceiver = receiver;
					return new MoteValue(1);
				};
			var context = new Context();
			context["inspect"] = new MoteValue(inspect);

			Assert.That(
				_decoder.Decode("inspect()").Evaluate(context).IntegerValue,
				Is.EqualTo(1));
			Assert.That(invoked, Is.True);
			Assert.That(capturedReceiver, Is.Null);
		}
	}
}
