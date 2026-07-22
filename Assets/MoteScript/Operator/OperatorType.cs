namespace MoteScript
{
	public enum OperatorType
	{
		Bracket = PriorityUnit * 0,
		FlowControl =  PriorityUnit * 0 + 1,

		Function =  PriorityUnit * 1,
		ArrayAccessor,
		DictionaryAccessor,
		New,

		Mul =  PriorityUnit * 2,
		Div,
		Mod,

		Plus =  PriorityUnit * 3,
		Minus,

		GreaterThan =  PriorityUnit * 4,
		GreaterThanOrEqualTo,
		LessThan,
		LessThanOrEqualTo,

		Equal =  PriorityUnit * 5,
		NotEqual,
		
		LogicalConjunction = PriorityUnit * 6,
		LogicalDisjunction = PriorityUnit * 7,


		Delegate = PriorityUnit * 9,
		Assignment = PriorityUnit * 10,
		KeyValuePair,

		ArraySeparator = PriorityUnit * 11,
		SentenceSeparator = PriorityUnit * 12,

		PriorityUnit = 0x100,
		PriorityShift = 8,
	}
	public static class OperatorTypeExtensions
	{
		public static int GetPriority(this OperatorType type)
		{
			return (int)type >> (int)OperatorType.PriorityShift;
			}
	}
}
