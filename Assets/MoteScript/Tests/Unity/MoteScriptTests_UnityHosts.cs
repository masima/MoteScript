using NUnit.Framework;
using System;
using System.Collections.Generic;
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
			if (_behaviourObject != null)
			{
				MoteScriptBehaviour behaviour =
					_behaviourObject.GetComponent<MoteScriptBehaviour>();
				if (behaviour is not null) InvokeLifecycle(behaviour, "OnDisable");
				UnityEngine.Object.DestroyImmediate(_behaviourObject);
			}
			_behaviourObject = null;
			if (_managerObject != null)
			{
				MoteScriptManager manager =
					_managerObject.GetComponent<MoteScriptManager>();
				if (manager is not null) InvokeLifecycle(manager, "OnDestroy");
				UnityEngine.Object.DestroyImmediate(_managerObject);
			}
			_managerObject = null;
		}

		[Test]
		public void Behaviour_AppliesInitialVariablesBeforeScriptAwake()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{observed=identifier+step};"));
			SetPrivateField(behaviour, "_initialVariables", new List<MoteInitialVariable>
			{
				new("identifier", 2),
				new("step", 3),
			});

			InvokeLifecycle(behaviour, "OnEnable");

			Context<float> context = behaviour.GetContext<float>();
			Assert.That(context["observed"].FloatValue, Is.EqualTo(5));
		}

		[Test]
		public void Behaviour_InitialVariablesSupportStringAndNull()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_initialVariables", new List<MoteInitialVariable>
			{
				new("text", "value"),
				new("nothing", (string)null),
			});

			InvokeLifecycle(behaviour, "OnEnable");

			Context<float> context = behaviour.GetContext<float>();
			Assert.That(context["text"].StringValue, Is.EqualTo("value"));
			Assert.That(context["nothing"].ValueType, Is.EqualTo(EValueType.String));
			Assert.That(context["nothing"].StringValue, Is.Null);
		}

		[TestCase(MoteNumericType.Integer)]
		[TestCase(MoteNumericType.Decimal)]
		public void Behaviour_InitialNumberUsesManagerNumericType(
			MoteNumericType numericType)
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			SetPrivateField(manager, "_numericType", numericType);
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_initialVariables", new List<MoteInitialVariable>
			{
				new("value", 12),
			});

			InvokeLifecycle(behaviour, "OnEnable");

			if (numericType == MoteNumericType.Integer)
			{
				Assert.That(behaviour.GetContext<int>()["value"].Value, Is.EqualTo(12));
			}
			else
			{
				Assert.That(behaviour.GetContext<decimal>()["value"].Value, Is.EqualTo(12m));
			}
		}

		[Test]
		public void Behaviour_RejectsDuplicateInitialVariableNames()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_initialVariables", new List<MoteInitialVariable>
			{
				new("value", 1),
				new("value", 2),
			});

			TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
				() => InvokeLifecycle(behaviour, "OnEnable"));

			Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(exception.InnerException.Message, Does.Contain("duplicated"));
		}

		[Test]
		public void Behaviour_RejectsReservedInitialVariableName()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_initialVariables", new List<MoteInitialVariable>
			{
				new("shared", 1),
			});

			TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
				() => InvokeLifecycle(behaviour, "OnEnable"));

			Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(exception.InnerException.Message, Does.Contain("reserved"));
		}

		[Test]
		public void Behaviour_RejectsFractionalIntegerInitialValue()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			SetPrivateField(manager, "_numericType", MoteNumericType.Integer);
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_initialVariables", new List<MoteInitialVariable>
			{
				new("value", 1.5),
			});

			TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
				() => InvokeLifecycle(behaviour, "OnEnable"));

			Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(exception.InnerException.Message, Does.Contain("Int32"));
		}

		[Test]
		public void Behaviour_RegistersWithManagerWhileEnabled()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
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
		public void Behaviour_ReenableInvokesEnableWithoutRepeatingAwakeOrStart()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{shared.order=shared.order*10+1};"
				+ "Enable=()=>{shared.order=shared.order*10+2};"
				+ "Start=()=>{shared.order=shared.order*10+3};"));
			manager.GetSharedVariables<float>().Set("order", 0);

			InvokeLifecycle(behaviour, "OnEnable");
			Context<float> firstContext = behaviour.GetContext<float>();
			InvokeLifecycle(manager, "Start");
			Assert.That(
				manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(123));

			InvokeLifecycle(behaviour, "OnDisable");
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Disabled));
			Assert.That(manager.RegisteredBehaviourCount, Is.Zero);

			InvokeLifecycle(behaviour, "OnEnable");
			Assert.That(behaviour.GetContext<float>(), Is.SameAs(firstContext));
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Started));
			Assert.That(manager.RegisteredBehaviourCount, Is.EqualTo(1));
			Assert.That(
				manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(1232));
		}

		[Test]
		public void Behaviour_DisableAndDestroyFollowUnityOrder()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{shared.order=1};"
				+ "Enable=()=>{shared.order=shared.order*10+2};"
				+ "Start=()=>{shared.order=shared.order*10+3};"
				+ "Disable=()=>{shared.order=shared.order*10+4};"
				+ "Destroy=()=>{shared.order=shared.order*10+5};"));

			InvokeLifecycle(behaviour, "OnEnable");
			InvokeLifecycle(manager, "Start");
			InvokeLifecycle(behaviour, "OnDisable");
			InvokeLifecycle(behaviour, "OnEnable");
			InvokeLifecycle(behaviour, "OnDestroy");

			Assert.That(
				manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(1234245));
			Assert.That(manager.RegisteredBehaviourCount, Is.Zero);
			Assert.That(behaviour.Manager, Is.Null);
		}

		[Test]
		public void Manager_ReenableInvokesEnableWithoutRepeatingAwakeOrStart()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			SetPrivateField(manager, "_script", new TextAsset(
				"Awake=()=>{shared.order=1};"
				+ "Enable=()=>{shared.order=shared.order*10+2};"
				+ "Start=()=>{shared.order=shared.order*10+3};"));
			AwakeAndEnableManager(manager);
			InvokeLifecycle(manager, "Start");

			InvokeLifecycle(manager, "OnDisable");
			InvokeLifecycle(manager, "OnEnable");

			Assert.That(manager.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Started));
			Assert.That(
				manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(1232));
		}

		[Test]
		public void Manager_DestroyInvokesDisableThenDestroy()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			SetPrivateField(manager, "_script", new TextAsset(
				"Awake=()=>{shared.order=1};"
				+ "Enable=()=>{shared.order=shared.order*10+2};"
				+ "Start=()=>{shared.order=shared.order*10+3};"
				+ "Disable=()=>{shared.order=shared.order*10+4};"
				+ "Destroy=()=>{shared.order=shared.order*10+5};"));
			AwakeAndEnableManager(manager);
			InvokeLifecycle(manager, "Start");
			Context<float> shared = manager.GetSharedVariables<float>();

			InvokeLifecycle(manager, "OnDestroy");

			Assert.That(shared["order"].IntegerValue, Is.EqualTo(12345));
			Assert.That(manager.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Uninitialized));
		}

		[Test]
		public void Behaviour_ReenablePreservesYieldedUpdateExecution()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{shared.enables=0;shared.resumed=0};"
				+ "Enable=()=>{shared.enables=shared.enables+1};"
				+ "Update=()=>{yield(1);shared.resumed=shared.resumed+1};"));
			InvokeLifecycle(behaviour, "OnEnable");
			InvokeLifecycle(manager, "Start");
			InvokeLifecycle(manager, "Update");
			Assert.That(behaviour.ExecutionState, Is.EqualTo(
				MoteExecutionState.Yielded));

			InvokeLifecycle(behaviour, "OnDisable");
			InvokeLifecycle(behaviour, "OnEnable");
			Assert.That(behaviour.ExecutionState, Is.EqualTo(
				MoteExecutionState.Yielded));
			Assert.That(
				manager.GetSharedVariables<float>()["enables"].IntegerValue,
				Is.EqualTo(2));

			InvokeLifecycle(manager, "Update");
			Assert.That(
				manager.GetSharedVariables<float>()["resumed"].IntegerValue,
				Is.EqualTo(1));
		}

		[Test]
		public void Behaviour_UsesIndependentContextWithSharedDictionaries()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
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
			AwakeAndEnableManager(manager);

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
			AwakeAndEnableManager(manager);
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
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Mote Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Start=()=>{shared.order=shared.order*10+3};"));
			InvokeLifecycle(behaviour, "OnEnable");

			InvokeLifecycle(manager, "Start");
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Enabled));

			InvokeLifecycle(manager, "Update");
			Assert.That(manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(23));
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Started));
		}

		[Test]
		public void BehaviourAddedAfterStart_RunsAwakeAndStartImmediately()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			InvokeLifecycle(manager, "Start");

			_behaviourObject = new GameObject("Late Behaviour");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{shared.order=1};"
				+ "Start=()=>{shared.order=shared.order*10+2};"));
			InvokeLifecycle(behaviour, "OnEnable");

			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Started));
			Assert.That(manager.GetSharedVariables<float>()["order"].IntegerValue,
				Is.EqualTo(12));
		}

		[Test]
		public void AwakeYield_IsRejectedAndFaultsHost()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Invalid Awake");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Awake=()=>{yield(1)};"));

			Assert.Throws<TargetInvocationException>(
				() => InvokeLifecycle(behaviour, "OnEnable"));
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Faulted));
		}

		[Test]
		public void EnableYield_IsRejectedAndFaultsHost()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Invalid Enable");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Enable=()=>{yield(1)};"));

			Assert.Throws<TargetInvocationException>(
				() => InvokeLifecycle(behaviour, "OnEnable"));
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Faulted));
		}

		[Test]
		public void DisableYield_IsRejectedAndDestroyStillReleasesHost()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Invalid Disable");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Disable=()=>{yield(1)};"));
			InvokeLifecycle(behaviour, "OnEnable");

			Assert.Throws<TargetInvocationException>(
				() => InvokeLifecycle(behaviour, "OnDisable"));
			Assert.That(behaviour.LifecycleState, Is.EqualTo(
				MoteScriptLifecycleState.Faulted));

			InvokeLifecycle(behaviour, "OnDestroy");
			Assert.That(manager.RegisteredBehaviourCount, Is.Zero);
			Assert.That(behaviour.Manager, Is.Null);
		}

		[Test]
		public void DestroyYield_IsRejectedAfterHostIsReleased()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Invalid Destroy");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Destroy=()=>{yield(1)};"));
			InvokeLifecycle(behaviour, "OnEnable");

			Assert.Throws<TargetInvocationException>(
				() => InvokeLifecycle(behaviour, "OnDestroy"));
			Assert.That(manager.RegisteredBehaviourCount, Is.Zero);
			Assert.That(behaviour.Manager, Is.Null);
		}

		[Test]
		public void UpdateYield_ResumesWithoutOverlappingInvocations()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
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
			AwakeAndEnableManager(manager);
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

		[Test]
		public void PausedUpdate_IsNotResumedByUnityScheduler()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Paused Target");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Update=()=>{\n"
				+ "yield(1);\n"
				+ "shared.after=1;\n"
				+ "};"));
			InvokeLifecycle(behaviour, "OnEnable");
			InvokeLifecycle(manager, "Start");
			InvokeLifecycle(manager, "Update");

			IMoteDebugTarget target = FindTarget(behaviour);
			target.Execution.SetBreakpoint(3);
			InvokeLifecycle(manager, "Update");
			Assert.That(target.Execution.State, Is.EqualTo(
				MoteExecutionState.Paused));

			InvokeLifecycle(manager, "Update");
			Assert.That(target.Execution.State, Is.EqualTo(
				MoteExecutionState.Paused));
			Assert.That(
				manager.GetSharedVariables<float>().ContainsKey("after"),
				Is.False);

			target.Execution.Continue();
			Assert.That(target.Execution.State, Is.EqualTo(
				MoteExecutionState.Completed));
			Assert.That(manager.GetSharedVariables<float>()["after"].IntegerValue,
				Is.EqualTo(1));
		}

		[Test]
		public void FaultedUpdate_PreservesLocalVariablesAndCallStack()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Faulted Target");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset(
				"Update=(;local temporary)=>{\n"
				+ "temporary=42;\n"
				+ "missing+1;\n"
				+ "};"));
			InvokeLifecycle(behaviour, "OnEnable");
			InvokeLifecycle(manager, "Start");

			int faultNotifications = 0;
			System.Action<IMoteDebugTarget> onFault = _ => ++faultNotifications;
			MoteDebugTargetRegistry.TargetFaulted += onFault;
			try
			{
				Assert.Throws<TargetInvocationException>(
					() => InvokeLifecycle(manager, "Update"));
			}
			finally
			{
				MoteDebugTargetRegistry.TargetFaulted -= onFault;
			}

			IMoteDebugTarget target = FindTarget(behaviour);
			Assert.That(target.Execution.State, Is.EqualTo(
				MoteExecutionState.Faulted));
			MoteDebugFaultSnapshot snapshot = target.Execution.FaultSnapshot;
			Assert.That(snapshot, Is.Not.Null);
			Assert.That(snapshot.Exception,
				Is.TypeOf<System.InvalidOperationException>());
			Assert.That(snapshot.Location?.StartLine, Is.EqualTo(3));
			Assert.That(FindVariable(snapshot.Variables, "temporary").Value,
				Is.EqualTo("42"));
			Assert.That(snapshot.CallStack.Count, Is.EqualTo(1));
			Assert.That(snapshot.CallStack[0].FunctionName, Is.EqualTo("Update"));
			Assert.That(faultNotifications, Is.EqualTo(1));
		}

		[Test]
		public void ManagerDestroy_UnregistersAllDebugTargets()
		{
			_managerObject = new GameObject("Mote Manager");
			MoteScriptManager manager =
				_managerObject.AddComponent<MoteScriptManager>();
			SetPrivateField(manager, "_script", new TextAsset("Start=()=>{0};"));
			AwakeAndEnableManager(manager);
			_behaviourObject = new GameObject("Child Target");
			MoteScriptBehaviour behaviour =
				_behaviourObject.AddComponent<MoteScriptBehaviour>();
			SetPrivateField(behaviour, "_script", new TextAsset("Update=()=>{0};"));
			InvokeLifecycle(behaviour, "OnEnable");

			Assert.That(FindTarget(manager), Is.Not.Null);
			Assert.That(FindTarget(behaviour), Is.Not.Null);
			InvokeLifecycle(manager, "OnDestroy");

			Assert.That(FindTarget(manager), Is.Null);
			Assert.That(FindTarget(behaviour), Is.Null);
		}
#endif

#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
		private static IMoteDebugTarget FindTarget(UnityEngine.Object owner)
		{
			foreach (IMoteDebugTarget target in MoteDebugTargetRegistry.Targets)
			{
				if (target.Owner == owner) return target;
			}
			return null;
		}

		private static MoteDebugVariable FindVariable(
			System.Collections.Generic.IReadOnlyList<MoteDebugVariable> variables,
			string name)
		{
			foreach (MoteDebugVariable variable in variables)
			{
				if (variable.Name == name) return variable;
			}
			throw new AssertionException($"Variable '{name}' was not captured.");
		}
#endif

		private static void InvokeLifecycle(object target, string methodName)
		{
			target.GetType()
				.GetMethod(methodName, PrivateInstance)
				.Invoke(target, null);
		}

		private static void AwakeAndEnableManager(MoteScriptManager manager)
		{
			InvokeLifecycle(manager, "Awake");
			InvokeLifecycle(manager, "OnEnable");
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
