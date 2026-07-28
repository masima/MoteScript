namespace MoteScript
{
	internal readonly struct FunctionParameterDefinition
	{
		public FunctionParameterDefinition(
			string name,
			bool isReference)
		{
			Name = name;
			IsReference = isReference;
		}

		public string Name { get; }
		public bool IsReference { get; }
	}
}
