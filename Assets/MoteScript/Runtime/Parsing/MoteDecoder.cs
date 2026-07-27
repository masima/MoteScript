using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;

namespace MoteScript
{

	public class MoteDecoder<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>
		, IComparable<T>
	{
		public static void Setup(
			Assembly assembly = null
			, Type contextType = null
			)
		{
			Assembly moteDecoderAssembly = typeof(MoteDecoder<T>).Assembly;
			OperatorRegistry<T>.Setup(moteDecoderAssembly, assembly);

			// Calculatorがセットされていなければ検索
			if (MoteValue<T>.Calculator is null)
			{
				if (assembly is not null
					&& MoteValue<T>.Setup(assembly)
					)
				{
					return;
				}
				if (!MoteValue<T>.Setup(moteDecoderAssembly))
				{
					throw new Exception($"not found ICalculator<{typeof(T).Name}>");
				}
			}

		}

		/// <summary>
		/// スクリプトをデコードする
		/// </summary>
		/// <param name="sentence"></param>
		/// <param name="startat"></param>
		/// <returns></returns>
		public MoteValue<T> Decode(string sentence, int startat = 0)
		{
			if (sentence == null)
			{
				throw new ArgumentNullException(nameof(sentence));
			}
			if (startat < 0 || startat > sentence.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(startat));
			}

			try
			{
				if (s_regexStructuredStatement.IsMatch(sentence, startat)
					|| sentence.IndexOf(';', startat) >= 0)
				{
					return DecodeStructuredScript(sentence, ref startat);
				}
				return DecodeExpression(sentence, startat);
			}
			catch (MoteScriptParseException e)
			{
				if (e.Source == sentence)
				{
					throw;
				}

				int searchStart = Math.Min(startat, sentence.Length) - 1;
				int nestedStart = searchStart >= 0 && !string.IsNullOrEmpty(e.Source)
					? sentence.LastIndexOf(e.Source, searchStart, StringComparison.Ordinal)
					: -1;
				if (nestedStart < 0)
				{
					throw;
				}
				throw new MoteScriptParseException(
					sentence,
					nestedStart + e.Position,
					e.InnerException ?? e);
			}
			catch (Exception e)
			{
				throw new MoteScriptParseException(sentence, startat, e);
			}
		}

		private MoteValue<T> DecodeExpression(string sentence, int startat = 0)
		{
			try
			{
				_queue.Clear();
				SplitSentence(_queue, sentence, ref startat);
				MoteValue<T> result = DecodeInner(_queue);
				return result;
			}
			catch (MoteScriptParseException)
			{
				throw;
			}
			catch (Exception e)
			{
				throw new MoteScriptParseException(sentence, startat, e);
			}
		}


		static readonly Regex s_regexConstValue = new Regex(@"-?\d+(\.\d*)?");
		static readonly Regex s_regexWord = new Regex(@"[a-zA-Z0-9_]+");
		static readonly Regex s_regexStructuredStatement = new Regex(@"\b(if|while|return|break|continue|yield)\b|=>");
		const string CommentCode = "//";

		static bool CanStartSignedConst(MoteValue<T> lastValue)
		{
			if (!lastValue.ValueType.IsValid())
			{
				return true;
			}
			if (!lastValue.ValueType.IsOperator())
			{
				return false;
			}
			return !lastValue.TryGetOperator(out UnaryOperatorCloseBracket<T> _);
		}


		readonly Queue<MoteValue<T>> _queue = new();
		readonly List<MoteValue<T>> _rpn = new();
		readonly DecodedScriptCache<T> _decodedCache = new();
		// MoteDecoder<T> _childDecoder = null;

		/// <summary>
		/// Returns a previously decoded script when the same source is requested again.
		/// Cached syntax trees reuse their array and dictionary result buffers and are
		/// therefore intended for sequential evaluation.
		/// </summary>
		public MoteValue<T> DecodeCached(string sentence)
		{
			if (_decodedCache.TryGetValue(sentence, out MoteValue<T> value))
			{
				return value;
			}
			value = Decode(sentence);
			_decodedCache.Add(sentence, value);
			return value;
		}

