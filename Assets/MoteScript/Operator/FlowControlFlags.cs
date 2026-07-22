using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoteScript
{
	[System.Flags]
	public enum FlowControlFlag
	{
		Continue = 0,
		Break = 1,
		Return = 1 << 1,
	}

	public static class FlowControlFlagExtensions
	{
		public static bool IsBreak(this FlowControlFlag flag)
		{
			return 0 != (flag & FlowControlFlag.Break);
		}
		public static bool IsReturn(this FlowControlFlag flag)
		{
			return 0 != (flag & FlowControlFlag.Return);
		}
	}

}
