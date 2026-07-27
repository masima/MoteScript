using System.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using MoteScript;

namespace MoteScript.Tests
{
	using MoteValue = MoteValue<float>;

	public partial class MoteScriptTests
	{
		/// <summary>
		/// 制御構文テスト
		/// </summary>
		/// <param name="index"></param>
		[Test]
		public void TestFlowControl_if(
			[Values(0, 1, 2)] float a
			)
		{
			// 変数保存場所生成
			var context = new Context();
			context.Set(nameof(a), a);
			var patterns = new (string sentence, float result)[]
			{
				("b=1;if(a==1){b=2};b", (a==1) ? 2:1),
				("b=1;if(a!=1){b=2};b", (a!=1) ? 2:1),
				("b=1;if(a==1){b=(b=2)*2};b", (a==1) ? 4:1),
				("b=1;if(a==1){b=2}else{b=3};b", (a==1) ? 2:3),
				("b=1;if(a!=1){b=2}else{b=3};b", (a!=1) ? 2:3),

				// 複数行確認(if,else)
				(@"
					b = 1;
					if (a != 1)
					{
						b = 2
					}
					else
					{
						b = 3
					};
					b", (a!=1) ? 2:3),

				// 明示的な文分割の省略確認(if,else)
				(@"
					b = 1;
					if (a != 1)
					{
						b = 2
					}
					else
					{
						b = 3
					}
					b", (a!=1) ? 2:3),

				// 複数行確認(if,else if)
				(@"
					b = 1;
					if (a == 1)
					{
						b = 2
					}
					else if (a == 2)
					{
						b = 3
					}
					b", (a==1) ? 2:(a==2) ? 3:1),

				// 複数行確認(if,else if,else)
				(@"
					b = 1;
					if (a == 1)
					{
						b = 2
					}
					else if (a == 2)
					{
						b = 3
					}
					else
					{
						b = 4
					}
					b", (a==1) ? 2:(a==2) ? 3:4),

				// 空文対応
				(@"
					b = 1;
					if (a == 1)
					{
						b = 2;
					}
					else if (a == 2)
					{
						b = 3;
					}
					b", (a==1) ? 2:(a==2) ? 3:1),

			};
			TestPatterns(patterns, context);
		}

		[Test]
		public void TestFlowControl_while(
			[Values(0, 1, 2)] float a
			)
		{
			var context = new Context();
			context.Set(nameof(a), a);
			var patterns = new (string sentence, float result)[]
			{
				// whileテスト
				// ("i=0;sum=0;while(i<a){sum=sum+i;i=i+1};sum", Enumerable.Range(0,(int)a).Sum()),

				// breakテスト
				(@"
					i=0;
					sum=0;
					while(1)
					{
						if (i>=a)
						{
							break;
						}
						sum=sum+i;
						i=i+1
					}
					sum", Enumerable.Range(0,(int)a).Sum()),

				// continueテスト
				(@"
					i=0;
					sum=0;
					while(1)
					{
						if (i<a)
						{
							sum=sum+i;
							i=i+1;
							continue;
						}
						break;
					}
					sum", Enumerable.Range(0,(int)a).Sum()),
			};
			TestPatterns(patterns, context);
		}

		[Test]
		public void TestFlowControl_while_while(
			[Values(0, 1, 2)] float a
			, [Values(0, 1, 2)] float b
			)
		{
			float i = 0;
			float counter = 0;
			while (true)
			{
				if (a <= i)
				{
					break;
				}
				float j = 0;						
				while (true)
				{
					if (b <= j)
					{
						break;
					}
					counter = counter + 1;
					j = j + 1;
				}
				i = i + 1;
			}

			var context = new Context();
			context.Set(nameof(a), a);
			context.Set(nameof(b), b);
			var patterns = new (string sentence, float result)[]
			{
				// 多重ループテスト
				(@"
					i = 0;
					counter = 0;
					while (1)
					{
						if (a <= i)
						{
							break;
						}
						j = 0;						
						while (1)
						{
							if (b <= j)
							{
								break;
							}
							counter = counter + 1;
							j = j + 1;
						}
						i = i + 1;
					}
					counter"
					, counter),
			};
			TestPatterns(patterns, context);
		}

		/// <summary>
		/// returnテスト
		/// </summary>
		/// <param name="a"></param>
		[Test]
		public void TestFlowControl_return(
			[Values(0, 1, 2)] float a
			)
		{
			var context = new Context();
			context.Set(nameof(a), a);
			var patterns = new (string sentence, float result)[]
			{
				 ("return a", a),
				 ("return a;", a),
				 ("{return a}", a),
				 ("if(a<1){return -1};a", (a<1) ? -1:a),
				 ("if(a<1){return-1};a", (a<1) ? -1:a),
				 // whie内のreturn
				 (@"
				 	i = 0;
				 	while (1)
				 	{
				 		if (a <= i)
				 		{
				 			return i;
				 		}
				 		i = i + 1;
				 	}
				 ", a),
				 // 関数内のreturn
				 (@"
				 	min = (x,y) =>
				 	{
				 		if (x <= y)
				 		{
				 			return x;
				 		}
				 		return y;
				 	};
				 	min(a,1)
				 ", (a <= 1) ? a : 1),
				 // 終端のreturn
				(@"
					mul2=(x)=>{return x*2};
					return mul2(a);
				", a*2),
			};
			TestPatterns(patterns, context);
		}
	}
}
