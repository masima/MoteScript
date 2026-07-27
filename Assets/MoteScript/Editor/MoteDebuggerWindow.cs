#if MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MoteScript.Editor
{
	public sealed class MoteDebuggerWindow : EditorWindow
	{
		private enum NumericType
		{
			Float,
			Integer,
			Decimal,
		}

		private TextAsset _sourceAsset;
		private NumericType _numericType;
		private IMoteDebugExecution _execution;
		private MoteDebugDocument _document;
		private readonly HashSet<int> _breakpoints = new();
		private Vector2 _sourceScroll;
		private Vector2 _variablesScroll;
		private GUIStyle _lineStyle;
		private GUIStyle _currentLineStyle;
		private int _selectedLine = -1;

		[MenuItem("Window/MoteScript/Debugger")]
		public static void Open()
		{
			GetWindow<MoteDebuggerWindow>("MoteScript Debugger");
		}

		internal static void OpenAsset(TextAsset asset, int line)
		{
			MoteDebuggerWindow window =
				GetWindow<MoteDebuggerWindow>("MoteScript Debugger");
			window._sourceAsset = asset;
			window._execution = null;
			window._document = null;
			window._selectedLine = Math.Max(1, line);
			window.Repaint();
		}

		private void OnEnable()
		{
			MoteDebugEvents.ExecutionCreated += OnExecutionCreated;
		}

		private void OnDisable()
		{
			MoteDebugEvents.ExecutionCreated -= OnExecutionCreated;
		}

		private void OnGUI()
		{
			EnsureStyles();
			DrawSourceSelection();
			DrawControls();
			EditorGUILayout.Space();
			DrawSource();
			EditorGUILayout.Space();
			DrawVariables();
			EditorGUILayout.Space();
			DrawCallStack();
		}

		private void DrawSourceSelection()
		{
			EditorGUI.BeginChangeCheck();
			_sourceAsset = (TextAsset)EditorGUILayout.ObjectField(
				"Script",
				_sourceAsset,
				typeof(TextAsset),
				false);
			_numericType = (NumericType)EditorGUILayout.EnumPopup(
				"Numeric Type",
				_numericType);
			if (EditorGUI.EndChangeCheck())
			{
				StopCurrentExecution();
				_execution = null;
				_document = null;
				_selectedLine = -1;
				_breakpoints.Clear();
			}
		}

		private void DrawControls()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUI.BeginDisabledGroup(_sourceAsset == null);
				if (GUILayout.Button("Run"))
				{
					StartDebugging(stepInto: false);
				}
				if (GUILayout.Button("Start && Step Into"))
				{
					StartDebugging(stepInto: true);
				}
				EditorGUI.EndDisabledGroup();

				bool paused = _execution?.State == MoteExecutionState.Paused;
				EditorGUI.BeginDisabledGroup(!paused);
				if (GUILayout.Button("Continue"))
				{
					Execute(_execution.Continue);
				}
				if (GUILayout.Button("Step Into"))
				{
					Execute(_execution.StepInto);
				}
				if (GUILayout.Button("Step Over"))
				{
					Execute(_execution.StepOver);
				}
				if (GUILayout.Button("Step Out"))
				{
					Execute(_execution.StepOut);
				}
				EditorGUI.EndDisabledGroup();

				bool stoppable = _execution?.State == MoteExecutionState.Paused
					|| _execution?.State == MoteExecutionState.Yielded;
				EditorGUI.BeginDisabledGroup(!stoppable);
				if (GUILayout.Button("Stop"))
				{
					Execute(_execution.Stop);
				}
				EditorGUI.EndDisabledGroup();
			}

			EditorGUILayout.LabelField(
				"State",
				_execution?.State.ToString() ?? "Not started");
			if (_execution?.CurrentLocation is MoteSourceSpan location)
			{
				EditorGUILayout.LabelField(
					"Location",
					$"{location.SourceName}:{location.StartLine}");
			}
		}

		private void DrawSource()
		{
			string source = _document?.Source ?? _sourceAsset?.text;
			if (string.IsNullOrEmpty(source))
			{
				EditorGUILayout.HelpBox(
					"Select a TextAsset containing a MoteScript source.",
					MessageType.Info);
				return;
			}

			string[] lines = source.Replace("\r\n", "\n").Split('\n');
			int currentLine =
				_execution?.CurrentLocation?.StartLine ?? _selectedLine;
			_sourceScroll = EditorGUILayout.BeginScrollView(
				_sourceScroll,
				GUILayout.MinHeight(220));
			for (int index = 0; index < lines.Length; ++index)
			{
				int line = index + 1;
				using (new EditorGUILayout.HorizontalScope())
				{
					bool hasBreakpoint = HasBreakpoint(line);
					if (GUILayout.Button(
						hasBreakpoint ? "●" : "○",
						GUILayout.Width(24)))
					{
						ToggleBreakpoint(line);
					}
					GUILayout.Label(
						line.ToString(),
						GUILayout.Width(38));
					GUILayout.Label(
						lines[index],
						line == currentLine
							? _currentLineStyle
							: _lineStyle);
				}
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawVariables()
		{
			EditorGUILayout.LabelField("Context Variables", EditorStyles.boldLabel);
			if (_execution is null)
			{
				return;
			}

			IReadOnlyList<MoteDebugVariable> variables =
				_execution.GetVariables();
			_variablesScroll = EditorGUILayout.BeginScrollView(
				_variablesScroll,
				GUILayout.MinHeight(100));
			for (int i = 0; i < variables.Count; ++i)
			{
				MoteDebugVariable variable = variables[i];
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField(
						variable.Name,
						GUILayout.Width(140));
					EditorGUILayout.LabelField(
						variable.Type,
						GUILayout.Width(90));
					EditorGUILayout.SelectableLabel(
						variable.Value,
						GUILayout.Height(EditorGUIUtility.singleLineHeight));
				}
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawCallStack()
		{
			EditorGUILayout.LabelField("Call Stack", EditorStyles.boldLabel);
			if (_execution is null)
			{
				return;
			}

			IReadOnlyList<MoteDebugCallFrame> frames =
				_execution.GetCallStack();
			for (int i = frames.Count - 1; i >= 0; --i)
			{
				MoteDebugCallFrame frame = frames[i];
				string location = frame.CallSite is MoteSourceSpan callSite
					? $"{callSite.SourceName}:{callSite.StartLine}"
					: string.Empty;
				EditorGUILayout.LabelField(
					frame.FunctionName,
					location);
			}
		}

		private void StartDebugging(bool stepInto)
		{
			try
			{
				switch (_numericType)
				{
					case NumericType.Integer:
						StartDebugging<int>(stepInto);
						break;
					case NumericType.Decimal:
						StartDebugging<decimal>(stepInto);
						break;
					default:
						StartDebugging<float>(stepInto);
						break;
				}
			}
			catch (Exception exception)
			{
				LogException(exception, null);
			}
		}

		private void StartDebugging<T>(bool stepInto)
			where T : struct, IComparable, IFormattable, IConvertible, IEquatable<T>, IComparable<T>
		{
			MoteDecoder<T>.Setup();
			var decoder = new MoteDecoder<T>();
			string path = AssetDatabase.GetAssetPath(_sourceAsset);
			MoteValue<T> script = decoder.DecodeDebug(
				_sourceAsset.text,
				path,
				out MoteDebugDocument document);
			var session = new MoteDebugSession<T>(document);
			foreach (int breakpoint in _breakpoints)
			{
				session.SetBreakpoint(breakpoint);
			}
			session.Paused += (_, _) => Repaint();
			session.ExceptionThrown += (_, location, exception) =>
				LogException(exception, location);

			var context = new Context<T>();
			StandardMathLibrary<T>.Register(context);
			StandardStringLibrary<T>.Register(context);
			StandardRegexLibrary<T>.Register(context);
			MoteExecution<T> execution =
				script.CreateDebugExecution(context, session);
			_execution = execution;
			_document = document;
			_selectedLine = -1;
			NormalizeBreakpoints();

			if (stepInto)
			{
				execution.StepInto();
			}
			else
			{
				execution.Evaluate();
			}
			Repaint();
		}

		private void Execute(Action operation)
		{
			try
			{
				operation();
			}
			catch (Exception exception)
			{
				LogException(exception, _execution?.CurrentLocation);
			}
			Repaint();
		}

		private bool HasBreakpoint(int line)
		{
			if (_execution is not null)
			{
				return _execution.HasBreakpoint(line);
			}
			int normalized = _document?.NormalizeBreakpointLine(line) ?? line;
			return _breakpoints.Contains(normalized);
		}

		private void ToggleBreakpoint(int line)
		{
			int normalized = _document?.NormalizeBreakpointLine(line) ?? line;
			if (!_breakpoints.Add(normalized))
			{
				_breakpoints.Remove(normalized);
				_execution?.RemoveBreakpoint(normalized);
			}
			else
			{
				_execution?.SetBreakpoint(normalized);
			}
			Repaint();
		}

		private void StopCurrentExecution()
		{
			if (_execution?.State == MoteExecutionState.Paused
				|| _execution?.State == MoteExecutionState.Yielded)
			{
				_execution.Stop();
			}
		}

		private void NormalizeBreakpoints()
		{
			if (_document is null)
			{
				return;
			}
			var normalized = new HashSet<int>();
			foreach (int line in _breakpoints)
			{
				normalized.Add(_document.NormalizeBreakpointLine(line));
			}
			_breakpoints.Clear();
			foreach (int line in normalized)
			{
				_breakpoints.Add(line);
			}
		}

		private void OnExecutionCreated(IMoteDebugExecution execution)
		{
			_execution = execution;
			_document = execution.DebugDocument;
			Repaint();
		}

		private static void LogException(
			Exception exception,
			MoteSourceSpan? location)
		{
			if (location is MoteSourceSpan source)
			{
				Debug.LogError(
					$"{exception.GetType().Name}: {exception.Message}\n"
					+ $"(at {source.SourceName}:{source.StartLine})");
				return;
			}
			Debug.LogException(exception);
		}

		private void EnsureStyles()
		{
			_lineStyle ??= new GUIStyle(EditorStyles.label)
			{
				font = EditorStyles.miniFont,
				wordWrap = false,
			};
			_currentLineStyle ??= new GUIStyle(_lineStyle);
			_currentLineStyle.normal.background =
				EditorGUIUtility.isProSkin
					? MakeTexture(new Color(0.25f, 0.35f, 0.5f, 0.7f))
					: MakeTexture(new Color(0.65f, 0.8f, 1f, 0.8f));
		}

		private static Texture2D MakeTexture(Color color)
		{
			var texture = new Texture2D(1, 1)
			{
				hideFlags = HideFlags.HideAndDontSave,
			};
			texture.SetPixel(0, 0, color);
			texture.Apply();
			return texture;
		}
	}
}
#endif
