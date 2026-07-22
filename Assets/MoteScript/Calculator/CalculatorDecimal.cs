using System;


namespace MoteScript
{
	public struct CalculatorDecimal : ICalculator<decimal>
	{
		public float ToSingle(decimal value)
		{
			return decimal.ToSingle(value);
		}
		public bool ToBool(decimal value)
		{
			return 0.0m < value;
		}

		public decimal Convert(bool value)
		{
			return value ? 1.0m : 0.0m;
		}
		public decimal Convert(int value)
		{
			return value;
		}
		public decimal Convert(float value)
		{
			return (decimal)value;
		}
		public decimal Convert(string value)
		{
			return decimal.Parse(value);
		}
		public decimal Convert(ReadOnlySpan<char> value)
		{
			return decimal.Parse(value);
		}

		public decimal Add(decimal left, decimal right)
		{
			return left + right;
		}
		public decimal Substruct(decimal left, decimal right)
		{
			return left - right;
		}
		public decimal Multiple(decimal left, decimal right)
		{
			return left * right;
		}
		public decimal Divide(decimal left, decimal right)
		{
			return left / right;
		}
		public decimal Mod(decimal left, decimal right)
		{
			return left % right;
		}

		public bool GreaterThan(decimal left, decimal right)
		{
			return left > right;
		}
		public bool GreaterThanOrEqualTo(decimal left, decimal right)
		{
			return left >= right;
		}
		public bool LessThan(decimal left, decimal right)
		{
			return left < right;
		}
		public bool LessThanOrEqualTo(decimal left, decimal right)
		{
			return left <= right;
		}
	}
}
