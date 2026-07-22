using System.Linq;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[Test]
		public void TestBoundary_EmptyScriptAndContainers()
		{
			Assert.AreEqual(EValueType.Unknown, _decoder.Decode(string.Empty).Evalute(null).ValueType);
			Assert.AreEqual(0, _decoder.Decode("array=();array").Evalute(new Context()).GetArray().Count);
			Assert.AreEqual(0, _decoder.Decode("dictionary=[];dictionary").Evalute(new Context()).GetDictionary().Count);
		}

		[Test]
		public void TestBoundary_LargeArrayLiteral()
		{
			const int elementCount = 128;
			string sentence = "(" + string.Join(",", Enumerable.Range(0, elementCount)) + ")";
			MoteList<float> result = _decoder.Decode(sentence).Evalute(new Context()).GetArray();

			Assert.AreEqual(elementCount, result.Count);
			Assert.AreEqual(elementCount - 1, result[elementCount - 1].IntegerValue);
		}

		[Test]
		public void TestBoundary_LargeDictionaryLiteral()
		{
			const int elementCount = 64;
			string entries = string.Join(",", Enumerable.Range(0, elementCount).Select(i => $"key{i}:{i}"));
			IContext<float> result = _decoder.Decode($"dictionary=[{entries}];dictionary")
				.Evalute(new Context()).GetDictionary();

			Assert.AreEqual(elementCount, result.Count);
			Assert.AreEqual(elementCount - 1, result[$"key{elementCount - 1}"].IntegerValue);
		}

		[Test]
		public void TestBoundary_DeepParentheses()
		{
			string sentence = "1+2";
			for (int i = 0; i < 32; ++i)
			{
				sentence = $"({sentence})";
			}

			Assert.AreEqual(3, _decoder.Decode(sentence).Evalute(null).IntegerValue);
		}

		[Test]
		public void TestBoundary_DeepIfBlocks()
		{
			string sentence = "value=0;";
			for (int i = 0; i < 8; ++i) sentence += "if(1){";
			sentence += "value=1";
			for (int i = 0; i < 8; ++i) sentence += "}";
			sentence += ";value";

			Assert.AreEqual(1, _decoder.Decode(sentence).Evalute(new Context()).IntegerValue);
		}

		[Test]
		public void TestBoundary_LongDictionaryPath()
		{
			string[] path = Enumerable.Range(0, 16).Select(i => $"level{i}").ToArray();
			string joinedPath = string.Join(".", path);
			var context = new Context();
			_decoder.Decode($"{joinedPath}=123").Evalute(context);

			Assert.AreEqual(123, context.GetByPath(path).IntegerValue);
		}

		[Test]
		public void TestBoundary_ZeroAndManyFunctionArguments()
		{
			Assert.AreEqual(1,
				_decoder.Decode("constant=()=>{1};constant()").Evalute(new Context()).IntegerValue);

			const int parameterCount = 8;
			string parameters = string.Join(",", Enumerable.Range(0, parameterCount).Select(i => $"p{i}"));
			string expression = string.Join("+", Enumerable.Range(0, parameterCount).Select(i => $"p{i}"));
			string arguments = string.Join(",", Enumerable.Range(1, parameterCount));
			string sentence = $"sum=({parameters})=>{{{expression}}};sum({arguments})";

			Assert.AreEqual(36, _decoder.Decode(sentence).Evalute(new Context()).IntegerValue);
		}
	}
}
