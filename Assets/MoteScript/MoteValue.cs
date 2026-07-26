using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoteScript
{
	using ValueTypeType = EValueType;

	public struct MoteValue<T>
		//: IMoteValue
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		private ValueTypeType _valueType;
		public EValueType ValueType
		{
			get => (EValueType)_valueType;
		}

		private T _value;
		public T Value => _value;

		public float FloatValue
		{
			get => _value.ToSingle(null);
		}
		public int IntegerValue
		{
			get => _value.ToInt32(null);
		}
		public string StringValue
		{
			get => ValueType == EValueType.String
				? _object as string
				: _object?.ToString() ?? _value.ToString();
		}

		public bool ToBool()
		{
			ThrowIfNotNumeric("boolean conversion");
			return Calculator.ToBool(_value);
		}

		internal T GetNumericValue(string operation)
		{
			ThrowIfNotNumeric(operation);
			return _value;
		}

		internal void ThrowIfNotNumeric(string operation)
		{
			if (ValueType != EValueType.Const)
			{
				throw new InvalidOperationException(
					$"{operation} requires a numeric value, but received {GetDiagnosticTypeName(ValueType)}.");
			}
		}

		internal static void ValidateAssignment(
			MoteValue<T> current,
			MoteValue<T> value,
			string target)
		{
			if (current.ValueType.IsValid() && current.ValueType != value.ValueType)
			{
				throw new InvalidOperationException(
					$"Cannot assign {GetDiagnosticTypeName(value.ValueType)}"
					+ $" to {GetDiagnosticTypeName(current.ValueType)} target '{target}'.");
			}
		}

		private static string GetDiagnosticTypeName(EValueType valueType)
		{
			return valueType == EValueType.Const ? "Number" : valueType.ToString();
		}

		public static MoteValue<T> Default => new MoteValue<T>((T)default);

		public override string ToString()
		{
			return $"{{_valueType:{(EValueType)_valueType},_value:{_value.ToString()},_object:{_object?.ToString() ?? "null"}}}";
		}

		/// <summary>
		/// binary operator等。
		/// </summary>
		private object _object;
		public bool TryGetOperator<TOperator>(out TOperator operatorInstance)
			where TOperator : IOperator
		{
			if (_object is TOperator op)
			{
				operatorInstance = op;
				return true;
			}
			operatorInstance = default;
			return false;
		}
		public TOperator GetOperator<TOperator>()
			where TOperator : class, IOperator
		{
			return _object as TOperator;
		}
		public IOperator GetOperator()
		{
			return _object as IOperator;
		}
		public TObject GetObject<TObject>()
			where TObject : class
		{
			return _object as TObject;
		}
		public object GetObject()
		{
			return _object;
		}
		public IContext<T> GetDictionary()
		{
			return _object as IContext<T>;
		}

		public MoteList<T> GetArray()
		{
			return _object as MoteList<T>;
		}

		public bool TryGetYield(out MoteYield<T> yield)
		{
			yield = _object as MoteYield<T>;
			return yield is not null;
		}


		static ICalculator s_calculator;
		public static ICalculator<T> Calculator
		{
			get => s_calculator as ICalculator<T>;
			set => s_calculator = value;
		}
		public static T Convert(bool value)
		{
			return Calculator.Convert(value);
		}

		public delegate MoteValue<T> Function(IContext<T> context, List<MoteValue<T>> values);


		/// <summary>
		/// 使用するCalculatorをセットする
		/// </summary>
		/// <param name="calculator"></param>
		public static void Setup(ICalculator<T> calculator)
		{
			s_calculator = calculator;
		}
		/// <summary>
		/// Assembly内の適応するCalculatorをセットする
		/// </summary>
		/// <param name="assembly"></param>
		/// <returns></returns>
		public static bool Setup(Assembly assembly)
		{
			foreach (Type type in assembly.GetTypes())
			{
				foreach (Type interfaseType in type.GetInterfaces())
				{
					if (!interfaseType.IsGenericType)
					{
						continue;
					}
					if (interfaseType.GetGenericTypeDefinition() == typeof(ICalculator<>))
					{
						Type genericArgumentType = interfaseType.GetGenericArguments()[0];
						if (genericArgumentType == typeof(T))
						{
							s_calculator = Activator.CreateInstance(type) as ICalculator<T>;
							return true;
						}

					}
				}
			}

			return false;
		}

		public static MoteValue<T> GetConstValue(string value)
		{
			return new MoteValue<T>(Calculator.Convert(value));
		}

		public static MoteValue<T> GetConstValue(ReadOnlySpan<char> value)
		{
			return new MoteValue<T>(Calculator.Convert(value));
		}




		public MoteValue(T value)
		{
			_valueType = (ValueTypeType)EValueType.Const;
			_value = value;
			_object = null;
		}
		public MoteValue(bool value)
		{
			_valueType = (ValueTypeType)EValueType.Const;
			_value = Calculator.Convert(value);
			_object = null;
		}
		public MoteValue(int value)
		{
			_valueType = (ValueTypeType)EValueType.Const;
			_value = Calculator.Convert(value);
			_object = null;
		}
		public MoteValue(float value)
		{
			_valueType = (ValueTypeType)EValueType.Const;
			_value = Calculator.Convert(value);
			_object = null;
		}
		public MoteValue(IOperator operatorObject)
		{
			switch (operatorObject)
			{
				case FlowControlOperatorReturn<T> operatorReturn:
					_valueType = (ValueTypeType)EValueType.LoopControl;
					_value = operatorReturn.ReturnValue.Value;
					_object = operatorReturn;
					break;
				case ILoopControl:
					_valueType = (ValueTypeType)EValueType.LoopControl;
					_value = default;
					break;
				case BinaryOperator<T>:
					_valueType = (ValueTypeType)EValueType.BinaryOperator;
					_value = default;
					break;
				case UnrayOperator<T>:
					_valueType = (ValueTypeType)EValueType.UnrayOperator;
					_value = default;
					break;
				default:
					_valueType = (ValueTypeType)EValueType.Operator;
					_value = default;
					break;					
			}
			_object = operatorObject;
		}
		public MoteValue(MoteList<T> values)
		{
			_valueType = (ValueTypeType)EValueType.Array;
			_value = Calculator.Convert(values.Count);
			_object = values;
		}
		public MoteValue(IContext<T> values)
		{
			_valueType = (ValueTypeType)EValueType.Dictionary;
			_value = Calculator.Convert(values.Count);
			_object = values;
		}
		public MoteValue(Function function)
		{
			_valueType = (ValueTypeType)EValueType.Function;
			_value = default;
			_object = function;
		}

		public MoteValue(string value)
		{
			_valueType = (ValueTypeType)EValueType.String;
			_value = default;
			_object = value;
		}

		public MoteValue(MoteYield<T> yield)
		{
			_valueType = (ValueTypeType)EValueType.Yield;
			_value = default;
			_object = yield;
		}

		public MoteValue(EValueType valueType, object value)
		{
			_valueType = (ValueTypeType)valueType;
			_value = default;
			_object = value;
		}
		// public MoteValue(
		// 	ParentheresesDefine parentheresesDefine
		// 	, EParentheresesState parentheresesState
		// 	)
		// {
		// 	_valueType = parentheresesState == EParentheresesState.Open
		// 		? (byte)EValueType.ParentheresesOpen
		// 		: (byte)EValueType.ParentheresesClose;
		// 	_value = default;
		// 	_object = parentheresesDefine;
		// }

		private string AssignmentKey => _object as string;

		public MoteValue<T> Evaluate(IContext<T> context)
		{
			MoteValue<T> result = EvaluteInner(context);
			if (result.TryGetOperator(out FlowControlOperatorReturn<T> returnOperator))
			{
				return returnOperator.ReturnValue;
			}
			return result;
		}
		[Obsolete("Use Evaluate instead.")]
		public MoteValue<T> Evalute(IContext<T> context)
		{
			return Evaluate(context);
		}
		internal MoteValue<T> EvaluteInner(IContext<T> context)
		{
			switch ((EValueType)_valueType)
			{
			case EValueType.Unknown:
			case EValueType.Const:
			case EValueType.String:
			case EValueType.Array:
			case EValueType.Dictionary:
			case EValueType.Function:
			case EValueType.Yield:
				return this;
			case EValueType.Variable:
			{
				var key = AssignmentKey;
				if (context.TryGetValue(key, out MoteValue<T> value))
				{
					return value;
				}
				string availableVariables = context is IEnumerable<KeyValuePair<string, MoteValue<T>>> values
					? string.Join(", ", values.Select(pair => pair.Key).OrderBy(name => name))
					: string.Empty;
				string message = $"Undefined variable : {key}";
				if (!string.IsNullOrEmpty(availableVariables))
				{
					message += $". Available variables: {availableVariables}";
				}
				throw new InvalidOperationException(message);
			}
			default:
				switch (_object)
				{
					case IOperator<T> op:
						return op.Evaluate(context);
					default:
						throw new System.Exception($"not support type : {((EValueType)_valueType).ToString()}");
				}
			}
		}

		internal void AssignmentTo(IContext<T> context, MoteValue<T> value)
		{
			switch (ValueType)
			{
				case EValueType.Variable:
					if (context.TryGetValue(AssignmentKey, out MoteValue<T> current))
					{
						ValidateAssignment(current, value, AssignmentKey);
					}
					context[AssignmentKey] = value;
					break;
				default:
					if (TryGetOperator(out IAssignmentTo<T> accessor))
					{
						accessor.AssignmentTo(context, value);
						return;
					}
					throw new InvalidOperationException();
			}
		}

		internal MoteValue<T> ConvertToVariable()
		{
			_valueType = (ValueTypeType)EValueType.Variable;
			return this;
		}
	}

}
