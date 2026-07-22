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
			FormatException exception = Assert.Throws<FormatException>(() => _decoder.Decode(sentence));
			StringAssert.Contains(messagePart, exception.Message);
		}

		[Test]
		public void TestErrors_UndefinedVariableThrowsInvalidOperationException()
		{
			MoteValue<float> script = _decoder.Decode("missing");

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => script.Evalute(new Context()));
			StringAssert.Contains("Undefined variable : missing", exception.Message);
		}
	}
}