		public void ClearDecodedCache()
		{
			_decodedCache.Clear();
		}

		// const char InvalidEndCode = (char)0;

		private MoteValue<T> DecodeStructuredScript(string source, ref int position, char terminator = '\0')
		{
			List<MoteValue<T>> statements = new();
			while (position < source.Length)
			{
				SkipTrivia(source, ref position);
				if (position >= source.Length || (terminator != '\0' && source[position] == terminator))
				{
					break;
				}

				MoteValue<T> statement;
				if (IsKeyword(source, position, "if"))
				{
					statement = ParseIf(source, ref position);
				}
				else if (IsKeyword(source, position, "while"))
				{
					statement = ParseWhile(source, ref position);
				}
				else if (IsKeyword(source, position, "return"))
				{
					statement = ParseReturn(source, ref position, terminator);
				}
				else if (IsKeyword(source, position, "yield"))
				{
					statement = ParseYield(source, ref position);
				}
				else if (IsKeyword(source, position, "break"))
				{
					position += "break".Length;
					statement = new MoteValue<T>(new FlowControlOperatorBreak<T>());
				}
				else if (IsKeyword(source, position, "continue"))
				{
					position += "continue".Length;
					statement = new MoteValue<T>(new FlowControlOperatorContinue<T>());
				}
				else
				{
					string expression = ReadStatementExpression(source, ref position, terminator);
					if (string.IsNullOrWhiteSpace(expression))
					{
						continue;
					}
					statement = DecodeStructuredExpression(expression);
				}
				statements.Add(statement);
				SkipTrivia(source, ref position);
			}

			if (terminator != '\0')
			{
				if (position >= source.Length || source[position] != terminator)
				{
					throw new FormatException($"expected {terminator}");
				}
				++position;
			}

			if (statements.Count == 0)
			{
				return default;
			}
			if (statements.Count == 1)
			{
				return statements[0];
			}
			return new MoteValue<T>(new ScriptBlockOperator<T>(statements));
		}

		private MoteValue<T> ParseIf(string source, ref int position)
		{
			position += "if".Length;
			MoteValue<T> judge = DecodeExpression(ReadEnclosed(source, ref position, '(', ')'));
			MoteValue<T> body = ParseRequiredBlock(source, ref position);
			MoteValue<T> ifValue = new MoteValue<T>(new FlowControlOperatorIf<T>
			{
				Judge = judge,
				Statement = body,
			});

			SkipTrivia(source, ref position);
			if (!IsKeyword(source, position, "else"))
			{
				return ifValue;
			}
			position += "else".Length;
			SkipTrivia(source, ref position);
			MoteValue<T> elseBody = IsKeyword(source, position, "if")
				? ParseIf(source, ref position)
				: ParseRequiredBlock(source, ref position);
			return new MoteValue<T>(new FlowControlOperatorElse<T>
			{
				Judge = ifValue,
				Statement = elseBody,
			});
		}

