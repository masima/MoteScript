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
			DelegateInfo delegateInfo = new()
			{
				Parameters = parameterNames
					.Select(name => new Parameter
					{
						Name = name,
						SavedValues = new Stack<SavedParameter>(4),
					})
					.ToArray(),
				Statement = statement,
			};
			return new MoteValue<T>(delegateInfo.Func);
		}

		private struct Parameter
		{
			public string Name;
			public Stack<SavedParameter> SavedValues;
		}
		private struct SavedParameter
		{
			public MoteValue<T> Value;
			public bool HadValue;
		}
		private class DelegateInfo
		{
			public Parameter[] Parameters;

			public MoteValue<T> Statement;

			public MoteValue<T> Func(IContext<T> context, List<MoteValue<T>> parameters)
			{
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
				for	(int i = 0; i < Parameters.Length; i++)
				{
					string name = Parameters[i].Name;
					bool hadValue = context.TryGetValue(name, out MoteValue<T> value);
					Parameters[i].SavedValues.Push(new SavedParameter
					{
						HadValue = hadValue,
						Value = value,
					});
					if (i < parameters.Count)
					{
						context[name] = parameters[i];
					}
				}
			}
			private void RestoreValues(IContext<T> context)
			{
				for	(int i = 0; i < Parameters.Length; i++)
				{
					string name = Parameters[i].Name;
					SavedParameter saved = Parameters[i].SavedValues.Pop();
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
