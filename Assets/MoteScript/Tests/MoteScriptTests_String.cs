using System;
using System.Globalization;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[Test]
		public void TestString_LiteralAndEscapes()
		{
			MoteValue<float> value = _decoder.Decode(
				"text=\"a;b,//(c)[d]{e},f:g=>h\\n\\t\\\"quote\\\"\\\\\";text")
				.Evaluate(new Context());

			Assert.AreEqual(EValueType.String, value.ValueType);
			Assert.AreEqual("a;b,//(c)[d]{e},f:g=>h\n\t\"quote\"\\", value.StringValue);
		}

		[Test]
		public void TestString_InvalidLiteralReportsParseError()
		{
			StringAssert.Contains(
				"unterminated string literal",
				Assert.Throws<MoteScriptParseException>(
					() => _decoder.Decode("\"unterminated")).Message);
			StringAssert.Contains(
				"unsupported string escape",
				Assert.Throws<MoteScriptParseException>(
					() => _decoder.Decode("\"invalid\\u\"")).Message);
		}

		[Test]
		public void TestString_NullAndEmptyAreDistinct()
		{
			var context = new Context();
			StandardStringLibrary<float>.Register(context);

			Assert.IsNull(_decoder.Decode("null").Evaluate(context).StringValue);
			Assert.AreEqual(string.Empty, _decoder.Decode("\"\"").Evaluate(context).StringValue);
			Assert.AreEqual(1, _decoder.Decode("string.isNull(null)").Evaluate(context).IntegerValue);
			Assert.AreEqual(0, _decoder.Decode("string.isNull(\"\")").Evaluate(context).IntegerValue);
			Assert.AreEqual(0, _decoder.Decode("string.isEmpty(null)").Evaluate(context).IntegerValue);
			Assert.AreEqual(1, _decoder.Decode("string.isEmpty(\"\")").Evaluate(context).IntegerValue);
		}

		[Test]
		public void TestString_AssignmentLocksVariableType()
		{
			Assert.AreEqual(
				"copy",
				_decoder.Decode("source=\"copy\";destination=source;destination")
					.Evaluate(new Context()).StringValue);

			InvalidOperationException numberToString = Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("value=1;value=\"text\"").Evaluate(new Context()));
			StringAssert.Contains("Cannot assign String to Number", numberToString.Message);

			InvalidOperationException stringToNumber = Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("value=\"text\";value=1").Evaluate(new Context()));
			StringAssert.Contains("Cannot assign Number to String", stringToNumber.Message);

			var context = new Context();
			_decoder.Decode("value=\"first\"").Evaluate(context);
			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("value=2").Evaluate(context));
		}

		[Test]
		public void TestString_AssignmentLocksArrayElementType()
		{
			MoteValue<float> result = _decoder.Decode(
				"values=(\"a\",\"b\");values[0]=\"updated\";values[0]")
				.Evaluate(new Context());
			Assert.AreEqual("updated", result.StringValue);

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("values=(1,2);values[0]=\"text\"")
					.Evaluate(new Context()));
			StringAssert.Contains("Cannot assign String to Number", exception.Message);
		}

		[Test]
		public void TestString_AssignmentLocksDictionaryMemberType()
		{
			MoteValue<float> result = _decoder.Decode(
				"data=[value:\"a\"];data.value=\"updated\";data.value")
				.Evaluate(new Context());
			Assert.AreEqual("updated", result.StringValue);

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("data=[value:1];data.value=\"text\"")
					.Evaluate(new Context()));
			StringAssert.Contains("Cannot assign String to Number", exception.Message);

			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("data=[value:1];data[\"value\"]=\"text\"")
					.Evaluate(new Context()));
			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("data=1;data.value=\"text\"")
					.Evaluate(new Context()));
			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("data.child=1;data.child.value=\"text\"")
					.Evaluate(new Context()));
		}

		[TestCase("\"text\"+1")]
		[TestCase("1-\"text\"")]
		[TestCase("\"text\"*2")]
		[TestCase("\"text\"/2")]
		[TestCase("\"text\"%2")]
		[TestCase("\"text\"==1")]
		[TestCase("\"text\"<1")]
		[TestCase("\"text\"&&1")]
		public void TestString_NumericOperatorsRejectStrings(string sentence)
		{
			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode(sentence).Evaluate(new Context()));
			StringAssert.Contains("requires a numeric value", exception.Message);
		}

		[Test]
		public void TestString_StandardLibraryBasicOperations()
		{
			var context = new Context();
			StandardStringLibrary<float>.Register(context);

			Assert.AreEqual("abc", EvaluateString("string.concat(\"a\",\"b\",\"c\")", context));
			Assert.AreEqual(3, EvaluateNumber("string.length(\"abc\")", context));
			Assert.AreEqual("bc", EvaluateString("string.substring(\"abcd\",1,2)", context));
			Assert.AreEqual("aXXd", EvaluateString("string.replace(\"abcd\",\"bc\",\"XX\")", context));
			Assert.AreEqual("abc", EvaluateString("string.trim(\"  abc  \")", context));
			Assert.AreEqual("abc", EvaluateString("string.toLower(\"ABC\")", context));
			Assert.AreEqual("ABC", EvaluateString("string.toUpper(\"abc\")", context));
			Assert.AreEqual(1, EvaluateNumber("string.indexOf(\"abc\",\"b\")", context));
			Assert.Less(EvaluateNumber("string.compare(\"A\",\"a\")", context), 0);
			Assert.AreEqual(1, EvaluateNumber("string.equals(\"abc\",\"abc\")", context));
			Assert.AreEqual(0, EvaluateNumber("string.equals(\"ABC\",\"abc\")", context));
			Assert.AreEqual(1, EvaluateNumber("string.contains(\"abc\",\"b\")", context));
			Assert.AreEqual(1, EvaluateNumber("string.startsWith(\"abc\",\"a\")", context));
			Assert.AreEqual(1, EvaluateNumber("string.endsWith(\"abc\",\"c\")", context));
		}

		[Test]
		public void TestString_FormatUsesCSharpCompositeFormatting()
		{
			var context = new Context();
			StandardStringLibrary<float>.Register(context);

			Assert.AreEqual(
				"value: 12.50",
				EvaluateString("string.format(\"{0}: {1:0.00}\",\"value\",12.5)", context));
			Assert.AreEqual(
				"[    x]",
				EvaluateString("string.format(\"[{0,5}]\",\"x\")", context));
			Assert.AreEqual(
				"{x}",
				EvaluateString("string.format(\"{{{0}}}\",\"x\")", context));
			Assert.AreEqual(
				"null=[]",
				EvaluateString("string.format(\"null=[{0}]\",null)", context));

			Assert.Throws<FormatException>(
				() => _decoder.Decode("string.format(\"{0\")").Evaluate(context));
			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("string.format(\"{0}\",(1,2))").Evaluate(context));
		}

		[Test]
		public void TestString_StandardLibraryInsertRemoveAndExplicitConversion()
		{
			var context = new Context();
			StandardStringLibrary<float>.Register(context);

			Assert.AreEqual("aXbc", EvaluateString("string.insert(\"abc\",1,\"X\")", context));
			Assert.AreEqual("ad", EvaluateString("string.remove(\"abcd\",1,2)", context));
			Assert.AreEqual(12.5f, _decoder.Decode("string.parseNumber(\"12.5\")")
				.Evaluate(context).Value);
			Assert.AreEqual("12.5", EvaluateString("string.fromNumber(12.5)", context));

			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("string.length(1)").Evaluate(context));
			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("string.fromNumber(\"1\")").Evaluate(context));
		}

		[Test]
		public void TestString_ExplicitConversionUsesInvariantCulture()
		{
			CultureInfo previousCulture = CultureInfo.CurrentCulture;
			try
			{
				CultureInfo.CurrentCulture = new CultureInfo("de-DE");
				var context = new Context();
				StandardStringLibrary<float>.Register(context);

				Assert.AreEqual(
					12.5f,
					_decoder.Decode("string.parseNumber(\"12.5\")").Evaluate(context).Value);
				Assert.AreEqual("12.5", EvaluateString("string.fromNumber(12.5)", context));
			}
			finally
			{
				CultureInfo.CurrentCulture = previousCulture;
			}
		}

		[Test]
		public void TestString_DecimalExplicitConversion()
		{
			MoteDecoder<decimal>.Setup();
			var decoder = new MoteDecoder<decimal>();
			var context = new Context<decimal>();
			StandardStringLibrary<decimal>.Register(context);

			Assert.AreEqual(
				0.1m,
				decoder.Decode("string.parseNumber(\"0.1\")").Evaluate(context).Value);
			Assert.AreEqual(
				"0.1",
				decoder.Decode("string.fromNumber(0.1)").Evaluate(context).StringValue);
		}

		private string EvaluateString(string sentence, Context context)
		{
			return _decoder.Decode(sentence).Evaluate(context).StringValue;
		}

		private int EvaluateNumber(string sentence, Context context)
		{
			return _decoder.Decode(sentence).Evaluate(context).IntegerValue;
		}
	}
}
