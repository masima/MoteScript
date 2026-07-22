using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[TestCase("value=value+1")]
		[TestCase("array=(1,2,3);array[1]=array[1]+1;array[1]")]
		[TestCase("dictionary=[a:1,b:2];dictionary.a=dictionary.a+1;dictionary.a")]
		[TestCase("increment=(value)=>{value+1};increment(1)")]
		[TestCase("value=0;while(value<10){value=value+1};value")]
		[TestCase("double=(value)=>{value*2};increment=(value)=>{double(value)+1};increment(3)")]
		[TestCase("root.branch.leaf=value;root.branch.leaf")]
		[TestCase("array=(1,2,3,4,5,6,7,8);array.removeat(7);array.add(9);array.insert(0,0);array.removeat(0)")]
		public void TestEvaluation_DoesNotAllocate(string sentence)
		{
			MoteValue<float> script = _decoder.Decode(sentence);
			var context = new Context();
			context.Set("value", 0);

			// Populate reusable result buffers and dictionary capacities before measuring.
			for (int i = 0; i < 10; ++i)
			{
				script.Evalute(context);
			}

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 1000; ++i)
			{
				script.Evalute(context);
			}
			long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(allocatedBytes, Is.Zero, sentence);
		}

		[Test]
		public void TestDecodeCached_ReusesDecodedScriptWithoutAllocating()
		{
			const string sentence = "value=value+1";
			MoteValue<float> first = _decoder.DecodeCached(sentence);
			MoteValue<float> second = _decoder.DecodeCached(sentence);
			Assert.AreSame(first.GetObject(), second.GetObject());

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 1000; ++i)
			{
				_decoder.DecodeCached(sentence);
			}
			long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.That(allocatedBytes, Is.Zero);

			_decoder.ClearDecodedCache();
			MoteValue<float> afterClear = _decoder.DecodeCached(sentence);
			Assert.AreNotSame(first.GetObject(), afterClear.GetObject());
		}

		[Test]
		public void TestDecodeCached_MultipleCachedSourcesDoNotAllocate()
		{
			string[] sentences =
			{
				"value+1",
				"value*2",
				"value<10",
			};
			foreach (string sentence in sentences)
			{
				_decoder.DecodeCached(sentence);
			}

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 1000; ++i)
			{
				_decoder.DecodeCached(sentences[i % sentences.Length]);
			}
			long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(allocatedBytes, Is.Zero);
		}

		[Test]
		public void TestDecodeCached_IsolatesSequentialContexts()
		{
			MoteValue<float> script = _decoder.DecodeCached("counter=counter+1;counter");
			var firstContext = new Context().Set("counter", 0);
			var secondContext = new Context().Set("counter", 100);

			Assert.AreEqual(1, script.Evalute(firstContext).IntegerValue);
			Assert.AreEqual(101, script.Evalute(secondContext).IntegerValue);
			Assert.AreEqual(2, script.Evalute(firstContext).IntegerValue);
			Assert.AreEqual(102, script.Evalute(secondContext).IntegerValue);
		}

		[Test]
		public void TestDecodeCached_IsolatesFunctionArgumentsAcrossContexts()
		{
			MoteValue<float> script = _decoder.DecodeCached(
				"increment=(value)=>{value+offset};increment(input)");
			var firstContext = new Context().Set("offset", 1).Set("input", 10);
			var secondContext = new Context().Set("offset", 100).Set("input", 20);

			Assert.AreEqual(11, script.Evalute(firstContext).IntegerValue);
			Assert.AreEqual(120, script.Evalute(secondContext).IntegerValue);
			Assert.AreEqual(11, script.Evalute(firstContext).IntegerValue);
		}

		[Test]
		public void TestCachedArrayResult_ReusesItsBuffer()
		{
			MoteValue<float> script = _decoder.DecodeCached("(value,value+1)");
			var context = new Context().Set("value", 1);
			MoteList<float> first = script.Evalute(context).GetArray();
			Assert.AreEqual(1, first[0].IntegerValue);

			context.Set("value", 10);
			MoteList<float> second = script.Evalute(context).GetArray();

			Assert.AreSame(first, second);
			Assert.AreEqual(10, first[0].IntegerValue);
			Assert.AreEqual(11, first[1].IntegerValue);
		}

		[Test]
		public void TestCachedDictionaryResult_ReusesItsBuffer()
		{
			MoteValue<float> script = _decoder.DecodeCached("dictionary=[a:value];dictionary");
			var context = new Context().Set("value", 1);
			IContext<float> first = script.Evalute(context).GetDictionary();
			Assert.AreEqual(1, first["a"].IntegerValue);

			context.Set("value", 10);
			IContext<float> second = script.Evalute(context).GetDictionary();

			Assert.AreSame(first, second);
			Assert.AreEqual(10, first["a"].IntegerValue);
		}
	}
}
