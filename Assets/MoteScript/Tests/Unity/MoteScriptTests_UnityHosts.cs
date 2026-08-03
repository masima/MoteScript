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

		private const BindingFlags PrivateInstance =
			BindingFlags.Instance | BindingFlags.NonPublic;

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

		[Test]
		public void Manager_DrivesLifecycleFunctionsInOrder()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			SetPrivateField(manager, "_script", new TextAsset(
				"Awake=()=>{shared.order=1};"
				+ "Start=()=>{shared.order=shared.order*10+2};"
				+ "Update=()=>{shared.order=shared.order*10+3};"));
			InvokeLifecycle(manager, "Awake");

			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{shared.order=shared.order*10+4};"
				+ "Start=()=>{shared.order=shared.order*10+5};"
				+ "Update=()=>{shared.order=shared.order*10+6};"));
			InvokeLifecycle(behaviour, "OnEnable");

			InvokeLifecycle(manager, "Start");
			InvokeLifecycle(manager, "Update");

			Assert.That(
				manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(142536));
			Assert.That(manager.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Updating));
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Updating));
		}

		[Test]
		public void StartYield_CompletesBeforeUpdateBegins()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			InvokeLifecycle(manager, "Awake");
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{shared.updates=0};"
				+ "Start=()=>{shared.phase=1;yield(10);shared.phase=2};"
				+ "Update=()=>{shared.updates=shared.updates+1};"));
			InvokeLifecycle(behaviour, "OnEnable");

			InvokeLifecycle(manager, "Start");
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Starting));
			Assert.That(behaviour.ExecutionState, Is.EqualTo(
				MoteExecutionState.Yielded));
			Assert.That(manager.GetSharedVariables<float>()["phase"].IntegerValue,
				Is.EqualTo(1));

			InvokeLifecycle(manager, "Update");
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Started));
			Assert.That(manager.GetSharedVariables<float>()["phase"].IntegerValue,
				Is.EqualTo(2));
			Assert.That(manager.GetSharedVariables<float>()["updates"].IntegerValue,
				Is.Zero);

			InvokeLifecycle(manager, "Update");
			Assert.That(manager.GetSharedVariables<float>()["updates"].IntegerValue,
				Is.EqualTo(1));
		}

		[Test]
		public void ManagerStartYield_DelaysBehaviourStart()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			SetPrivateField(manager, "_script", new TextAsset(
				"Awake=()=>{shared.order=0};"
				+ "Start=()=>{shared.order=1;yield(0);shared.order=2};"));
			InvokeLifecycle(manager, "Awake");
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Start=()=>{shared.order=shared.order*10+3};"));
			InvokeLifecycle(behaviour, "OnEnable");

			InvokeLifecycle(manager, "Start");
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Awakened));

			InvokeLifecycle(manager, "Update");
			Assert.That(manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(23));
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Started));
		}

		[Test]
		public void UpdateYield_ResumesWithoutOverlappingInvocations()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			InvokeLifecycle(manager, "Awake");
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{shared.calls=0;shared.resumes=0};"
				+ "Update=()=>{shared.calls=shared.calls+1;"
				+ "yield(shared.calls);shared.resumes=shared.resumes+1};"));
			InvokeLifecycle(behaviour, "OnEnable");
			InvokeLifecycle(manager, "Start");

			InvokeLifecycle(manager, "Update");
			Assert.That(behaviour.ExecutionState, Is.EqualTo(
				MoteExecutionState.Yielded));
			Assert.That(manager.GetSharedVariables<float>()["calls"].IntegerValue,
				Is.EqualTo(1));
			Assert.That(manager.GetSharedVariables<float>()["resumes"].IntegerValue,
				Is.Zero);

			InvokeLifecycle(manager, "Update");
			Assert.That(manager.GetSharedVariables<float>()["calls"].IntegerValue,
				Is.EqualTo(1));
			Assert.That(manager.GetSharedVariables<float>()["resumes"].IntegerValue,
				Is.EqualTo(1));

			InvokeLifecycle(manager, "Update");
			Assert.That(manager.GetSharedVariables<float>()["calls"].IntegerValue,
				Is.EqualTo(2));
		}

#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		[Test]
		public void YieldingBehaviour_IsAvailableFromDebugTargetRegistry()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			InvokeLifecycle(manager, "Awake");
			_behaviourObject = new GameObject("Debug Target");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Update=()=>{yield(1)};"));
			InvokeLifecycle(behaviour, "OnEnable");
			InvokeLifecycle(manager, "Start");
			InvokeLifecycle(manager, "Update");

			IMoteDebugTarget target = null;
			foreach (IMoteDebugTarget candidate in MoteDebugTargetRegistry.Targets)
			{
				if (candidate.Owner == behaviour)
				{
					target = candidate;
					break;
				}
			}
			Assert.That(target, Is.Not.Null);
			Assert.That(target.DisplayName, Is.EqualTo("Debug Target"));
			Assert.That(target.Execution.State, Is.EqualTo(
				MoteExecutionState.Yielded));
			Assert.That(target.SourceAsset, Is.SameAs(behaviour.Script));
		}
#endif

		private static void InvokeLifecycle(object target, string methodName)
		{
			target.GetType()
				.GetMethod(methodName, PrivateInstance)
				.Invoke(target, null);
		}

		private static void SetPrivateField(
			object target,
			string fieldName,
			object value)
		{
			target.GetType()
				.GetField(fieldName, PrivateInstance)
				.SetValue(target, value);
		}
	}
}
