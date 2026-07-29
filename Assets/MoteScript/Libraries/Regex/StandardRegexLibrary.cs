using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MoteScript
{
	/// <summary>
	/// Registers timeout-protected regular expression operations under a child context.
	/// </summary>
	public static class StandardRegexLibrary<T>
		where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
	{
		private static readonly TimeSpan s_defaultMatchTimeout = TimeSpan.FromMilliseconds(100);

		public static void Register(
			IContext<T> context,
			string libraryName = "regex",
			RegexOptions options = RegexOptions.CultureInvariant,
			TimeSpan? matchTimeout = null,
			int maxCachedPatterns = 64)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}
			if (string.IsNullOrEmpty(libraryName))
			{
				throw new ArgumentException("Library name is required.", nameof(libraryName));
			}
			if ((options & RegexOptions.Compiled) != 0)
			{
				throw new ArgumentException(
					"RegexOptions.Compiled is not supported by StandardRegexLibrary.",
					nameof(options));
			}
			if (maxCachedPatterns <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxCachedPatterns));
			}

			TimeSpan timeout = matchTimeout ?? s_defaultMatchTimeout;
			if (timeout <= TimeSpan.Zero || timeout == Regex.InfiniteMatchTimeout)
			{
				throw new ArgumentOutOfRangeException(
					nameof(matchTimeout),
					"A finite, positive regular expression timeout is required.");
			}

			var library = new RegexLibraryContext(
				options | RegexOptions.CultureInvariant,
				timeout,
				maxCachedPatterns);
			var libraryValue = new MoteValue<T>(library);
			if (context.TryGetValue(libraryName, out MoteValue<T> current))
			{
				MoteValue<T>.ValidateAssignment(current, libraryValue, libraryName);
			}
			context[libraryName] = libraryValue;
		}

		public static void ClearCache(IContext<T> context, string libraryName = "regex")
		{
			GetLibrary(context, libraryName).ClearRegexCache();
		}

		public static int GetCachedPatternCount(
			IContext<T> context,
			string libraryName = "regex")
		{
			return GetLibrary(context, libraryName).CachedPatternCount;
		}

		private static RegexLibraryContext GetLibrary(
			IContext<T> context,
			string libraryName)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}
			if (!context.TryGetValue(libraryName, out MoteValue<T> value)
				|| !(value.GetDictionary() is RegexLibraryContext library))
			{
				throw new InvalidOperationException(
					$"StandardRegexLibrary is not registered as '{libraryName}'.");
			}
			return library;
		}

		private sealed class RegexLibraryContext : Context<T>
		{
			private readonly RegexCache _cache;

			internal int CachedPatternCount => _cache.Count;

			internal RegexLibraryContext(
				RegexOptions options,
				TimeSpan matchTimeout,
				int maxCachedPatterns)
			{
				_cache = new RegexCache(options, matchTimeout, maxCachedPatterns);
				this["isMatch"] = Function(IsMatch);
				this["match"] = Function(Match);
				this["matches"] = Function(Matches);
				this["replace"] = Function(Replace);
			}

			internal void ClearRegexCache()
			{
				_cache.Clear();
			}

			private delegate MoteValue<T> LibraryFunction(
				IContext<T> context,
				List<MoteValue<T>> values);

			private static MoteValue<T> Function(LibraryFunction function)
			{
				return new MoteValue<T>(
					(context, receiver, values) =>
						function(context, values));
			}

			private MoteValue<T> IsMatch(
				IContext<T> context,
				List<MoteValue<T>> values)
			{
				RequireCount(values, 2, 2, "regex.isMatch");
				string input = GetRequiredString(values[0], context, "regex.isMatch");
				string pattern = GetRequiredString(values[1], context, "regex.isMatch");
				return new MoteValue<T>(_cache.Get(pattern).IsMatch(input));
			}

			private MoteValue<T> Match(
				IContext<T> context,
				List<MoteValue<T>> values)
			{
				RequireCount(values, 2, 2, "regex.match");
				string input = GetRequiredString(values[0], context, "regex.match");
				string pattern = GetRequiredString(values[1], context, "regex.match");
				return CreateMatchValue(_cache.Get(pattern).Match(input));
			}

			private MoteValue<T> Matches(
				IContext<T> context,
				List<MoteValue<T>> values)
			{
				RequireCount(values, 2, 2, "regex.matches");
				string input = GetRequiredString(values[0], context, "regex.matches");
				string pattern = GetRequiredString(values[1], context, "regex.matches");
				MatchCollection matches = _cache.Get(pattern).Matches(input);
				var results = new MoteList<T>
				{
					Capacity = matches.Count,
				};
				foreach (Match match in matches)
				{
					results.Add(CreateMatchValue(match));
				}
				return new MoteValue<T>(results);
			}

			private MoteValue<T> Replace(
				IContext<T> context,
				List<MoteValue<T>> values)
			{
				RequireCount(values, 3, 3, "regex.replace");
				string input = GetRequiredString(values[0], context, "regex.replace");
				string pattern = GetRequiredString(values[1], context, "regex.replace");
				string replacement = GetRequiredString(values[2], context, "regex.replace");
				return new MoteValue<T>(_cache.Get(pattern).Replace(input, replacement));
			}

			private MoteValue<T> CreateMatchValue(Match match)
			{
				IContext<T> result = Instantiate();
				result["success"] = new MoteValue<T>(match.Success);
				result["value"] = new MoteValue<T>(match.Value);
				result["index"] = new MoteValue<T>(match.Index);
				result["length"] = new MoteValue<T>(match.Length);

				var groups = new MoteList<T>
				{
					Capacity = match.Groups.Count,
				};
				foreach (Group group in match.Groups)
				{
					IContext<T> groupResult = Instantiate();
					groupResult["success"] = new MoteValue<T>(group.Success);
					groupResult["value"] = new MoteValue<T>(group.Value);
					groupResult["index"] = new MoteValue<T>(group.Index);
					groupResult["length"] = new MoteValue<T>(group.Length);
					groups.Add(new MoteValue<T>(groupResult));
				}
				result["groups"] = new MoteValue<T>(groups);
				return new MoteValue<T>(result);
			}
		}

		private sealed class RegexCache
		{
			private readonly RegexOptions _options;
			private readonly TimeSpan _matchTimeout;
			private readonly int _maximumCount;
			private readonly Dictionary<string, CacheEntry> _entries;
			private readonly LinkedList<string> _leastRecentlyUsed;

			internal int Count => _entries.Count;

			internal RegexCache(
				RegexOptions options,
				TimeSpan matchTimeout,
				int maximumCount)
			{
				_options = options;
				_matchTimeout = matchTimeout;
				_maximumCount = maximumCount;
				_entries = new Dictionary<string, CacheEntry>(maximumCount);
				_leastRecentlyUsed = new LinkedList<string>();
			}

			internal Regex Get(string pattern)
			{
				if (_entries.TryGetValue(pattern, out CacheEntry entry))
				{
					_leastRecentlyUsed.Remove(entry.Node);
					_leastRecentlyUsed.AddFirst(entry.Node);
					return entry.Regex;
				}

				var regex = new Regex(pattern, _options, _matchTimeout);
				LinkedListNode<string> node = _leastRecentlyUsed.AddFirst(pattern);
				_entries.Add(pattern, new CacheEntry(regex, node));
				if (_entries.Count > _maximumCount)
				{
					LinkedListNode<string> removed = _leastRecentlyUsed.Last;
					_leastRecentlyUsed.RemoveLast();
					_entries.Remove(removed.Value);
				}
				return regex;
			}

			internal void Clear()
			{
				_entries.Clear();
				_leastRecentlyUsed.Clear();
			}

			private readonly struct CacheEntry
			{
				internal readonly Regex Regex;
				internal readonly LinkedListNode<string> Node;

				internal CacheEntry(Regex regex, LinkedListNode<string> node)
				{
					Regex = regex;
					Node = node;
				}
			}
		}

		private static string GetRequiredString(
			MoteValue<T> parameter,
			IContext<T> context,
			string function)
		{
			MoteValue<T> value = parameter.EvaluateInner(context);
			if (value.ValueType != EValueType.String)
			{
				throw new InvalidOperationException(
					$"{function} requires a String value, but received {value.ValueType}.");
			}
			if (value.StringValue == null)
			{
				throw new ArgumentNullException(function, $"{function} does not accept null.");
			}
			return value.StringValue;
		}

		private static void RequireCount(
			List<MoteValue<T>> values,
			int minimum,
			int maximum,
			string function)
		{
			if (values.Count < minimum || values.Count > maximum)
			{
				throw new ArgumentException(
					$"{function} expects {minimum} arguments, but received {values.Count}.");
			}
		}
	}
}
