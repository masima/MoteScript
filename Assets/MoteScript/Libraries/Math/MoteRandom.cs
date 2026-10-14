using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace MoteScript
{
	/// <summary>
	/// A stateful random-number generator exposed as a read-only script Context.
	/// </summary>
	public sealed class MoteRandom<T> : IClonableContext<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>,
		IComparable<T>
	{
		private const ulong DefaultSequence = 1442695040888963407UL;
		private const ulong SeedIncrement = 11400714819323198485UL;
		private static readonly ulong s_seedSalt = CreateSeedSalt();
		private static long s_seedCounter;

		private ulong _state;
		private readonly ulong _increment;
		private readonly MoteValue<T> _rangeIntFunction;
		private readonly MoteValue<T> _rangeRealFunction;

		public int Count => 2;

		public MoteRandom() : this(CreateAutomaticSeed())
		{
		}

		public MoteRandom(long seed)
		{
			_increment = (DefaultSequence << 1) | 1UL;
			_state = 0UL;
			NextUInt32();
			_state += unchecked((ulong)seed);
			NextUInt32();
			_rangeIntFunction = new MoteValue<T>(RangeInt);
			_rangeRealFunction = new MoteValue<T>(RangeReal);
		}

		private MoteRandom(ulong state, ulong increment)
		{
			_state = state;
			_increment = increment;
			_rangeIntFunction = new MoteValue<T>(RangeInt);
			_rangeRealFunction = new MoteValue<T>(RangeReal);
		}

		public MoteValue<T> this[string key]
		{
			get
			{
				if (TryGetValue(key, out MoteValue<T> value))
				{
					return value;
				}
				throw new KeyNotFoundException(key);
			}
			set => throw ReadOnlyException();
		}

		public bool TryGetValue(string key, out MoteValue<T> value)
		{
			switch (key)
			{
				case "rangeInt":
					value = _rangeIntFunction;
					return true;
				case "rangeReal":
					value = _rangeRealFunction;
					return true;
				default:
					value = default;
					return false;
			}
		}

		public bool Remove(string key)
		{
			throw ReadOnlyException();
		}

		public void Clear()
		{
			throw ReadOnlyException();
		}

		public IContext<T> Instantiate()
		{
			return new Context<T>();
		}

		public IContext<T> Clone()
		{
			return new MoteRandom<T>(_state, _increment);
		}

		private MoteValue<T> RangeInt(
			IContext<T> context,
			IContext<T> receiver,
			List<MoteValue<T>> values)
		{
			RequireCount(values, 2, "random.rangeInt");
			int minimum = GetInteger(values[0], context, "random.rangeInt");
			int maximum = GetInteger(values[1], context, "random.rangeInt");
			if (minimum >= maximum)
			{
				throw new ArgumentException(
					"random.rangeInt minimum must be less than maximum.");
			}

			ulong width = (ulong)((long)maximum - minimum);
			ulong sample = NextUInt64Bounded(width);
			long result = minimum + (long)sample;
			return new MoteValue<T>(MoteValue<T>.Calculator.Convert((int)result));
		}

		private MoteValue<T> RangeReal(
			IContext<T> context,
			IContext<T> receiver,
			List<MoteValue<T>> values)
		{
			if (typeof(T) == typeof(int))
			{
				throw new NotImplementedException(
					"random.rangeReal is not implemented for CalculatorInteger.");
			}
			RequireCount(values, 2, "random.rangeReal");
			T minimum = GetNumber(values[0], context, "random.rangeReal");
			T maximum = GetNumber(values[1], context, "random.rangeReal");
			if (MoteValue<T>.Calculator.GreaterThan(minimum, maximum))
			{
				throw new ArgumentException(
					"random.rangeReal minimum cannot exceed maximum.");
			}

			if (typeof(T) == typeof(decimal))
			{
				decimal min = (decimal)(object)minimum;
				decimal max = (decimal)(object)maximum;
				decimal unit = NextUInt32() / (decimal)uint.MaxValue;
				return new MoteValue<T>((T)(object)(min + ((max - min) * unit)));
			}

			float minFloat = (float)(object)minimum;
			float maxFloat = (float)(object)maximum;
			float unitFloat = NextUInt32() / (float)uint.MaxValue;
			return new MoteValue<T>((T)(object)
				(minFloat + ((maxFloat - minFloat) * unitFloat)));
		}

		private uint NextUInt32()
		{
			ulong oldState = _state;
			_state = unchecked((oldState * 6364136223846793005UL) + _increment);
			uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
			int rotation = (int)(oldState >> 59);
			return (xorShifted >> rotation)
				| (xorShifted << ((-rotation) & 31));
		}

		private static long CreateAutomaticSeed()
		{
			ulong counter = unchecked((ulong)Interlocked.Increment(ref s_seedCounter));
			return unchecked((long)MixSeed(s_seedSalt + (counter * SeedIncrement)));
		}

		private static ulong CreateSeedSalt()
		{
			ulong ticks = unchecked((ulong)DateTime.UtcNow.Ticks);
			ulong timestamp = unchecked((ulong)Stopwatch.GetTimestamp());
			return MixSeed(ticks ^ (timestamp << 1));
		}

		private static ulong MixSeed(ulong value)
		{
			value ^= value >> 30;
			value *= 13787848793156543929UL;
			value ^= value >> 27;
			value *= 10723151780598845931UL;
			return value ^ (value >> 31);
		}

		private ulong NextUInt64Bounded(ulong bound)
		{
			ulong threshold = unchecked(0UL - bound) % bound;
			while (true)
			{
				ulong value = ((ulong)NextUInt32() << 32) | NextUInt32();
				if (value >= threshold)
				{
					return value % bound;
				}
			}
		}

		private static int GetInteger(
			MoteValue<T> parameter,
			IContext<T> context,
			string function)
		{
			T value = GetNumber(parameter, context, function);
			decimal numeric = value.ToDecimal(null);
			if (numeric != decimal.Truncate(numeric))
			{
				throw new ArgumentException($"{function} bounds must be integers.");
			}
			try
			{
				return decimal.ToInt32(numeric);
			}
			catch (OverflowException)
			{
				throw new ArgumentOutOfRangeException(
					"values",
					$"{function} bounds must fit in a 32-bit integer.");
			}
		}

		private static T GetNumber(
			MoteValue<T> parameter,
			IContext<T> context,
			string function)
		{
			return parameter.EvaluateInner(context).GetNumericValue(function);
		}

		private static void RequireCount(
			List<MoteValue<T>> values,
			int expected,
			string function)
		{
			if (values.Count != expected)
			{
				throw new ArgumentException(
					$"{function} expects {expected} arguments, but received {values.Count}.");
			}
		}

		private static InvalidOperationException ReadOnlyException()
		{
			return new InvalidOperationException("MoteRandom members are read-only.");
		}
	}
}