		private MoteValue<T> DecodeStructuredExpression(string expression)
		{
			int arrowIndex = FindTopLevelToken(expression, "=>");
			if (arrowIndex < 0)
			{
				int topLevelAssignment = FindTopLevelAssignment(expression);
				if (topLevelAssignment >= 0)
				{
					string leftText = expression.Substring(0, topLevelAssignment).Trim();
					string rightText = expression.Substring(topLevelAssignment + 1).Trim();
					MoteValue<T> right = DecodeStructuredValue(rightText);
					return new MoteValue<T>(new BinaryOperatorAssignment<T>
					{
						Left = DecodeExpression(leftText),
						Right = right,
					});
				}
				Match emptyArrayAssignment = Regex.Match(expression,
					@"^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*\(\s*\)\s*$");
				if (emptyArrayAssignment.Success)
				{
					return new MoteValue<T>(new BinaryOperatorAssignment<T>
					{
						Left = new MoteValue<T>(emptyArrayAssignment.Groups[1].Value).ConvertToVariable(),
						Right = new MoteValue<T>(new MoteList<T>()),
					});
				}
				string trimmedExpression = expression.Trim();
				if (IsWrappedInParentheses(trimmedExpression))
				{
					string inner = trimmedExpression.Substring(1, trimmedExpression.Length - 2);
					List<string> elements = SplitTopLevel(inner, ',');
					if (elements.Count > 1)
					{
						return BinaryOperatorArraySeparater<T>.InstantiateParameters(
							elements.Select(element => DecodeExpression(element)));
					}
					return DecodeExpression(inner);
				}
				return DecodeStructuredValue(expression);
			}

			int assignmentIndex = FindTopLevelToken(expression, "=");
			if (assignmentIndex < 0 || assignmentIndex >= arrowIndex)
			{
				throw new FormatException("delegate assignment is required");
			}

			string variableName = expression.Substring(0, assignmentIndex).Trim();
			string parametersText = expression.Substring(
				assignmentIndex + 1,
				arrowIndex - assignmentIndex - 1).Trim();
			if (parametersText.Length < 2 || parametersText[0] != '(' || parametersText[parametersText.Length - 1] != ')')
			{
				throw new FormatException("delegate parameters are invalid");
			}
			FunctionHeaderParser.Parse(
				parametersText.Substring(1, parametersText.Length - 2),
				out string[] parameterNames,
				out string[] localNames);

			string bodyText = expression.Substring(arrowIndex + 2).Trim();
			if (bodyText.Length < 2 || bodyText[0] != '{' || bodyText[bodyText.Length - 1] != '}')
			{
				throw new FormatException("delegate body is invalid");
			}
			int bodyPosition = 1;
			MoteValue<T> body = DecodeStructuredScript(bodyText, ref bodyPosition, '}');
			MoteValue<T> function = BinaryOperatorDelegate<T>.CreateFunction(
				parameterNames,
				localNames,
				body);
			return new MoteValue<T>(new BinaryOperatorAssignment<T>
			{
				Left = new MoteValue<T>(variableName).ConvertToVariable(),
				Right = function,
			});
		}

		private MoteValue<T> DecodeStructuredValue(string expression)
		{
			string trimmed = expression.Trim();
			Match assignedProduct = Regex.Match(trimmed,
				@"^\(\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*([^()]*)\)\s*\*\s*(.+)$");
			if (assignedProduct.Success)
			{
				return new MoteValue<T>(new BinaryOperatorMultiple<T>
				{
					Left = new MoteValue<T>(new BinaryOperatorAssignment<T>
					{
						Left = new MoteValue<T>(assignedProduct.Groups[1].Value).ConvertToVariable(),
						Right = DecodeExpression(assignedProduct.Groups[2].Value),
					}),
					Right = DecodeExpression(assignedProduct.Groups[3].Value),
				});
			}
			if (Regex.IsMatch(trimmed, @"^\(\s*\)$"))
			{
				return new MoteValue<T>(new MoteList<T>());
			}
			if (trimmed.StartsWith("new ", StringComparison.Ordinal))
			{
				return new MoteValue<T>(new UnaryOperatorNew<T>
				{
					Right = DecodeStructuredValue(trimmed.Substring(4)),
				});
			}
			if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
			{
				string inner = trimmed.Substring(1, trimmed.Length - 2);
				List<MoteValue<T>> pairs = new();
				if (!string.IsNullOrWhiteSpace(inner))
				{
					foreach (string item in SplitTopLevel(inner, ','))
					{
						List<string> parts = SplitTopLevel(item, ':');
						if (parts.Count != 2) throw new FormatException("dictionary entry is invalid");
						pairs.Add(new MoteValue<T>(new BinaryOperatorKeyValuePair<T>
						{
							Left = new MoteValue<T>(parts[0].Trim()).ConvertToVariable(),
							Right = DecodeExpression(parts[1]),
						}));
					}
				}
				MoteValue<T> entries = pairs.Count == 1
					? pairs[0]
					: BinaryOperatorArraySeparater<T>.InstantiateParameters(pairs);
				return new MoteValue<T>(new DefinitionOperatorDictionary<T>(new Context<T>(), entries));
			}
			return DecodeExpression(trimmed);
		}

