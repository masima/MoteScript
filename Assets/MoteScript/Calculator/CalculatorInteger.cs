using System;


namespace MoteScript
{
	public struct CalculatorInteger : ICalculator<int>
	{
		public float ToSingle(int value)
		{
			return value;
		}
		public bool ToBool(int value)
		{
			return 0 < value;
		}

		public int Convert(bool value)
		{
			return value ? 1 : 0;
		}
		public int Convert(int value)
		{
			return value;
		}
		public int Convert(float value)
		{
			return (int)value;
		}
		public int Convert(string value)
		{
			return int.Parse(value);
		}

		public int Convert(ReadOnlySpan<char> value)
		{
			return int.Parse(value);
		}

		public int Add(int left, int right)
		{
			return left + right;
		}
		public int Substruct(int left, int right)
		{
			return left - right;
		}
		public int Multiple(int left, int right)
		{
			return left * right;
		}
		public int Divide(int left, int right)
		{
			return left / right;
		}
		public int Mod(int left, int right)
		{
			return left % right;
		}

		public bool GreaterThan(int left, int right)
		{
			return left > right;
		}
		public bool GreaterThanOrEqualTo(int left, int right)
		{
			return left >= right;
		}
		public bool LessThan(int left, int right)
		{
			return left < right;
		}
		public bool LessThanOrEqualTo(int left, int right)
		{
			return left <= right;
		}
	}
}
