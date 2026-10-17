using System;
using System.Globalization;

namespace MoteScript
{
	public struct CalculatorDouble : ICalculator<double>
	{
		public float ToSingle(double value)
		{
			return (float)value;
		}

		public bool ToBool(double value)
		{
			return 0.0 < value;
		}

		public double Convert(bool value)
		{
			return value ? 1.0 : 0.0;
		}

		public double Convert(int value)
		{
			return value;
		}

		public double Convert(float value)
		{
			return value;
		}

		public double Convert(string value)
		{
			return double.Parse(
				value,
				NumberStyles.Float,
				CultureInfo.InvariantCulture);
		}

		public double Convert(ReadOnlySpan<char> value)
		{
			return double.Parse(
				value,
				NumberStyles.Float,
				CultureInfo.InvariantCulture);
		}

		public double Add(double left, double right)
		{
			return left + right;
		}

		public double Substruct(double left, double right)
		{
			return left - right;
		}

		public double Multiple(double left, double right)
		{
			return left * right;
		}

		public double Divide(double left, double right)
		{
			return left / right;
		}

		public double Mod(double left, double right)
		{
			return left % right;
		}

		public bool GreaterThan(double left, double right)
		{
			return left > right;
		}

		public bool GreaterThanOrEqualTo(double left, double right)
		{
			return left >= right;
		}

		public bool LessThan(double left, double right)
		{
			return left < right;
		}

		public bool LessThanOrEqualTo(double left, double right)
		{
			return left <= right;
		}
	}
}