		private static int FindTopLevelAssignment(string expression)
		{
			return StructuredExpressionReader.FindAssignment(expression);
		}

		private static int FindTopLevelToken(string expression, string token)
		{
			return StructuredExpressionReader.FindToken(expression, token);
		}

		private static bool IsWrappedInParentheses(string expression)
		{
			return StructuredExpressionReader.IsWrappedInParentheses(expression);
		}

		private static List<string> SplitTopLevel(string expression, char separator)
		{
			return StructuredExpressionReader.Split(expression, separator);
		}

		private MoteValue<T> ParseWhile(string source, ref int position)
		{
			position += "while".Length;
			return new MoteValue<T>(new FlowControlOperatorWhile<T>
			{
				Judge = DecodeExpression(ReadEnclosed(source, ref position, '(', ')')),
				Statement = ParseRequiredBlock(source, ref position),
			});
		}

		private MoteValue<T> ParseReturn(string source, ref int position, char terminator)
		{
			position += "return".Length;
			string expression = ReadStatementExpression(source, ref position, terminator);
			if (string.IsNullOrWhiteSpace(expression))
			{
				throw new FormatException("return value is required");
			}
			return new MoteValue<T>(new FlowControlOperatorReturn<T>
			{
				Statement = DecodeExpression(expression),
			});
		}

		private MoteValue<T> ParseYield(string source, ref int position)
		{
			position += "yield".Length;
			string expression = ReadEnclosed(source, ref position, '(', ')');
			if (string.IsNullOrWhiteSpace(expression))
			{
				throw new FormatException("yield value is required");
			}
			SkipTrivia(source, ref position);
			if (position < source.Length && source[position] == ';')
			{
				++position;
			}
			return new MoteValue<T>(new FlowControlOperatorYield<T>
			{
				Statement = DecodeExpression(expression),
			});
		}

		private MoteValue<T> ParseRequiredBlock(string source, ref int position)
		{
			SkipTrivia(source, ref position);
			if (position >= source.Length || source[position] != '{')
			{
				throw new FormatException("expected block");
			}
			++position;
			return DecodeStructuredScript(source, ref position, '}');
		}

		private static string ReadEnclosed(string source, ref int position, char open, char close)
		{
			SkipTrivia(source, ref position);
			if (position >= source.Length || source[position] != open)
			{
				throw new FormatException($"expected {open}");
			}
			int start = ++position;
			int depth = 1;
			while (position < source.Length && depth > 0)
			{
				char c = source[position++];
				if (c == '"')
				{
					position = FindStringLiteralEnd(source, position - 1) + 1;
				}
				else if (c == open) ++depth;
				else if (c == close) --depth;
			}
			if (depth != 0) throw new FormatException($"expected {close}");
			return source.Substring(start, position - start - 1);
		}

		private static string ReadStatementExpression(string source, ref int position, char terminator)
		{
			int start = position;
			int round = 0;
			int square = 0;
			int curly = 0;
			while (position < source.Length)
			{
				char c = source[position];
				if (c == '"')
				{
					position = FindStringLiteralEnd(source, position) + 1;
					continue;
				}
				if (c == '(') ++round;
				else if (c == ')') --round;
				else if (c == '[') ++square;
				else if (c == ']') --square;
				else if (c == '{') ++curly;
				else if (c == '}')
				{
					if (curly == 0 && terminator == '}') break;
					--curly;
				}
				else if (c == ';' && round == 0 && square == 0 && curly == 0)
				{
					string result = source.Substring(start, position - start);
					++position;
					return result;
				}
				++position;
			}
			return source.Substring(start, position - start);
		}

		private static int FindStringLiteralEnd(string source, int start)
		{
			return ScriptTextReader.FindStringLiteralEnd(source, start);
		}

		private static bool TryReadStringLiteral(
			string source,
			ref int position,
			out MoteValue<T> value)
		{
			if (!ScriptTextReader.TryReadStringLiteral(source, ref position, out string stringValue))
			{
				value = default;
				return false;
			}
			value = new MoteValue<T>(stringValue);
			return true;
		}

