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
	}
}
