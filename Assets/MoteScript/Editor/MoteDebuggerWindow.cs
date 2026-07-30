#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
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
		private Vector2 _callStackScroll;
		[SerializeField] private float _sourcePaneHeight = 280f;
		[SerializeField] private float _variablesPaneHeight = 160f;
		[SerializeField] private TreeViewState _variablesTreeState;
		[SerializeField] private MultiColumnHeaderState _variablesHeaderState;
		private VariableTreeView _variablesTree;
		private GUIStyle _lineStyle;
		private int _selectedLine = -1;
		private int _visibleExecutionLine = -1;
		private int _draggedSplitter;

		private const float SplitterHeight = 5f;
		private const float MinimumPaneHeight = 70f;

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
			window._visibleExecutionLine = -1;
			window.Repaint();
		}

		private void OnEnable()
		{
			MoteDebugEvents.ExecutionCreated += OnExecutionCreated;
			RestoreInvalidPaneHeights();
			EnsureVariableTree();
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
			Rect content = GUILayoutUtility.GetRect(
				0f,
				float.MaxValue,
				0f,
				float.MaxValue,
				GUILayout.ExpandWidth(true),
				GUILayout.ExpandHeight(true));
			DrawPanes(content);
		}

		private void DrawPanes(Rect content)
		{
			RestoreInvalidPaneHeights();
			float available = Mathf.Max(
				0f,
				content.height - SplitterHeight * 2f);

			Rect sourceRect = new(
				content.x,
				content.y,
				content.width,
				_sourcePaneHeight);
			Rect firstSplitter = new(
				content.x,
				sourceRect.yMax,
				content.width,
				SplitterHeight);
			Rect variablesRect = new(
				content.x,
				firstSplitter.yMax,
				content.width,
				_variablesPaneHeight);
			Rect secondSplitter = new(
				content.x,
				variablesRect.yMax,
				content.width,
				SplitterHeight);
			Rect callStackRect = new(
				content.x,
				secondSplitter.yMax,
				content.width,
				Mathf.Max(0f, content.yMax - secondSplitter.yMax));

			DrawSourcePane(sourceRect);
			DrawSplitter(firstSplitter, 1, available);
			DrawVariablesPane(variablesRect);
			DrawSplitter(secondSplitter, 2, available);
			DrawCallStackPane(callStackRect);
		}

		private void RestoreInvalidPaneHeights()
		{
			if (!float.IsFinite(_sourcePaneHeight)
				|| _sourcePaneHeight < MinimumPaneHeight)
			{
				_sourcePaneHeight = 280f;
			}
			if (!float.IsFinite(_variablesPaneHeight)
				|| _variablesPaneHeight < MinimumPaneHeight)
			{
				_variablesPaneHeight = 160f;
			}
		}

		private void ClampPaneHeights(float available)
		{
			if (available < MinimumPaneHeight * 3f)
			{
				float equalHeight = available / 3f;
				_sourcePaneHeight = equalHeight;
				_variablesPaneHeight = equalHeight;
				return;
			}

			_sourcePaneHeight = Mathf.Clamp(
				_sourcePaneHeight,
				MinimumPaneHeight,
				available - MinimumPaneHeight * 2f);
			_variablesPaneHeight = Mathf.Clamp(
				_variablesPaneHeight,
				MinimumPaneHeight,
				available - _sourcePaneHeight - MinimumPaneHeight);
		}

		private void DrawSplitter(Rect rect, int splitter, float available)
		{
			EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
			EditorGUI.DrawRect(
				rect,
				EditorGUIUtility.isProSkin
					? new Color(0.12f, 0.12f, 0.12f)
					: new Color(0.65f, 0.65f, 0.65f));

			Event current = Event.current;
			if (current.type == EventType.MouseDown
				&& current.button == 0
				&& rect.Contains(current.mousePosition))
			{
				_draggedSplitter = splitter;
				current.Use();
			}
			if (current.type == EventType.MouseDrag
				&& _draggedSplitter == splitter)
			{
				if (splitter == 1)
				{
					_sourcePaneHeight += current.delta.y;
					_variablesPaneHeight -= current.delta.y;
				}
				else
				{
					_variablesPaneHeight += current.delta.y;
				}
				ClampPaneHeights(available);
				Repaint();
				current.Use();
			}
			if (current.rawType == EventType.MouseUp
				&& _draggedSplitter == splitter)
			{
				_draggedSplitter = 0;
				current.Use();
			}
		}

		private void DrawSourceSelection()
		{
			DrawMoteAssetField();
			EditorGUI.BeginChangeCheck();
			_numericType = (NumericType)EditorGUILayout.EnumPopup(
				"Numeric Type",
				_numericType);
			if (EditorGUI.EndChangeCheck())
			{
				ResetDebuggingForSourceChange();
			}
		}

		private void DrawMoteAssetField()
		{
			Rect control = EditorGUILayout.GetControlRect();
			Rect field = EditorGUI.PrefixLabel(control, new GUIContent("Script"));
			const float selectorWidth = 24f;
			Rect valueField = new(
				field.x,
				field.y,
				Mathf.Max(0f, field.width - selectorWidth),
				field.height);
			Rect selector = new(
				valueField.xMax,
				field.y,
				selectorWidth,
				field.height);
			string path = _sourceAsset is null
				? string.Empty
				: AssetDatabase.GetAssetPath(_sourceAsset);
			GUIContent content = _sourceAsset is null
				? new GUIContent("None (MoteScript Asset)")
				: new GUIContent(_sourceAsset.name, path);
			bool showMenu = EditorGUI.DropdownButton(
				valueField,
				content,
				FocusType.Keyboard,
				EditorStyles.objectField);
			GUIContent folderIcon = EditorGUIUtility.IconContent("Folder Icon");
			folderIcon.tooltip = "Select a .mote asset";
			if (GUI.Button(
				selector,
				folderIcon,
				EditorStyles.miniButton))
			{
				showMenu = true;
			}
			if (showMenu)
			{
				ShowMoteAssetMenu();
			}
			HandleMoteAssetDrag(field);
		}

		private void ShowMoteAssetMenu()
		{
			var menu = new GenericMenu();
			menu.AddItem(
				new GUIContent("None"),
				_sourceAsset is null,
				() => SetSourceAsset(null));
			menu.AddSeparator(string.Empty);

			string[] assetPaths = AssetDatabase.GetAllAssetPaths();
			var motePaths = new List<string>();
			for (int i = 0; i < assetPaths.Length; ++i)
			{
				if (IsMoteAssetPath(assetPaths[i]))
				{
					motePaths.Add(assetPaths[i]);
				}
			}
			motePaths.Sort(StringComparer.Ordinal);
			for (int i = 0; i < motePaths.Count; ++i)
			{
				string path = motePaths[i];
				TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				if (asset is null)
				{
					continue;
				}
				menu.AddItem(
					new GUIContent(path),
					ReferenceEquals(asset, _sourceAsset),
					() => SetSourceAsset(asset));
			}
			if (motePaths.Count == 0)
			{
				menu.AddDisabledItem(new GUIContent("No .mote assets found"));
			}
			menu.ShowAsContext();
		}

		private void HandleMoteAssetDrag(Rect field)
		{
			Event current = Event.current;
			if (!field.Contains(current.mousePosition)
				|| (current.type != EventType.DragUpdated
					&& current.type != EventType.DragPerform))
			{
				return;
			}
			TextAsset asset = DragAndDrop.objectReferences.Length == 1
				? DragAndDrop.objectReferences[0] as TextAsset
				: null;
			bool valid = asset is not null
				&& IsMoteAssetPath(AssetDatabase.GetAssetPath(asset));
			DragAndDrop.visualMode = valid
				? DragAndDropVisualMode.Copy
				: DragAndDropVisualMode.Rejected;
			if (current.type == EventType.DragPerform)
			{
				if (valid)
				{
					DragAndDrop.AcceptDrag();
					SetSourceAsset(asset);
				}
				else
				{
					ShowNotification(
						new GUIContent("Only .mote assets can be selected."));
				}
			}
			current.Use();
		}

		private void SetSourceAsset(TextAsset asset)
		{
			if (ReferenceEquals(_sourceAsset, asset))
			{
				return;
			}
			_sourceAsset = asset;
			ResetDebuggingForSourceChange();
			Repaint();
		}

		private void ResetDebuggingForSourceChange()
		{
			StopCurrentExecution();
			_execution = null;
			_document = null;
			_selectedLine = -1;
			_visibleExecutionLine = -1;
			_breakpoints.Clear();
		}

		private static bool IsMoteAssetPath(string path)
		{
			return !string.IsNullOrEmpty(path)
				&& path.EndsWith(".mote", StringComparison.OrdinalIgnoreCase);
		}

		private void DrawControls()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				bool stoppable = _execution?.State == MoteExecutionState.Paused
					|| _execution?.State == MoteExecutionState.Yielded;
				EditorGUI.BeginDisabledGroup(_sourceAsset == null && !stoppable);
				if (GUILayout.Button(stoppable ? "Stop" : "Run"))
				{
					if (stoppable)
					{
						Execute(_execution.Stop);
					}
					else
					{
						StartDebugging(startPaused: false);
					}
				}
				EditorGUI.EndDisabledGroup();

				bool paused = _execution?.State == MoteExecutionState.Paused;
				bool yielded = _execution?.State == MoteExecutionState.Yielded;
				bool canStep = paused || yielded;
				bool canStartDebugging = _sourceAsset != null
					&& CanStartNewExecution();
				EditorGUI.BeginDisabledGroup(!paused);
				if (GUILayout.Button("Continue"))
				{
					Execute(_execution.Continue);
				}
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(!canStep && !canStartDebugging);
				if (GUILayout.Button("Step Over"))
				{
					if (canStep)
					{
						Execute(_execution.StepOver);
					}
					else
					{
						StartDebugging(startPaused: true);
					}
				}
				if (GUILayout.Button("Step Into"))
				{
					if (canStep)
					{
						Execute(_execution.StepInto);
					}
					else
					{
						StartDebugging(startPaused: true);
					}
				}
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(!canStep);
				if (GUILayout.Button("Step Out"))
				{
					Execute(_execution.StepOut);
				}
				EditorGUI.EndDisabledGroup();
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(
					"State",
					GUILayout.Width(42f));
				EditorGUILayout.LabelField(
					_execution?.State.ToString() ?? "Not started",
					EditorStyles.textField,
					GUILayout.Height(EditorGUIUtility.singleLineHeight),
					GUILayout.MinWidth(90f));
				EditorGUILayout.LabelField(
					"Result",
					GUILayout.Width(44f));
				EditorGUILayout.SelectableLabel(
					_execution?.ResultText ?? string.Empty,
					EditorStyles.textField,
					GUILayout.Height(EditorGUIUtility.singleLineHeight),
					GUILayout.ExpandWidth(true));
			}
			string locationText =
				_execution?.CurrentLocation is MoteSourceSpan location
					? $"{location.SourceName}:{location.StartLine}"
					: string.Empty;
			EditorGUILayout.LabelField("Location", locationText);
		}

		private bool CanStartNewExecution()
		{
			return _execution is null
				|| _execution.State == MoteExecutionState.Completed
				|| _execution.State == MoteExecutionState.Faulted
				|| _execution.State == MoteExecutionState.Stopped;
		}

		private void DrawSourcePane(Rect rect)
		{
			GUI.BeginGroup(rect);
			float titleHeight = EditorGUIUtility.singleLineHeight + 2f;
			GUI.Label(
				new Rect(0f, 0f, rect.width, titleHeight),
				"Code",
				EditorStyles.boldLabel);
			string source = _document?.Source ?? _sourceAsset?.text;
			if (string.IsNullOrEmpty(source))
			{
				EditorGUI.HelpBox(
					new Rect(
						0f,
						titleHeight,
						rect.width,
						EditorGUIUtility.singleLineHeight * 2f),
					"Select a TextAsset containing a MoteScript source.",
					MessageType.Info);
				GUI.EndGroup();
				return;
			}

			string[] lines = source.Replace("\r\n", "\n").Split('\n');
			int currentLine =
				_execution?.CurrentLocation?.StartLine ?? _selectedLine;
			float rowHeight = EditorGUIUtility.singleLineHeight + 2f;
			Rect viewport = new(
				0f,
				titleHeight,
				rect.width,
				Mathf.Max(0f, rect.height - titleHeight));
			if (_execution?.CurrentLocation is MoteSourceSpan
				&& currentLine != _visibleExecutionLine)
			{
				_sourceScroll.y = Mathf.Max(
					0f,
					(currentLine - 1) * rowHeight - viewport.height * 0.5f);
				_visibleExecutionLine = currentLine;
			}

			float codeWidth = 0f;
			for (int index = 0; index < lines.Length; ++index)
			{
				codeWidth = Mathf.Max(
					codeWidth,
					_lineStyle.CalcSize(new GUIContent(lines[index])).x);
			}
			Rect content = new(
				0f,
				0f,
				Mathf.Max(viewport.width - 16f, 72f + codeWidth),
				Mathf.Max(viewport.height, lines.Length * rowHeight));
			_sourceScroll = GUI.BeginScrollView(
				viewport,
				_sourceScroll,
				content);
			for (int index = 0; index < lines.Length; ++index)
			{
				int line = index + 1;
				float y = index * rowHeight;
				Rect row = new(0f, y, content.width, rowHeight);
				if (line == currentLine)
				{
					EditorGUI.DrawRect(
						row,
						EditorGUIUtility.isProSkin
							? new Color(0.25f, 0.35f, 0.5f, 0.7f)
							: new Color(0.65f, 0.8f, 1f, 0.8f));
				}
				bool hasBreakpoint = HasBreakpoint(line);
				if (GUI.Button(
					new Rect(0f, y, 24f, rowHeight),
					hasBreakpoint ? "●" : "○"))
				{
					ToggleBreakpoint(line);
				}
				GUI.Label(
					new Rect(28f, y, 38f, rowHeight),
					line.ToString(),
					_lineStyle);
				GUI.Label(
					new Rect(68f, y, codeWidth + 4f, rowHeight),
					lines[index],
					_lineStyle);
			}
			GUI.EndScrollView();
			GUI.EndGroup();
		}

		private void DrawVariablesPane(Rect rect)
		{
			GUI.BeginGroup(rect);
			GUI.Label(
				new Rect(0f, 0f, rect.width, EditorGUIUtility.singleLineHeight),
				"Context Variables",
				EditorStyles.boldLabel);
			EnsureVariableTree();
			IReadOnlyList<MoteDebugVariable> variables =
				_execution?.GetVariables() ?? Array.Empty<MoteDebugVariable>();
			_variablesTree.SetVariables(variables);
			float top = EditorGUIUtility.singleLineHeight + 2f;
			_variablesTree.OnGUI(
				new Rect(0f, top, rect.width, Mathf.Max(0f, rect.height - top)));
			GUI.EndGroup();
		}

		private void DrawCallStackPane(Rect rect)
		{
			GUI.BeginGroup(rect);
			Rect local = new(0f, 0f, rect.width, rect.height);
			GUILayout.BeginArea(local);
			EditorGUILayout.LabelField("Call Stack", EditorStyles.boldLabel);
			if (_execution is null)
			{
				GUILayout.EndArea();
				GUI.EndGroup();
				return;
			}

			IReadOnlyList<MoteDebugCallFrame> frames =
				_execution.GetCallStack();
			_callStackScroll = EditorGUILayout.BeginScrollView(
				_callStackScroll,
				GUILayout.ExpandHeight(true));
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
			EditorGUILayout.EndScrollView();
			GUILayout.EndArea();
			GUI.EndGroup();
		}

		private void EnsureVariableTree()
		{
			if (_variablesTree is not null)
			{
				return;
			}
			_variablesTreeState ??= new TreeViewState();
			MultiColumnHeaderState headerState =
				VariableTreeView.CreateHeaderState();
			if (_variablesHeaderState is not null
				&& MultiColumnHeaderState.CanOverwriteSerializedFields(
					_variablesHeaderState,
					headerState))
			{
				MultiColumnHeaderState.OverwriteSerializedFields(
					_variablesHeaderState,
					headerState);
			}
			_variablesHeaderState = headerState;
			_variablesTree ??= new VariableTreeView(
				_variablesTreeState,
				new MultiColumnHeader(headerState));
		}

		private void StartDebugging(bool startPaused)
		{
			ClearValueSelection();
			try
			{
				switch (_numericType)
				{
					case NumericType.Integer:
						StartDebugging<int>(startPaused);
						break;
					case NumericType.Decimal:
						StartDebugging<decimal>(startPaused);
						break;
					default:
						StartDebugging<float>(startPaused);
						break;
				}
			}
			catch (Exception exception)
			{
				LogException(exception, null);
			}
		}

		private void StartDebugging<T>(bool startPaused)
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
			_visibleExecutionLine = -1;
			NormalizeBreakpoints();

			if (startPaused)
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
			ClearValueSelection();
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

		private static void ClearValueSelection()
		{
			GUI.FocusControl(null);
			GUIUtility.keyboardControl = 0;
			EditorGUIUtility.editingTextField = false;
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
			_visibleExecutionLine = -1;
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
			if (_lineStyle is not null)
			{
				return;
			}

			Color textColor = EditorGUIUtility.isProSkin
				? new Color(0.86f, 0.86f, 0.86f)
				: new Color(0.12f, 0.12f, 0.12f);
			_lineStyle = new GUIStyle(EditorStyles.label)
			{
				font = EditorStyles.miniFont,
				wordWrap = false,
			};
			SetTextColor(_lineStyle, textColor);
		}

		private static void SetTextColor(GUIStyle style, Color color)
		{
			style.normal.textColor = color;
			style.hover.textColor = color;
			style.active.textColor = color;
			style.focused.textColor = color;
			style.onNormal.textColor = color;
			style.onHover.textColor = color;
			style.onActive.textColor = color;
			style.onFocused.textColor = color;
		}

		private sealed class VariableTreeView : TreeView
		{
			private IReadOnlyList<MoteDebugVariable> _variables =
				Array.Empty<MoteDebugVariable>();

			private enum Column
			{
				Name,
				Type,
				Value,
			}

			public VariableTreeView(
				TreeViewState state,
				MultiColumnHeader header)
				: base(state, header)
			{
				showAlternatingRowBackgrounds = true;
				showBorder = true;
				rowHeight = EditorGUIUtility.singleLineHeight + 2f;
				header.sortingChanged += _ => Reload();
				Reload();
			}

			public static MultiColumnHeaderState CreateHeaderState()
			{
				return new MultiColumnHeaderState(new[]
				{
					new MultiColumnHeaderState.Column
					{
						headerContent = new GUIContent("Name"),
						width = 160f,
						minWidth = 70f,
						autoResize = false,
						canSort = true,
					},
					new MultiColumnHeaderState.Column
					{
						headerContent = new GUIContent("Type"),
						width = 100f,
						minWidth = 60f,
						autoResize = false,
						canSort = true,
					},
					new MultiColumnHeaderState.Column
					{
						headerContent = new GUIContent("Value"),
						width = 240f,
						minWidth = 80f,
						autoResize = false,
						canSort = true,
					},
				});
			}

			public void SetVariables(IReadOnlyList<MoteDebugVariable> variables)
			{
				variables ??= Array.Empty<MoteDebugVariable>();
				if (AreEqual(_variables, variables))
				{
					return;
				}
				_variables = variables;
				Reload();
			}

			private static bool AreEqual(
				IReadOnlyList<MoteDebugVariable> left,
				IReadOnlyList<MoteDebugVariable> right)
			{
				if (left.Count != right.Count)
				{
					return false;
				}
				for (int i = 0; i < left.Count; ++i)
				{
					if (!string.Equals(left[i].Name, right[i].Name, StringComparison.Ordinal)
						|| !string.Equals(left[i].Type, right[i].Type, StringComparison.Ordinal)
						|| !string.Equals(left[i].Value, right[i].Value, StringComparison.Ordinal)
						|| !AreEqual(
							left[i].Children ?? Array.Empty<MoteDebugVariable>(),
							right[i].Children ?? Array.Empty<MoteDebugVariable>()))
					{
						return false;
					}
				}
				return true;
			}

			protected override TreeViewItem BuildRoot()
			{
				var root = new TreeViewItem
				{
					id = 0,
					depth = -1,
					displayName = "Root",
				};
				var rows = new List<TreeViewItem>();
				int id = 1;
				AddRows(_variables, 0, rows, ref id);
				SetupParentsAndChildrenFromDepths(root, rows);
				return root;
			}

			protected override void RowGUI(RowGUIArgs args)
			{
				base.RowGUI(args);
				var item = (VariableTreeViewItem)args.item;
				for (int visibleIndex = 0;
					visibleIndex < args.GetNumVisibleColumns();
					++visibleIndex)
				{
					int columnIndex = args.GetColumn(visibleIndex);
					if ((Column)columnIndex == Column.Name)
					{
						continue;
					}
					Rect cell = args.GetCellRect(visibleIndex);
					CenterRectUsingSingleLineHeight(ref cell);
					string value = (Column)columnIndex switch
					{
						Column.Type => item.Variable.Type,
						_ => item.Variable.Value,
					};
					EditorGUI.LabelField(cell, value ?? string.Empty);
				}
			}

			private void AddRows(
				IReadOnlyList<MoteDebugVariable> variables,
				int depth,
				List<TreeViewItem> rows,
				ref int id)
			{
				var sorted = new List<MoteDebugVariable>(variables.Count);
				for (int i = 0; i < variables.Count; ++i)
				{
					sorted.Add(variables[i]);
				}
				SortVariables(sorted);
				for (int i = 0; i < sorted.Count; ++i)
				{
					MoteDebugVariable variable = sorted[i];
					rows.Add(new VariableTreeViewItem(id++, depth, variable));
					if (variable.Children is { Count: > 0 })
					{
						AddRows(variable.Children, depth + 1, rows, ref id);
					}
				}
			}

			private void SortVariables(List<MoteDebugVariable> variables)
			{
				if (multiColumnHeader.sortedColumnIndex < 0)
				{
					return;
				}
				int column = multiColumnHeader.sortedColumnIndex;
				bool ascending =
					multiColumnHeader.IsSortedAscending(column);
				variables.Sort((left, right) =>
				{
					int comparison = Compare(left, right, (Column)column);
					return ascending ? comparison : -comparison;
				});
			}

			private static int Compare(
				MoteDebugVariable left,
				MoteDebugVariable right,
				Column column)
			{
				if (column == Column.Value
					&& decimal.TryParse(
						left.Value,
						NumberStyles.Number,
						CultureInfo.InvariantCulture,
						out decimal leftNumber)
					&& decimal.TryParse(
						right.Value,
						NumberStyles.Number,
						CultureInfo.InvariantCulture,
						out decimal rightNumber))
				{
					return leftNumber.CompareTo(rightNumber);
				}
				string leftText = column switch
				{
					Column.Name => left.Name,
					Column.Type => left.Type,
					_ => left.Value,
				};
				string rightText = column switch
				{
					Column.Name => right.Name,
					Column.Type => right.Type,
					_ => right.Value,
				};
				return string.CompareOrdinal(leftText, rightText);
			}

			private sealed class VariableTreeViewItem : TreeViewItem
			{
				public VariableTreeViewItem(
					int id,
					int depth,
					MoteDebugVariable variable)
					: base(id, depth, variable.Name)
				{
					Variable = variable;
				}

				public MoteDebugVariable Variable { get; }
			}
		}
	}
}
#endif
