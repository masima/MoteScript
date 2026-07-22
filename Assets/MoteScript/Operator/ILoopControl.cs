using System.Collections;
using System.Collections.Generic;


namespace MoteScript
{
	public interface ILoopControl : IOperator
	{
		public FlowControlFlag FlowControlFlag { get; }
	}

}
