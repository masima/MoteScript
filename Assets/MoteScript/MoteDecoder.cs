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
		/// <summary>
		/// 演算子リスト
		/// </summary>
		private static readonly List<OperatorInfo> s_unrayOperators = new();
		private static readonly List<OperatorInfo> s_flowControlOperators = new();
		private static readonly List<OperatorInfo> s_binaryOperators = new();
		private static readonly List<OperatorInfo> s_bracketOperators = new();
		private static Type s_contextType;

		public static void Setup(
			Assembly assembly = null
			, Type contextType = null
			)
		{
			s_flowControlOperators.Clear();
			s_binaryOperators.Clear();

			Assembly moteDecoderAssembly = typeof(MoteDecoder<T>).Assembly;
			RegisterOperators(moteDecoderAssembly);
			if (assembly is not null && moteDecoderAssembly != assembly)
			{
				RegisterOperators(assembly);
			}

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

			if (contextType == null)
			{
				s_contextType = typeof(Context<T>);
			}
			else
			{
				s_contextType = contextType;
			}
		}
		private static void RegisterOperators(Assembly assembly)
		{
			Type[] binaryOperatorTypes = assembly.GetTypes()
				.Where(_ =>
					_.BaseType != null
					&& !_.IsAbstract
					&& _.BaseType.IsGenericType
					// && _.BaseType.GetGenericTypeDefinition() == typeof(IOperator<>)
					)
				.ToArray();

			foreach (Type type in binaryOperatorTypes)
			{
				List<OperatorInfo> infos;
				var genericTypeDefinition = type.BaseType.GetGenericTypeDefinition();
				if (genericTypeDefinition == typeof(UnrayOperatorOpenBracket<>)
					|| genericTypeDefinition == typeof(UnrayOperatorCloseBracket<>)
					)
				{
					infos = s_bracketOperators;
				}
				else if (genericTypeDefinition == typeof(FlowControlOperator<>))
				{
					infos = s_flowControlOperators;
				}
				else if (genericTypeDefinition == typeof(BinaryOperator<>))
				{
					infos = s_binaryOperators;
				}
				else if (genericTypeDefinition == typeof(UnrayOperator<>))
				{
					infos = s_unrayOperators;
				}
				else
				{
					continue;
				}
				var genericType = type.MakeGenericType(typeof(T));
				var propertyInfo = genericType.GetProperty(nameof(IOperator<T>.OperatorCode));
				if (null == propertyInfo)
				{
					continue;
				}
				var opInstance = Activator.CreateInstance(genericType) as IOperator;
				string code = opInstance.OperatorCode;
				if (!string.IsNullOrEmpty(code))
				{
					infos.Add(new OperatorInfo
					{
						Type = genericType,
						OperatorCode = code,
						Priority = opInstance.Priority,
					});
				}
			}
			// 文字数の多い順に並べる
			s_flowControlOperators.Sort((left, right) => right.OperatorCode.Length - left.OperatorCode.Length);
			s_binaryOperators.Sort((left, right) => right.OperatorCode.Length - left.OperatorCode.Length);
			s_unrayOperators.Sort((left, right) => right.OperatorCode.Length - left.OperatorCode.Length);
		}

		/// <summary>
		/// スクリプトをデコードする
		/// </summary>
		/// <param name="sentence"></param>
		/// <param name="startat"></param>
		/// <returns></returns>
		public MoteValue<T> Decode(string sentence, int startat = 0)
		{
			if (s_regexStructuredStatement.IsMatch(sentence, startat)
				|| sentence.IndexOf(';', startat) >= 0)
			{
				return DecodeStructuredScript(sentence, ref startat);
			}
			return DecodeExpression(sentence, startat);
		}

		private MoteValue<T> DecodeExpression(string sentence, int startat = 0)
		{
			#if CATCH_EXCEPTION
			try
			#endif
			{
				_queue.Clear();
				SplitSentence(_queue, sentence, ref startat);
				MoteValue<T> result = DecodeInner(_queue);
				return result;
			}
			#if CATCH_EXCEPTION
			catch (Exception e)
			{
				throw new FormatException(GetErrorPositionMessage(sentence, startat), e);
			}
			#endif
		}


		static readonly Regex s_regexConstValue = new Regex(@"-?\d+(\.\d*)?");
		static readonly Regex s_regexWord = new Regex(@"[a-zA-Z0-9_]+");
		static readonly Regex s_regexStructuredStatement = new Regex(@"\b(if|while|return|break|continue)\b|=>");
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
			return !lastValue.TryGetOperator(out UnrayOperatorCloseBracket<T> _);
		}


		readonly Queue<MoteValue<T>> _queue = new();
		readonly List<MoteValue<T>> _rpn = new();
		readonly Dictionary<string, MoteValue<T>> _decodedCache = new();
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
			int arrowIndex = expression.IndexOf("=>", StringComparison.Ordinal);
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

			int assignmentIndex = expression.IndexOf('=');
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
			string[] parameterNames = parametersText.Substring(1, parametersText.Length - 2)
				.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(name => name.Trim())
				.ToArray();

			string bodyText = expression.Substring(arrowIndex + 2).Trim();
			if (bodyText.Length < 2 || bodyText[0] != '{' || bodyText[bodyText.Length - 1] != '}')
			{
				throw new FormatException("delegate body is invalid");
			}
			int bodyPosition = 1;
			MoteValue<T> body = DecodeStructuredScript(bodyText, ref bodyPosition, '}');
			MoteValue<T> function = BinaryOperatorDelegate<T>.CreateFunction(parameterNames, body);
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
				return new MoteValue<T>(new UnrayOperatorNew<T>
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
			int round = 0;
			int square = 0;
			for (int i = 0; i < expression.Length; ++i)
			{
				char c = expression[i];
				if (c == '(') ++round;
				else if (c == ')') --round;
				else if (c == '[') ++square;
				else if (c == ']') --square;
				else if (c == '=' && round == 0 && square == 0
					&& (i == 0 || expression[i - 1] != '=' && expression[i - 1] != '!' && expression[i - 1] != '<' && expression[i - 1] != '>')
					&& (i + 1 >= expression.Length || expression[i + 1] != '='))
				{
					return i;
				}
			}
			return -1;
		}

		private static bool IsWrappedInParentheses(string expression)
		{
			if (expression.Length < 2 || expression[0] != '(' || expression[expression.Length - 1] != ')')
			{
				return false;
			}
			int depth = 0;
			for (int i = 0; i < expression.Length; ++i)
			{
				if (expression[i] == '(') ++depth;
				else if (expression[i] == ')' && --depth == 0 && i != expression.Length - 1) return false;
			}
			return depth == 0;
		}

		private static List<string> SplitTopLevel(string expression, char separator)
		{
			var results = new List<string>();
			int start = 0;
			int round = 0;
			int square = 0;
			int curly = 0;
			for (int i = 0; i < expression.Length; ++i)
			{
				char c = expression[i];
				if (c == '(') ++round;
				else if (c == ')') --round;
				else if (c == '[') ++square;
				else if (c == ']') --square;
				else if (c == '{') ++curly;
				else if (c == '}') --curly;
				else if (c == separator && round == 0 && square == 0 && curly == 0)
				{
					results.Add(expression.Substring(start, i - start));
					start = i + 1;
				}
			}
			results.Add(expression.Substring(start));
			return results;
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
				if (c == open) ++depth;
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

		private static bool IsKeyword(string source, int position, string keyword)
		{
			if (position < 0 || position + keyword.Length > source.Length
				|| string.CompareOrdinal(source, position, keyword, 0, keyword.Length) != 0)
			{
				return false;
			}
			int end = position + keyword.Length;
			return (position == 0 || !char.IsLetterOrDigit(source[position - 1]) && source[position - 1] != '_')
				&& (end == source.Length || !char.IsLetterOrDigit(source[end]) && source[end] != '_');
		}

		private static void SkipTrivia(string source, ref int position)
		{
			while (position < source.Length)
			{
				if (char.IsWhiteSpace(source[position]) || source[position] == ';')
				{
					++position;
					continue;
				}
				if (position + 1 < source.Length && source[position] == '/' && source[position + 1] == '/')
				{
					position += 2;
					while (position < source.Length && source[position] != '\n' && source[position] != '\r') ++position;
					continue;
				}
				break;
			}
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
				// 	|| lastValue.ValueType == EValueType.UnrayOperator
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
					if (TryGetUnrayOperator(sentence, startat, out OperatorInfo unrayOperatorInfo))
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
		static string GetErrorPositionMessage(string sentence, int startat)
		{
			int clipLength = 8;
			int startClipPosition = startat - clipLength;
			int startClipLength = clipLength;
			if (startClipPosition < 0)
			{
				startClipLength += startClipPosition;
				startClipPosition = 0;
			}
			int endClipLength = clipLength;
			if (sentence.Length < startat + clipLength)
			{
				endClipLength = sentence.Length - startat;
			}

			return $"({startat.ToString()})"
				+ $" : {sentence.Substring(startClipPosition, startClipLength)}"
				+ "^^^"
				+ $" {sentence.Substring(startat, endClipLength)}";
		}
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
			foreach (OperatorInfo op in s_flowControlOperators)
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
			foreach (OperatorInfo op in s_binaryOperators)
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

		internal bool TryGetUnrayOperator(string sentence, int startat, out OperatorInfo value)
		{
			foreach (OperatorInfo op in s_unrayOperators)
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
			foreach (OperatorInfo op in s_bracketOperators)
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
			, UnrayOperatorOpenBracket<T> currentBracket = null
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
			, UnrayOperatorOpenBracket<T> currentBracket = null
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
						
						case UnrayOperatorOpenBracket<T> openBracket:
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

						case UnrayOperatorCloseBracket<T> closeBracket:
							if (currentBracket != null
								&& currentBracket.IsCloseBracket(closeBracket)
								)
							{
								return;
							}
							throw new FormatException($"対応する括弧なし : {closeBracket.ToString()}");
	
						case UnrayOperator<T> unrayOperator:
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
				if (1 < childRpn.Count
					&& childRpn[childRpn.Count - 1]
						.TryGetOperator(out OperatorOpenSquareBracket<T> _))
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
					case UnrayOperatorOpenBracket<T> openBracket:
						// カッコ内の値取得
						MoteValue<T> bracket = GetBrackets(queue, openBracket);
						rpn.Add(bracket);
						rpn.Add(value);
						break;

					case UnrayOperatorCloseBracket<T> closeBracket:
						// throw new FormatException($"対応する括弧なし : {closeBracket}");
						rpn.Add(value);
						return;

					case UnrayOperator<T> unrayOperator:
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
				&& queue.Peek().TryGetOperator(out UnrayOperatorOpenBracket<T> openBracket)
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
			, UnrayOperatorOpenBracket<T> openBracket
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
						pushValue = op.Finailze(_rpnStack);
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