		private static bool IsKeyword(string source, int position, string keyword)
		{
			return ScriptTextReader.IsKeyword(source, position, keyword);
		}

		private static void SkipTrivia(string source, ref int position)
		{
			ScriptTextReader.SkipTrivia(source, ref position);
		}


		internal void SplitSentence(
			Queue<MoteValue<T>> queue
			, string sentence
			, ref int startat
			, int operatorPriority = int.MinValue
			)
		{
			MoteValue<T> lastValue = default;
			while (startat < sentence.Length)
			{
				char c = sentence[startat];
				if (IsSpace(c))
				{
					++startat;
					continue;
				}

				if (TryReadStringLiteral(sentence, ref startat, out MoteValue<T> stringValue))
				{
					queue.Enqueue(stringValue);
					lastValue = stringValue;
					continue;
				}

				if (TrimComment(sentence, ref startat))
				{
					continue;
				}

				// 括弧取得
				if (TryGetBracketOperator(sentence, startat, out OperatorInfo bracketOperatorInfo))
				{
					lastValue = EnqueueOperator(queue, ref startat, bracketOperatorInfo);
					continue;
				}

				// if (lastValue.ValueType == EValueType.Unknown
				// 	|| lastValue.ValueType == EValueType.UnaryOperator
				// 	|| lastValue.ValueType == EValueType.BinaryOperator
				// 	)
				{
					// 定数？
					if (TryGetConstValue(sentence, startat, out string value)
						&& (value[0] != '-' || CanStartSignedConst(lastValue)))
					{
						// 値
						var constValue = MoteValue<T>.GetConstValue(value);
						queue.Enqueue(constValue);
						startat += value.Length;
						lastValue = constValue;
						continue;
					}
					// 単項演算子？
					if (TryGetUnaryOperator(sentence, startat, out OperatorInfo unrayOperatorInfo))
					{
						lastValue = EnqueueOperator(queue, ref startat, unrayOperatorInfo);
						continue;
					}
					if (TryGetBinaryOperator(sentence, startat, out OperatorInfo binaryOperatorInfo))
					{
						lastValue = EnqueueOperator(queue, ref startat, binaryOperatorInfo);
						continue;
					}
					// 制御構文？
					if (TryGetFlowControlOperator(sentence, ref startat, out OperatorInfo flowControlOperatorInfo))
					{
						lastValue = EnqueueOperator(queue, ref startat, flowControlOperatorInfo);
						continue;
					}
					// 英数字？					
					if (TryGetWord(sentence, startat, out string word))
					{
						if (word == "null")
						{
							var nullValue = new MoteValue<T>((string)null);
							queue.Enqueue(nullValue);
							startat += word.Length;
							lastValue = nullValue;
							continue;
						}
						// 変数
						var wordValue = new MoteValue<T>(word).ConvertToVariable();
						queue.Enqueue(wordValue);
						startat += word.Length;
						lastValue = wordValue;
						continue;
					}
					else
					{
						throw new FormatException();
					}
				}
			}

			return;
		}
		private MoteValue<T> EnqueueOperator(
			Queue<MoteValue<T>> queue
			, ref int startat
			, OperatorInfo operatorInfo
			)
		{
			var value = new MoteValue<T>(
				Activator.CreateInstance(operatorInfo.Type)
				as IOperator);
			queue.Enqueue(value);
			startat += operatorInfo.OperatorCode.Length;
			return value;
		}

