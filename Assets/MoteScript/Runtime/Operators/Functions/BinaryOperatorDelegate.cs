using System;
using System.Collections.Generic;
using System.Linq;

namespace MoteScript
{
	public class BinaryOperatorDelegate<T> 
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
 		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.Delegate;
		public override string OperatorCode => "=>";


		public override MoteValue<T> FinalizeOperator(Stack<MoteValue<T>> rpnStack)
		{
			MoteValue<T> statement = rpnStack.Pop();
			MoteValue<T> paramValue = rpnStack.Pop();
			List<string> parameterNames = new();
			if (paramValue.TryGetOperator(out BinaryOperatorArraySeparater<T> arraySeparater))
			{
				parameterNames.AddRange(arraySeparater.Values.Select(_ => _.StringValue));
			}
			else if (paramValue.ValueType.IsValid())
			{
				parameterNames.Add(paramValue.StringValue);
			}
			return CreateFunction(parameterNames, statement);
		}

		internal static MoteValue<T> CreateFunction(
			IEnumerable<string> parameterNames,
			MoteValue<T> statement)
		{
			return CreateFunction(
				parameterNames,
				Array.Empty<string>(),
				statement);
		}

		internal static MoteValue<T> CreateFunction(
			IEnumerable<string> parameterNames,
			IEnumerable<string> localNames,
			MoteValue<T> statement)
		{
			return CreateFunction(
				parameterNames.Select(
					name => new FunctionParameterDefinition(
						name,
						isReference: false)),
				localNames,
				statement);
		}

		internal static MoteValue<T> CreateFunction(
			IEnumerable<FunctionParameterDefinition> parameters,
			IEnumerable<string> localNames,
			MoteValue<T> statement)
		{
			DelegateInfo delegateInfo = new()
			{
				Parameters = CreateVariables(parameters),
				Locals = CreateVariables(
					localNames.Select(
						name => new FunctionParameterDefinition(
							name,
							isReference: false))),
				Statement = statement,
			};
			return new MoteValue<T>(delegateInfo.Func);
		}

		private static ScopedVariable[] CreateVariables(
			IEnumerable<FunctionParameterDefinition> parameters)
		{
			return parameters
				.Select(parameter => new ScopedVariable
				{
					Name = parameter.Name,
					IsReference = parameter.IsReference,
					SavedValues = new Stack<SavedValue>(4),
				})
				.ToArray();
		}

		private struct ScopedVariable
		{
			public string Name;
			public bool IsReference;
			public Stack<SavedValue> SavedValues;
		}
		private struct SavedValue
		{
			public MoteValue<T> Value;
			public bool HadValue;
		}
		private class DelegateInfo
		{
			public ScopedVariable[] Parameters;
			public ScopedVariable[] Locals;

			public MoteValue<T> Statement;

			public MoteValue<T> Func(IContext<T> context, List<MoteValue<T>> parameters)
			{
				ValidateParameters(parameters);
				SetupValues(context, parameters);
				bool suspended = false;
				try
				{
					MoteValue<T> result = Statement.EvaluateInner(context);
					if (result.TryGetExecution(out MoteExecution<T> execution))
					{
						suspended = true;
						execution.AddContinuation(new ParameterRestoreContinuation(this));
					}
					return result;
				}
				finally
				{
					if (!suspended)
					{
						RestoreValues(context);
					}
				}
			}

			private void ValidateParameters(
				List<MoteValue<T>> parameters)
			{
				for (int i = 0; i < Parameters.Length; ++i)
				{
					ScopedVariable parameter = Parameters[i];
					bool hasArgument = i < parameters.Count;
					bool isReference = hasArgument
						&& parameters[i].ValueType == EValueType.Reference;
					if (parameter.IsReference && !isReference)
					{
						throw new InvalidOperationException(
							$"ref parameter '{parameter.Name}' requires a ref argument.");
					}
					if (!parameter.IsReference && isReference)
					{
						throw new InvalidOperationException(
							$"value parameter '{parameter.Name}' cannot receive a ref argument.");
					}
				}
			}

			private sealed class ParameterRestoreContinuation : IExecutionContinuation<T>
			{
				private readonly DelegateInfo _owner;
				private bool _restored;

				public ParameterRestoreContinuation(DelegateInfo owner)
				{
					_owner = owner;
				}

				public MoteValue<T> Resume(IContext<T> context, MoteValue<T> result, int state)
				{
					Restore(context);
					return result;
				}

				public void Cancel(IContext<T> context)
				{
					Restore(context);
				}

				private void Restore(IContext<T> context)
				{
					if (_restored) return;
					_restored = true;
					_owner.RestoreValues(context);
				}
			}

			private void SetupValues(IContext<T> context, List<MoteValue<T>> parameters)
			{
				SaveValues(context, Parameters);
				SaveValues(context, Locals);

				for (int i = 0; i < Locals.Length; ++i)
				{
					context.Remove(Locals[i].Name);
				}
				for	(int i = 0; i < Parameters.Length; i++)
				{
					if (i < parameters.Count)
					{
						context[Parameters[i].Name] = parameters[i];
					}
				}
			}

			private static void SaveValues(
				IContext<T> context,
				ScopedVariable[] variables)
			{
				for (int i = 0; i < variables.Length; ++i)
				{
					string name = variables[i].Name;
					bool hadValue = context.TryGetValue(name, out MoteValue<T> value);
					variables[i].SavedValues.Push(new SavedValue
					{
						HadValue = hadValue,
						Value = value,
					});
				}
			}

			private void RestoreValues(IContext<T> context)
			{
				RestoreValues(context, Locals);
				RestoreValues(context, Parameters);
			}

			private static void RestoreValues(
				IContext<T> context,
				ScopedVariable[] variables)
			{
				for	(int i = 0; i < variables.Length; i++)
				{
					string name = variables[i].Name;
					SavedValue saved = variables[i].SavedValues.Pop();
					if (saved.HadValue)
					{
						context[name] = saved.Value;
					}
					else
					{
						context.Remove(name);
					}
				}
			}
		}


		public override MoteValue<T> Evaluate(IContext<T> context)
		{
			throw new InvalidOperationException();
		}

	}


}
