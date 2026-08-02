using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using MoteScript.Unity;

namespace MoteScript.Tests
{
	public sealed class MoteScriptTests_UnityHosts
	{
		private GameObject _managerObject;
		private GameObject _behaviourObject;

		[TearDown]
		public void TearDown()
		{
			if (_behaviourObject is not null)
			{
				Object.DestroyImmediate(_behaviourObject);
			}
			if (_managerObject is not null)
			{
				Object.DestroyImmediate(_managerObject);
			}
		}

		[Test]
		public void Behaviour_RegistersWithManagerWhileEnabled()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			InvokeLifecycle(manager, "Awake");
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			InvokeLifecycle(behaviour, "OnEnable");

			Assert.That(behaviour.Manager, Is.SameAs(manager));
			Assert.That(manager.RegisteredBehaviourCount, Is.EqualTo(1));

			InvokeLifecycle(behaviour, "OnDisable");
			Assert.That(manager.RegisteredBehaviourCount, Is.Zero);
		}

		private static void InvokeLifecycle(object target, string methodName)
		{
			target.GetType()
				.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(target, null);
		}
	}
}