		// internal MoteValue<T> DecodeChild(string sentence, ref int startat)
		// {
		// 	if (_childDecoder is null)
		// 	{
		// 		_childDecoder = new MoteDecoder<T>();
		// 	}
		// 	var childValue = _childDecoder.DecodeInner(sentence, ref startat);
		// 	return childValue;
		// }
		static bool IsSpace(char c)
		{
			switch (c)
			{
			case ' ':
			case '\t':
			case '　':
			case '\n':
			case '\r':
				return true;
			default:
				return false;
			}
		}
		static bool IsReturnCode(char c)
		{
			switch (c)
			{
			case '\r':
			case '\n':
				return true;
			default:
				return false;
			}
		}
		// static bool TryGetChar(string sentence, ref int startat, out char c)
		// {
		// 	if (sentence.Length <= startat)
		// 	{
		// 		c = default;
		// 		return false;
		// 	}
		// 	c = sentence[startat++];
		// 	return true;
		// }
		// static bool TryPeekChar(string sentence, ref int startat, out char c)
		// {
		// 	if (sentence.Length <= startat)
		// 	{
		// 		c = default;
		// 		return false;
		// 	}
		// 	c = sentence[startat];
		// 	return true;
		// }
		static bool TrimStart(string sentence, ref int startat)
		{
			while (startat < sentence.Length)
			{
				if (!IsSpace(sentence[startat]))
				{
					return true;
				}
				++startat;
			}
			return false;
		}
		static bool TrimComment(string sentence, ref int startat)
		{
			if (sentence.Length < startat + CommentCode.Length)
			{
				return false;
			}
			if (sentence[startat..(startat + CommentCode.Length)] == CommentCode)
			{
				startat += CommentCode.Length;
				// コメント部分スキップ
				do
				{
					if (sentence.Length <= startat)
					{
						return true;
					}
				} while (!IsReturnCode(sentence[startat++]));
				// 改行削除
				while (IsReturnCode(sentence[startat++]));
				return true;
			}
			return false;
		}
		static bool TryGetConstValue(string sentence, int startat, out string value)
		{
			Match match = s_regexConstValue.Match(sentence, startat);
			if (match.Success)
			{
				Group group = match.Groups[0];
				if (startat == group.Index)
				{
					value = sentence.Substring(startat, group.Length);
					return true;
				}
			}
			value = default;
			return false;
		}
		static bool TryGetWord(string sentence, int startat, out string value)
		{
			Match match = s_regexWord.Match(sentence, startat);
			if (match.Success)
			{
				Group group = match.Groups[0];
				if (startat == group.Index)
				{
					value = sentence.Substring(startat, group.Length);
					return true;
				}
			}
			value = default;
			return false;
		}


		internal bool TryGetFlowControlOperator(string sentence, ref int startat, out OperatorInfo value)
		{
			if (!TrimStart(sentence, ref startat))
			{
				value = default;
				return false;
			}
			foreach (OperatorInfo op in OperatorRegistry<T>.FlowControlOperators)
			{
				if (0 == string.CompareOrdinal(
						sentence, startat
						, op.OperatorCode, 0, op.OperatorCode.Length))
				{
					value = op;
					return true;
				}
			}

			value = default;
			return false;
		}

		internal bool TryGetBinaryOperator(string sentence, int startat, out OperatorInfo value)
		{
			foreach (OperatorInfo op in OperatorRegistry<T>.BinaryOperators)
			{
				if (0 == string.CompareOrdinal(
						sentence, startat
						, op.OperatorCode, 0, op.OperatorCode.Length))
				{
					value = op;
					return true;
				}
			}

			value = default;
			return false;
		}

		internal bool TryGetUnaryOperator(string sentence, int startat, out OperatorInfo value)
		{
			foreach (OperatorInfo op in OperatorRegistry<T>.UnaryOperators)
			{
				if (0 == string.CompareOrdinal(
						sentence, startat
						, op.OperatorCode, 0, op.OperatorCode.Length))
				{
					value = op;
					return true;
				}
			}

			value = default;
			return false;
		}
		internal bool TryGetBracketOperator(string sentence, int startat, out OperatorInfo value)
		{
			foreach (OperatorInfo op in OperatorRegistry<T>.BracketOperators)
			{
				if (0 == string.CompareOrdinal(
						sentence, startat
						, op.OperatorCode, 0, op.OperatorCode.Length))
				{
					value = op;
					return true;
				}
			}

			value = default;
			return false;
		}


