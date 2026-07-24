using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public partial class MoteScriptTests
	{
		[Test]
		public void TestRegex_IsMatchAndReplace()
		{
			var context = new Context();
			StandardRegexLibrary<float>.Register(context);

			Assert.AreEqual(
				1,
				_decoder.Decode("regex.isMatch(\"abc123\",\"\\\\d+\")")
					.Evaluate(context).IntegerValue);
			Assert.AreEqual(
				"07/25/2026",
				_decoder.Decode(
						"regex.replace(\"2026-07-25\",\"(\\\\d{4})-(\\\\d{2})-(\\\\d{2})\",\"$2/$3/$1\")")
					.Evaluate(context).StringValue);
		}

		[Test]
		public void TestRegex_MatchReturnsMatchAndGroupInformation()
		{
			var context = new Context();
			StandardRegexLibrary<float>.Register(context);

			_decoder.Decode(
					"result=regex.match(\"abc123\",\"([a-z]+)(\\\\d+)\");")
				.Evaluate(context);

			Assert.IsTrue(context.TryGetValue("result", out MoteValue<float> result));
			Assert.AreEqual(EValueType.Dictionary, result.ValueType);
			Assert.Greater(result.GetDictionary().Count, 0);
			Assert.AreEqual(1, _decoder.Decode("result.success").Evaluate(context).IntegerValue);
			Assert.AreEqual("abc123", _decoder.Decode("result.value").Evaluate(context).StringValue);
			Assert.AreEqual(0, _decoder.Decode("result.index").Evaluate(context).IntegerValue);
			Assert.AreEqual(6, _decoder.Decode("result.length").Evaluate(context).IntegerValue);
			_decoder.Decode("groups=result.groups;").Evaluate(context);
			Assert.AreEqual("abc", _decoder.Decode("groups[1].value").Evaluate(context).StringValue);
			Assert.AreEqual("123", _decoder.Decode("groups[2].value").Evaluate(context).StringValue);
		}

		[Test]
		public void TestRegex_MatchFailureReturnsUnsuccessfulResult()
		{
			var context = new Context();
			StandardRegexLibrary<float>.Register(context);

			_decoder.Decode("result=regex.match(\"abc\",\"\\\\d+\");").Evaluate(context);

			Assert.IsTrue(context.TryGetValue("result", out MoteValue<float> result));
			Assert.AreEqual(EValueType.Dictionary, result.ValueType);
			Assert.Greater(result.GetDictionary().Count, 0);
			Assert.AreEqual(0, _decoder.Decode("result.success").Evaluate(context).IntegerValue);
			Assert.AreEqual(string.Empty, _decoder.Decode("result.value").Evaluate(context).StringValue);
		}

		[Test]
		public void TestRegex_MatchesReturnsAllResults()
		{
			var context = new Context();
			StandardRegexLibrary<float>.Register(context);

			_decoder.Decode("results=regex.matches(\"a12 b345\",\"\\\\d+\");").Evaluate(context);

			Assert.IsTrue(context.TryGetValue("results", out MoteValue<float> results));
			Assert.AreEqual(EValueType.Array, results.ValueType);
			Assert.AreEqual(2, results.GetArray().Count);
			Assert.AreEqual(2, _decoder.Decode("results.count").Evaluate(context).IntegerValue);
			Assert.AreEqual("12", _decoder.Decode("results[0].value").Evaluate(context).StringValue);
			Assert.AreEqual(1, _decoder.Decode("results[0].index").Evaluate(context).IntegerValue);
			Assert.AreEqual("345", _decoder.Decode("results[1].value").Evaluate(context).StringValue);
			Assert.AreEqual(5, _decoder.Decode("results[1].index").Evaluate(context).IntegerValue);
		}

		[Test]
		public void TestRegex_CachesPatternsWithLruLimit()
		{
			var context = new Context();
			StandardRegexLibrary<float>.Register(context, maxCachedPatterns: 2);

			_decoder.Decode("regex.isMatch(\"a\",\"a\")").Evaluate(context);
			_decoder.Decode("regex.isMatch(\"b\",\"b\")").Evaluate(context);
			_decoder.Decode("regex.isMatch(\"c\",\"c\")").Evaluate(context);

			Assert.AreEqual(2, StandardRegexLibrary<float>.GetCachedPatternCount(context));
			StandardRegexLibrary<float>.ClearCache(context);
			Assert.AreEqual(0, StandardRegexLibrary<float>.GetCachedPatternCount(context));
		}

		[Test]
		public void TestRegex_UsesConfiguredOptions()
		{
			var context = new Context();
			StandardRegexLibrary<float>.Register(
				context,
				options: RegexOptions.IgnoreCase);

			Assert.AreEqual(
				1,
				_decoder.Decode("regex.isMatch(\"MOTE\",\"mote\")")
					.Evaluate(context).IntegerValue);
		}

		[Test]
		public void TestRegex_RejectsInvalidRegistrationAndArguments()
		{
			Assert.Throws<ArgumentException>(
				() => StandardRegexLibrary<float>.Register(
					new Context(),
					options: RegexOptions.Compiled));
			Assert.Throws<ArgumentOutOfRangeException>(
				() => StandardRegexLibrary<float>.Register(
					new Context(),
					matchTimeout: Regex.InfiniteMatchTimeout));
			Assert.Throws<ArgumentOutOfRangeException>(
				() => StandardRegexLibrary<float>.Register(
					new Context(),
					maxCachedPatterns: 0));

			var context = new Context();
			StandardRegexLibrary<float>.Register(context);
			Assert.Throws<InvalidOperationException>(
				() => _decoder.Decode("regex.isMatch(1,\"1\")").Evaluate(context));
			Assert.Throws<ArgumentNullException>(
				() => _decoder.Decode("regex.isMatch(null,\"a\")").Evaluate(context));
			Assert.Throws<ArgumentException>(
				() => _decoder.Decode("regex.isMatch(\"a\",\"[\")").Evaluate(context));
		}

		[Test]
		public void TestRegex_EnforcesMatchTimeout()
		{
			var context = new Context();
			StandardRegexLibrary<float>.Register(
				context,
				matchTimeout: TimeSpan.FromMilliseconds(1));
			string input = new string('a', 10000) + "!";
			context["input"] = new MoteValue<float>(input);

			Assert.Throws<RegexMatchTimeoutException>(
				() => _decoder.Decode("regex.isMatch(input,\"^(a+)+$\")").Evaluate(context));
		}
	}
}
