using System;
using System.Collections.Generic;

namespace MoteScript
{
	public class BinaryOperatorSentenceSeparater<T>
		: BinaryOperator<T>
		, IOperatorOnFinalized
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public const char OperatorCodeConst = ';';
		private static readonly string _operatorCode = string.Empty + OperatorCodeConst;
		public override OperatorType OperatorType => OperatorType.SentenceSeparator;
		public override string OperatorCode => _operatorCode;


		public override bool ConvertToRpn(
			Queue<MoteValue<T>> queue
			, List<MoteValue<T>> rpn
			, out int insertIndex
			)
		{
			insertIndex = MoteDecoder<T>.GetInsertPosition(rpn, Priority);
			// Get right value
			if (0 < queue.Count)
			{
				MoteValue<T> rightValue = queue.Dequeue();
				while (rightValue.TryGetOperator(out BinaryOperatorSentenceSeparater<T> _))
				{
					// 空文
					if (0 == queue.Count)
					{
						return true;
					}
					rightValue = queue.Dequeue();
				}
				rpn.Insert(insertIndex++, rightValue);
				rpn.Insert(insertIndex, new MoteValue<T>(this));
			}
			else
			{
				// 右辺値無し
			}

			return true;
		}


		private List<MoteValue<T>> _values;
		private MoteValue<T>[] _statements;


		public override MoteValue<T> Finailze(Stack<MoteValue<T>> rpnStack)
		{
			base.Finailze(rpnStack);
			if (!Left.TryGetOperator(out BinaryOperatorSentenceSeparater<T> accessor))
			{
				_values = new List<MoteValue<T>>();
				_values.Add(Left);
				_values.Add(Right);
				accessor = this;
			}
			else
			{
				accessor._values.Add(Right);
			}
			Left = MoteValue<T>.Default;
			Right = MoteValue<T>.Default;
			return new MoteValue<T>(accessor);
		}

		public bool IsOnFinishedRequired => _values is not null;
		public void OnFinalized()
		{
			_statements = _values.ToArray();
			_values = null;
		}


		public override MoteValue<T> Evalute(IContext<T> context)
		{
			MoteValue<T> lastValue = default;
			foreach	(var value in _statements)
			{
				lastValue = value.EvaluteInner(context);
				if (lastValue.ValueType.IsLoopControl())
				{
					return lastValue;
				}
			}
			return lastValue;
		}
	}


}