		private MoteValue<T> DecodeInner(
			Queue<MoteValue<T>> queue
			, UnaryOperatorOpenBracket<T> currentBracket = null
			)
		{
			// リスト一時ワーク取得
			List<MoteValue<T>> rpn = RentalMoteValueList();
			MoteValue<T> value;
			try
			{
				ConvertToRpn(queue, rpn, currentBracket);
				value = FinalizeRpn(rpn);
			}
			finally
			{
				// リスト返却
				ReturnMoteValueList(rpn);
			}
			return value;
		}
		private void ConvertToRpn(
			Queue<MoteValue<T>> queue
			, List<MoteValue<T>> rpn
			, UnaryOperatorOpenBracket<T> currentBracket = null
			)
		{
#if VERVOSE
			UnityEngine.Debug.Log("queue:" + string.Join("\n", queue.ToArray()));
#endif
			while (0 < queue.Count)
			{
				MoteValue<T> value = queue.Dequeue();
				if (value.ValueType.IsOperator())
				{
					switch (value.GetOperator())
					{
						case BinaryOperator<T> binaryOperator:
							InsertBinaryOperator(queue, rpn, binaryOperator);
							break;
						
						case UnaryOperatorOpenBracket<T> openBracket:
							MoteValue<T> bracketValue = DecodeInner(queue, openBracket);
							if (openBracket.BracketsType == Brackets.EType.SquareBrackets
								&& rpn.Count > 0
								&& rpn[rpn.Count - 1].TryGetOperator(out BinaryOperatorSentenceSeparater<T> _))
							{
								int insertIndex = rpn.Count - 1;
								rpn.Insert(insertIndex++, bracketValue);
								rpn.Insert(insertIndex, value);
							}
							else
							{
								rpn.Add(bracketValue);
								rpn.Add(value);
							}
							break;

						case UnaryOperatorCloseBracket<T> closeBracket:
							if (currentBracket != null
								&& currentBracket.IsCloseBracket(closeBracket)
								)
							{
								return;
							}
							throw new FormatException($"対応する括弧なし : {closeBracket.ToString()}");
	
						case UnaryOperator<T> unrayOperator:
							GetOneValue(queue, rpn);
							rpn.Add(value);
							break;
						default:
							throw new InvalidOperationException();
					}
				}
				else
				{
					rpn.Add(value);
				}
			}
			if (currentBracket != null)
			{
				throw new FormatException($"対応する閉じ括弧なし : {currentBracket.OperatorCode}");
			}
		}

		private void InsertBinaryOperator(
			Queue<MoteValue<T>> queue
			, List<MoteValue<T>> rpn
			, BinaryOperator<T> binaryOperator			
			)
		{
			if (queue.Count == 0)
			{
				throw new FormatException("right value nothing.");
			}
			int insertIndex = GetInsertPosition(rpn, binaryOperator.Priority);
			var childRpn = RentalMoteValueList();
			try
			{
				GetOneValue(queue, childRpn);
				rpn.InsertRange(insertIndex, childRpn);
				bool isCompoundRightValue = 1 < childRpn.Count
					&& (childRpn[childRpn.Count - 1]
							.TryGetOperator(out OperatorOpenSquareBracket<T> _)
						|| childRpn[childRpn.Count - 1]
							.TryGetOperator(out UnaryOperatorNew<T> _));
				if (isCompoundRightValue)
				{
					insertIndex += childRpn.Count;
				}
				else
				{
					++insertIndex;
				}
				rpn.Insert(insertIndex, new MoteValue<T>(binaryOperator));
			}
			finally
			{
				ReturnMoteValueList(childRpn);
			}
		}

