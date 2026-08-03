using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MoteScript.Editor;

namespace MoteScript.Tests
{
	public sealed class MoteScriptTests_DebuggerWindowLifetime
	{
		[Test]
		public void DestroyedSourceAsset_IsClearedBeforeDrawingWindow()
		{
			MoteDebuggerWindow window =
				ScriptableObject.CreateInstance<MoteDebuggerWindow>();
			var source = new TextAsset("1+2");
			FieldInfo sourceField = typeof(MoteDebuggerWindow).GetField(
				"_sourceAsset",
				BindingFlags.Instance | BindingFlags.NonPublic);
			MethodInfo sanitize = typeof(MoteDebuggerWindow).GetMethod(
				"SanitizeDestroyedReferences",
				BindingFlags.Instance | BindingFlags.NonPublic);
			try
			{
				sourceField.SetValue(window, source);
				Object.DestroyImmediate(source);

				sanitize.Invoke(window, null);

				Assert.That(sourceField.GetValue(window), Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(window);
			}
		}
	}
}
