using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MoteScript.Editor;
using MoteScript.Unity;

namespace MoteScript.Tests
{
	public sealed class MoteScriptTests_DebuggerWindowLifetime
	{
		private static readonly BindingFlags InstancePrivate =
			BindingFlags.Instance | BindingFlags.NonPublic;
		private static readonly BindingFlags StaticPrivate =
			BindingFlags.Static | BindingFlags.NonPublic;

		[Test]
		public void DestroyedSourceAsset_IsClearedBeforeDrawingWindow()
		{
			MoteDebuggerWindow window =
				ScriptableObject.CreateInstance<MoteDebuggerWindow>();
			var source = new TextAsset("1+2");
			FieldInfo sourceField = typeof(MoteDebuggerWindow).GetField(
				"_sourceAsset",
				InstancePrivate);
			MethodInfo sanitize = typeof(MoteDebuggerWindow).GetMethod(
				"SanitizeDestroyedReferences",
				InstancePrivate);
			try
			{
				sourceField.SetValue(window, source);
				UnityEngine.Object.DestroyImmediate(source);

				sanitize.Invoke(window, null);

				Assert.That(sourceField.GetValue(window), Is.Null);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(window);
			}
		}

		[Test]
		public void RegisteredTargetExecution_DoesNotChangeWindowSelection()
		{
			MoteDebuggerWindow window =
				ScriptableObject.CreateInstance<MoteDebuggerWindow>();
			var target = new DebugTarget();
			MethodInfo register = typeof(MoteDebugTargetRegistry).GetMethod(
				"Register",
				StaticPrivate);
			MethodInfo unregister = typeof(MoteDebugTargetRegistry).GetMethod(
				"Unregister",
				StaticPrivate);
			FieldInfo selectedTarget = typeof(MoteDebuggerWindow).GetField(
				"_selectedTarget",
				InstancePrivate);
			FieldInfo selectedExecution = typeof(MoteDebuggerWindow).GetField(
				"_execution",
				InstancePrivate);
			try
			{
				register.Invoke(null, new object[] { target });

				Assert.That(selectedTarget.GetValue(window), Is.Null);
				Assert.That(selectedExecution.GetValue(window), Is.Null);

				MoteDecoder<float>.Setup();
				var decoder = new MoteDecoder<float>();
				MoteValue<float> script = decoder.DecodeDebug(
					"value=1;",
					"Target.mote",
					out MoteDebugDocument document);
				target.Execution = script.CreateDebugExecution(
					new Context<float>(),
					new MoteDebugSession<float>(document));

				Assert.That(selectedTarget.GetValue(window), Is.Null);
				Assert.That(selectedExecution.GetValue(window), Is.Null);
			}
			finally
			{
				unregister.Invoke(null, new object[] { target });
				UnityEngine.Object.DestroyImmediate(window);
			}
		}

		private sealed class DebugTarget : IMoteDebugTarget
		{
			public string DisplayName => "Target";
			public UnityEngine.Object Owner => null;
			public TextAsset SourceAsset => null;
			public bool IsManager => false;
			public MoteScriptLifecycleState LifecycleState =>
				MoteScriptLifecycleState.Updating;
			public IMoteDebugExecution Execution { get; set; }
			public Exception Exception => null;
		}
	}
}