		/// <summary>
		/// 値を取得
		/// </summary>
		/// <param name="queue"></param>
		private void GetOneValue(
			Queue<MoteValue<T>> queue
			, List<MoteValue<T>> rpn
		)
		{
			MoteValue<T> value = queue.Dequeue();
			if (value.ValueType.IsOperator())
			{
				switch (value.GetOperator())
				{
					case UnaryOperatorOpenBracket<T> openBracket:
						// カッコ内の値取得
						MoteValue<T> bracket = GetBrackets(queue, openBracket);
						rpn.Add(bracket);
						rpn.Add(value);
						break;

					case UnaryOperatorCloseBracket<T> closeBracket:
						// throw new FormatException($"対応する括弧なし : {closeBracket}");
						rpn.Add(value);
						return;

					case UnaryOperator<T> unrayOperator:
					{
						GetOneValue(queue, rpn);
						rpn.Add(value);
						break;
					}

					case BinaryOperator<T> binaryOperator:
						throw new FormatException($"左辺値なし : {binaryOperator.ToString()}");
					default:
						throw new InvalidOperationException();
				}
			}
			else
			{
				rpn.Add(value);
			}
			GetPostfixBrackets(queue, rpn);
		}
		private void GetPostfixBrackets(
			Queue<MoteValue<T>> queue
			, List<MoteValue<T>> rpn
		)
		{
			while (0 < queue.Count
				&& queue.Peek().TryGetOperator(out UnaryOperatorOpenBracket<T> openBracket)
				&& openBracket.BracketsType == Brackets.EType.SquareBrackets)
			{
				MoteValue<T> bracketOperator = queue.Dequeue();
				MoteValue<T> bracket = GetBrackets(queue, openBracket);
				rpn.Add(bracket);
				rpn.Add(bracketOperator);
			}
		}
		private MoteValue<T> GetBrackets(
			Queue<MoteValue<T>> queue
			, UnaryOperatorOpenBracket<T> openBracket
			)
		{
			return DecodeInner(queue, currentBracket: openBracket);
		}
		public static int GetInsertPosition(
			IReadOnlyList<MoteValue<T>> rpn
			, int operatorPriority
			)
		{
			for (int i = rpn.Count - 1; 0 <= i; --i)
			{
				var value = rpn[i];
				if (!value.ValueType.IsOperator()
					|| value.GetOperator().IsFinalized)
				{
					return i + 1;
				}
				IOperator binaryOperator = value.GetOperator();
				if (binaryOperator.Priority <= operatorPriority)
				{
					return i + 1;
				}
			}
			throw new FormatException("left value nothing");
		}

		readonly Stack<MoteValue<T>> _rpnStack = new();
		readonly List<IOperatorOnFinalized> _onFinalizedList = new();
		/// <summary>
		/// 逆ポーランド状態から最終結果作成
		/// </summary>
		MoteValue<T> FinalizeRpn(List<MoteValue<T>> rpn)
		{
			if (0 == rpn.Count)
			{
				// 値なし
				return new MoteValue<T>();
			}
			_rpnStack.Clear();
			_onFinalizedList.Clear();
			foreach (MoteValue<T> value in rpn)
			{
				MoteValue<T> pushValue;
				if (value.ValueType.IsOperator()
					&& !value.GetOperator().IsFinalized)
				{
					if (value.TryGetOperator(out IRpnOperator<T> op))
					{
						pushValue = op.FinalizeOperator(_rpnStack);
						if (op is IOperatorOnFinalized opOnFinalized)
						{
							if (opOnFinalized.IsOnFinishedRequired)
							{
								_onFinalizedList.Add(op as IOperatorOnFinalized);
							}
						}
					}
					else
					{
						throw new InvalidOperationException();
					}
				}
				else
				{
					pushValue = value;
				}
				_rpnStack.Push(pushValue);
			}

			// 最終処理
			foreach (var op in _onFinalizedList)
			{
				op.OnFinalized();
			}
			_onFinalizedList.Clear();
			return _rpnStack.Pop();
		}

		#region RentalList
		private readonly Stack<List<MoteValue<T>>> _valueListStack = new();

		private List<MoteValue<T>> RentalMoteValueList()
		{
			if (0 < _valueListStack.Count)
			{
				return _valueListStack.Pop();
			}
			var valueList = new List<MoteValue<T>>();
			return valueList;
		}
		private void ReturnMoteValueList(List<MoteValue<T>> valueList)
		{
			valueList.Clear();
			_valueListStack.Push(valueList);
		}

		#endregion// RentalList


	}

}
