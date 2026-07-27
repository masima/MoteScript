using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoteScript
{
	internal static class OperatorRegistry<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private static readonly List<OperatorInfo> s_unaryOperators = new();
		private static readonly List<OperatorInfo> s_flowControlOperators = new();
		private static readonly List<OperatorInfo> s_binaryOperators = new();
		private static readonly List<OperatorInfo> s_bracketOperators = new();

		public static IReadOnlyList<OperatorInfo> UnaryOperators => s_unaryOperators;
		public static IReadOnlyList<OperatorInfo> FlowControlOperators => s_flowControlOperators;
		public static IReadOnlyList<OperatorInfo> BinaryOperators => s_binaryOperators;
		public static IReadOnlyList<OperatorInfo> BracketOperators => s_bracketOperators;

		public static void Setup(Assembly runtimeAssembly, Assembly extensionAssembly)
		{
			s_unaryOperators.Clear();
			s_flowControlOperators.Clear();
			s_binaryOperators.Clear();
			s_bracketOperators.Clear();

			Register(runtimeAssembly);
			if (extensionAssembly is not null && runtimeAssembly != extensionAssembly)
			{
				Register(extensionAssembly);
			}
		}

		private static void Register(Assembly assembly)
		{
			Type[] operatorTypes = assembly.GetTypes()
				.Where(type =>
					type.BaseType != null
					&& !type.IsAbstract
					&& type.BaseType.IsGenericType)
				.ToArray();

			foreach (Type type in operatorTypes)
			{
				List<OperatorInfo> operators = SelectOperators(
					type.BaseType.GetGenericTypeDefinition());
				if (operators is null)
				{
					continue;
				}

				Type genericType = type.MakeGenericType(typeof(T));
				if (genericType.GetProperty(nameof(IOperator<T>.OperatorCode)) is null)
				{
					continue;
				}

				var instance = Activator.CreateInstance(genericType) as IOperator;
				string code = instance.OperatorCode;
				if (!string.IsNullOrEmpty(code))
				{
					operators.Add(new OperatorInfo
					{
						Type = genericType,
						OperatorCode = code,
						Priority = instance.Priority,
					});
				}
			}

			SortByLongestCode(s_flowControlOperators);
			SortByLongestCode(s_binaryOperators);
			SortByLongestCode(s_unaryOperators);
		}

		private static List<OperatorInfo> SelectOperators(Type baseType)
		{
			if (baseType == typeof(UnaryOperatorOpenBracket<>)
				|| baseType == typeof(UnaryOperatorCloseBracket<>))
			{
				return s_bracketOperators;
			}
			if (baseType == typeof(FlowControlOperator<>))
			{
				return s_flowControlOperators;
			}
			if (baseType == typeof(BinaryOperator<>))
			{
				return s_binaryOperators;
			}
			if (baseType == typeof(UnaryOperator<>))
			{
				return s_unaryOperators;
			}
			return null;
		}

		private static void SortByLongestCode(List<OperatorInfo> operators)
		{
			operators.Sort(
				(left, right) => right.OperatorCode.Length - left.OperatorCode.Length);
		}
	}
}
