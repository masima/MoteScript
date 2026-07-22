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


		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
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
				try
				{
					return Statement.EvaluteInner(context);
				}
				finally
				{
					RestoreValues(context);
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


		public override MoteValue<T> Evalute(IContext<T> context)
		{
			throw new InvalidOperationException();
		}

	}


}
