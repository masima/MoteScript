#if MOTESCRIPT_DEBUGGER
using System;
using NUnit.Framework;

namespace MoteScript.Tests
{
	public class MoteScriptTests_Debugger
	{
		[SetUp]
		public void SetUp()
		{
			MoteDecoder<float>.Setup();
		}

		[Test]
		public void BreakpointOnContinuationLineUsesStatementStartLine()
		{
			const string source =
				"a =\n"
				+ "  1 + 2;\n"
				+ "b=4;";
			var decoder = new MoteDecoder<float>();
			decoder.DecodeDebug(
				source,
				"multiline.mote",
				out MoteDebugDocument document);

			Assert.That(document.NormalizeBreakpointLine(2), Is.EqualTo(1));
			Assert.That(document.NormalizeBreakpointLine(3), Is.EqualTo(3));
		}

		[Test]
		public void BreakpointPausesBeforeLineAndContinueCompletes()
		{
			const string source = "a=1;\nb=2;\na+b;";
			var decoder = new MoteDecoder<float>();
			MoteValue<float> script = decoder.DecodeDebug(
				source,
				"breakpoint.mote",
				out MoteDebugDocument document);
			var session = new MoteDebugSession<float>(document);
			session.SetBreakpoint(2);
			var context = new Context<float>();
			MoteExecution<float> execution =
				script.CreateDebugExecution(context, session);

			execution.Evaluate();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Paused));
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(2));
			Assert.That(context.ContainsKey("a"), Is.True);
			Assert.That(context.ContainsKey("b"), Is.False);

			execution.ContinueDebugging();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Completed));
			Assert.That(execution.Result.Value, Is.EqualTo(3f));
		}

		[Test]
		public void StepIntoStopsAtEachStatement()
		{
			const string source = "a=1;\na=a+1;\na;";
			var decoder = new MoteDecoder<float>();
			MoteValue<float> script = decoder.DecodeDebug(
				source,
				"step-into.mote",
				out MoteDebugDocument document);
			var execution = script.CreateDebugExecution(
				new Context<float>(),
				new MoteDebugSession<float>(document));

			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(1));

			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(2));

			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(3));

			execution.StepInto();
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Completed));
			Assert.That(execution.Result.Value, Is.EqualTo(2f));
		}

		[Test]
		public void SameLineIsOnlyOneDebugStop()
		{
			const string source = "a=1;b=2;\nc=3;";
			var decoder = new MoteDecoder<float>();
			MoteValue<float> script = decoder.DecodeDebug(
				source,
				"same-line.mote",
				out MoteDebugDocument document);
			var session = new MoteDebugSession<float>(document);
			session.SetBreakpoint(1);
			var execution = script.CreateDebugExecution(
				new Context<float>(),
				session);

			execution.Evaluate();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(1));

			execution.StepInto();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Paused));
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(2));
		}

		[Test]
		public void StepOverSkipsFunctionBody()
		{
			const string source =
				"f=()=>{\n"
				+ "  x=1;\n"
				+ "  return x;\n"
				+ "};\n"
				+ "result=f();\n"
				+ "result=result+1;";
			MoteExecution<float> execution = CreateExecution(source, "step-over.mote");

			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(1));
			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(5));

			execution.StepOver();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Paused));
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(6));
			Assert.That(execution.FunctionDepth, Is.EqualTo(0));
		}

		[Test]
		public void StepOutStopsAfterCurrentFunction()
		{
			const string source =
				"f=()=>{\n"
				+ "  x=1;\n"
				+ "  x=x+1;\n"
				+ "  return x;\n"
				+ "};\n"
				+ "result=f();\n"
				+ "result=result+1;";
			MoteExecution<float> execution = CreateExecution(source, "step-out.mote");

			execution.StepInto();
			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(6));
			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(2));
			Assert.That(execution.FunctionDepth, Is.EqualTo(1));
			var frames = ((IMoteDebugExecution)execution).GetCallStack();
			Assert.That(frames.Count, Is.EqualTo(1));
			Assert.That(frames[0].FunctionName, Is.EqualTo("f"));
			Assert.That(frames[0].CallSite?.StartLine, Is.EqualTo(6));

			execution.StepOut();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Paused));
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(7));
			Assert.That(execution.FunctionDepth, Is.EqualTo(0));
		}

		[Test]
		public void StopRestoresFunctionScopeAndReleasesContext()
		{
			const string source =
				"f=(value; local temporary)=>{\n"
				+ "  temporary=value+1;\n"
				+ "  return temporary;\n"
				+ "};\n"
				+ "result=f(2);";
			var decoder = new MoteDecoder<float>();
			MoteValue<float> script = decoder.DecodeDebug(
				source,
				"stop.mote",
				out MoteDebugDocument document);
			var context = new Context<float>();
			MoteExecution<float> execution = script.CreateDebugExecution(
				context,
				new MoteDebugSession<float>(document));

			execution.StepInto();
			execution.StepInto();
			execution.StepInto();
			Assert.That(execution.CurrentLocation?.StartLine, Is.EqualTo(2));
			Assert.That(context.ContainsKey("value"), Is.True);

			execution.StopDebugging();

			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Stopped));
			Assert.That(context.ContainsKey("value"), Is.False);
			Assert.That(context.ContainsKey("temporary"), Is.False);
			Assert.That(
				((IExecutionContext<float>)context).CurrentExecution,
				Is.Null);
		}

		[Test]
		public void DebugPauseAndScriptYieldRemainDistinct()
		{
			const string source = "a=1;\nyield(a);\na=2;";
			var decoder = new MoteDecoder<float>();
			MoteValue<float> script = decoder.DecodeDebug(
				source,
				"yield.mote",
				out MoteDebugDocument document);
			var session = new MoteDebugSession<float>(document);
			session.SetBreakpoint(1);
			MoteExecution<float> execution =
				script.CreateDebugExecution(new Context<float>(), session);

			execution.Evaluate();
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Paused));

			execution.ContinueDebugging();
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Yielded));
			Assert.That(execution.YieldValue.Value, Is.EqualTo(1f));

			execution.Resume();
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Completed));
		}

		[Test]
		public void ExceptionIncludesLastStatementLocation()
		{
			const string source = "a=1;\nmissing+1;";
			var decoder = new MoteDecoder<float>();
			MoteValue<float> script = decoder.DecodeDebug(
				source,
				"exception.mote",
				out MoteDebugDocument document);
			var session = new MoteDebugSession<float>(document);
			MoteSourceSpan? exceptionLocation = null;
			session.ExceptionThrown += (_, location, _) =>
				exceptionLocation = location;
			MoteExecution<float> execution =
				script.CreateDebugExecution(new Context<float>(), session);

			Assert.Throws<InvalidOperationException>(() => execution.Evaluate());
			Assert.That(execution.State, Is.EqualTo(MoteExecutionState.Faulted));
			Assert.That(exceptionLocation?.StartLine, Is.EqualTo(2));
		}

		private static MoteExecution<float> CreateExecution(
			string source,
			string sourceName)
		{
			var decoder = new MoteDecoder<float>();
			MoteValue<float> script = decoder.DecodeDebug(
				source,
				sourceName,
				out MoteDebugDocument document);
			return script.CreateDebugExecution(
				new Context<float>(),
				new MoteDebugSession<float>(document));
		}
	}
}
#endif
