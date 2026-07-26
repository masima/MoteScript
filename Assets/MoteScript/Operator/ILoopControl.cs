using System.Collections;
using System.Collections.Generic;


namespace MoteScript
{
	public interface ILoopControl : IOperator, IExecutionControl
	{
		public FlowControlFlag FlowControlFlag { get; }
	}

}
