using System;

namespace MoteScript
{
	public abstract class FlowControlOperator<T>
		: IOperator<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public abstract OperatorType OperatorType { get; }
		public int Priority => OperatorType.GetPriority();
		public abstract string OperatorCode { get; }

		public MoteValue<T> Judge;
		public MoteValue<T> Statement;

		public bool IsFinalized
		{
			get
			{
				return Judge.ValueType.IsValid() && Statement.ValueType.IsValid();
			}
		}

		public abstract MoteValue<T> Evaluate(IContext<T> context);
	}

}
