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
				MoteScriptBehaviour behaviour =
					_behaviourObject.GetComponent<MoteScriptBehaviour>();
				if (behaviour is not null) InvokeLifecycle(behaviour, "OnDisable");
				Object.DestroyImmediate(_behaviourObject);
			}
			if (_managerObject is not null)
			{
				MoteScriptManager manager =
					_managerObject.GetComponent<MoteScriptManager>();
				if (manager is not null) InvokeLifecycle(manager, "OnDestroy");
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

		[Test]
		public void Behaviour_UsesIndependentContextWithSharedDictionaries()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			InvokeLifecycle(manager, "Awake");
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			InvokeLifecycle(behaviour, "OnEnable");

			Context<float> managerContext = manager.GetContext<float>();
			Context<float> hostContext = behaviour.GetContext<float>();
			Context<float> shared = manager.GetSharedVariables<float>();
			Context<float> libraries = manager.GetSharedLibraries<float>();

			Assert.That(hostContext, Is.Not.SameAs(managerContext));
			Assert.That(hostContext["shared"].GetDictionary(), Is.SameAs(shared));
			Assert.That(hostContext["library"].GetDictionary(), Is.SameAs(libraries));
			Assert.That(managerContext["shared"].GetDictionary(), Is.SameAs(shared));

			shared.Set("score", 10);
			Assert.That(
				hostContext.GetByPath("shared.score").IntegerValue,
				Is.EqualTo(10));
		}

		private static void InvokeLifecycle(object target, string methodName)
		{
			target.GetType()
				.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(target, null);
		}
	}
}
