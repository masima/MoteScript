using System;
using System.Collections.Generic;

namespace MoteScript
{
	public class BinaryOperatorArraySeparater<T>
		: BinaryOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public override OperatorType OperatorType => OperatorType.ArraySeparator;
		public override string OperatorCode => ",";

		private List<MoteValue<T>> _values;
		public List<MoteValue<T>> Values => _values;
		private MoteList<T> _results;


		public static MoteValue<T> InstantiateSingleParameter(MoteValue<T> value)
		{
			return InstantiateParameters(value.ValueType.IsValid()
				? new[] { value }
				: Array.Empty<MoteValue<T>>());
		}

		internal static MoteValue<T> InstantiateParameters(IEnumerable<MoteValue<T>> values)
		{
			var arraySeparater = new BinaryOperatorArraySeparater<T>();
			arraySeparater._results = new MoteList<T>();
			arraySeparater._values = new List<MoteValue<T>>(values);
			arraySeparater._results.Capacity = arraySeparater._values.Count;
			arraySeparater.Left = MoteValue<T>.Default;
			arraySeparater.Right = MoteValue<T>.Default;

			return new MoteValue<T>(arraySeparater);
		}

		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			base.Finailze(rpnStack);
			if (!Left.TryGetOperator(out BinaryOperatorArraySeparater<T> separater))
			{
				_results = new MoteList<T>();
				_values = new List<MoteValue<T>>();
				_values.Add(Left);
				_values.Add(Right);
				separater = this;
			}
			else
			{
				separater._values.Add(Right);
			}
			separater.FinalizeInner();
			return new MoteValue<T>(separater);
		}
		private void FinalizeInner()
		{
			Left = MoteValue<T>.Default;
			Right = MoteValue<T>.Default;
			if (_results.Capacity < _values.Count)
			{
				_results.Capacity = _values.Count;
			}
		}


		public override MoteValue<T> Evalute(IContext<T> context)
		{
			// _results.Capacity = _values.Count;
			_results.Clear();
			foreach	(MoteValue<T> value in _values)
			{
				_results.Add(value.EvaluteInner(context));
			}
			return new MoteValue<T>(_results);
		}
	}


}
