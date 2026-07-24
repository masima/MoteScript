using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[TestCase("(1+2", "閉じ括弧なし")]
		[TestCase("1+2)", "対応する括弧なし")]
		[TestCase("value=", "right value nothing")]
		[TestCase("dictionary=[invalid];dictionary", "dictionary entry is invalid")]
		[TestCase("while(1)", "expected block")]
		public void TestErrors_InvalidSyntaxThrowsFormatException(string sentence, string messagePart)
		{
			MoteScriptParseException exception = Assert.Throws<MoteScriptParseException>(
				() => _decoder.Decode(sentence));
			StringAssert.Contains(messagePart, exception.Message);
			StringAssert.Contains($"index {exception.Position}", exception.Message);
			StringAssert.Contains("^", exception.Message);
			Assert.IsNotNull(exception.InnerException);
		}

		[Test]
		public void TestErrors_ParseExceptionProvidesSourceLocation()
		{
			const string sentence = "value=1;\nresult=(value+2";

			MoteScriptParseException exception = Assert.Throws<MoteScriptParseException>(
				() => _decoder.Decode(sentence));

			Assert.AreEqual(sentence, exception.Source);
			Assert.AreEqual(exception.Source.Length, exception.Position);
			Assert.AreEqual(2, exception.Line);
			Assert.AreEqual("result=(value+2".Length + 1, exception.Column);
			Assert.AreEqual("result=(value+2", exception.SourceLine);
			StringAssert.Contains("閉じ括弧なし", exception.Message);
		}

		[Test]
		public void TestErrors_UndefinedVariableThrowsInvalidOperationException()
		{
			MoteValue<float> script = _decoder.Decode("missing");
			var context = new Context()
				.Set("score", 10)
				.Set("bonus", 1);

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => script.Evaluate(context));
			StringAssert.Contains("Undefined variable : missing", exception.Message);
			StringAssert.Contains("Available variables: bonus, score", exception.Message);
		}
	}
}
