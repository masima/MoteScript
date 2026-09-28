using MoteScript.Editor;
using NUnit.Framework;
using System.Linq;

namespace MoteScript.Tests
{
	internal sealed class MoteScriptTests_ImmediateSession
	{
		[Test]
		public void Execute_ReusesContextBetweenInputs()
		{
			var session = new MoteImmediateSession<float>();

			MoteImmediateResult assignment = session.Execute("value=10");
			MoteImmediateResult evaluation = session.Execute("value*2");

			Assert.That(assignment.IsError, Is.False);
			Assert.That(evaluation.IsError, Is.False);
			Assert.That(evaluation.Output, Is.EqualTo("20"));
		}

		[Test]
		public void Execute_ReusesFunctionDefinitions()
		{
			var session = new MoteImmediateSession<float>();

			MoteImmediateResult definition = session.Execute(
				"double=(value)=>{return value*2}");
			MoteImmediateResult evaluation = session.Execute("double(6)");

			Assert.That(definition.IsError, Is.False);
			Assert.That(evaluation.Output, Is.EqualTo("12"));
		}

		[Test]
		public void Execute_RegistersStandardLibraries()
		{
			var session = new MoteImmediateSession<float>();

			MoteImmediateResult result = session.Execute("math.clamp(20,0,5)");

			Assert.That(result.IsError, Is.False);
			Assert.That(result.Output, Is.EqualTo("5"));
		}

		[Test]
		public void Execute_RejectsMultipleLines()
		{
			var session = new MoteImmediateSession<float>();

			MoteImmediateResult result = session.Execute("a=1\na+1");

			Assert.That(result.IsError, Is.True);
			Assert.That(result.Output, Does.Contain("one line"));
		}

		[Test]
		public void Execute_ReportsYieldAsErrorAndKeepsContextUsable()
		{
			var session = new MoteImmediateSession<float>();

			MoteImmediateResult yielded = session.Execute("yield(1)");
			MoteImmediateResult next = session.Execute("1+1");

			Assert.That(yielded.IsError, Is.True);
			Assert.That(next.IsError, Is.False);
			Assert.That(next.Output, Is.EqualTo("2"));
		}

		[Test]
		public void Execute_UsesRequestedNumericType()
		{
			var session = new MoteImmediateSession<decimal>();

			MoteImmediateResult result = session.Execute("1/4");

			Assert.That(result.IsError, Is.False);
			Assert.That(result.Output, Is.EqualTo("0.25"));
		}

		[Test]
		public void GetVariables_ReflectsLatestContextValues()
		{
			var session = new MoteImmediateSession<float>();
			session.Execute("score=10");
			session.Execute("score=score+5");

			MoteDebugVariable score = session
				.GetVariables()
				.Single(variable => variable.Name == "score");

			Assert.That(score.Type, Is.EqualTo("Const"));
			Assert.That(score.Value, Is.EqualTo("15"));
			Assert.That(score.CanEdit, Is.False);
		}

		[Test]
		public void GetVariables_BuildsArrayHierarchy()
		{
			var session = new MoteImmediateSession<float>();
			session.Execute("values=(0,0)");
			session.Execute("values[0]=3");

			MoteDebugVariable values = session
				.GetVariables()
				.Single(variable => variable.Name == "values");

			Assert.That(values.Children, Has.Count.EqualTo(2));
			Assert.That(values.Children[0].Name, Is.EqualTo("[0]"));
			Assert.That(values.Children[0].Value, Is.EqualTo("3"));
		}
	}
}
