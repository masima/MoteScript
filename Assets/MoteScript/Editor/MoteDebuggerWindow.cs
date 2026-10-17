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
			Double,
		}

		private enum WindowMode
		{
			Script,
			Immediate,
		}

		private TextAsset _sourceAsset;
		[SerializeField] private string _sourceAssetPath;
		private NumericType _numericType;
		private IMoteDebugExecution _execution;
		private IMoteDebugTarget _selectedTarget;
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
		private GUIStyle _executionMarkerStyle;
		private AdvancedDropdownState _moteAssetDropdownState;
		private MoteAssetDropdown _moteAssetDropdown;
		private int _selectedLine = -1;
		private int _visibleExecutionLine = -1;
		private int _draggedSplitter;
		private bool _sourceChanged;
		[SerializeField] private WindowMode _windowMode;
		[SerializeField] private string _immediateInput = string.Empty;
		private readonly List<MoteImmediateResult> _immediateResults = new();
		private readonly List<string> _immediateHistory = new();
		private IMoteImmediateSession _immediateSession;
		private NumericType _immediateSessionNumericType;
		private Vector2 _immediateScroll;
		private int _immediateHistoryIndex;
		private bool _focusImmediateInput;

		private const float SplitterHeight = 5f;
		private const float MinimumPaneHeight = 70f;
		private const string BreakpointSessionKeyPrefix =
			"MoteScript.Debugger.Breakpoints.";
		private const string RecentMoteAssetsSessionKey =
			"MoteScript.Debugger.RecentAssets";
		private const int MaximumRecentMoteAssets = 8;
		private const int MaximumImmediateResults = 200;
		private const string ImmediateInputControl =
			"MoteScript.Immediate.Input";
		private static string[] s_cachedMoteAssetPaths;

		[MenuItem("Window/MoteScript/Debugger")]
		public static void Open()
		{
			GetWindow<MoteDebuggerWindow>("MoteScript Debugger");
		}

		internal static MoteDebuggerWindow FindOpenWindow()
		{
			MoteDebuggerWindow[] windows =
				Resources.FindObjectsOfTypeAll<MoteDebuggerWindow>();
			return windows.Length == 0 ? null : windows[0];
		}

		internal static void OpenTarget(IMoteDebugTarget target)
		{
			MoteDebuggerWindow window =
				GetWindow<MoteDebuggerWindow>("MoteScript Debugger");
			window.SelectTarget(target);
			window.Show();
			window.Focus();
		}

		internal bool SelectedTargetIsFaulted =>
			_selectedTarget?.Execution?.State == MoteExecutionState.Faulted;

		internal static void OpenAsset(TextAsset asset, int line)
		{
			MoteDebuggerWindow window =
				GetWindow<MoteDebuggerWindow>("MoteScript Debugger");
			window.SetSourceAsset(asset);
			window._sourceChanged = false;
			window._selectedLine = Math.Max(1, line);
			window._visibleExecutionLine = -1;
			window.Repaint();
		}

		private void OnEnable()
		{
			SanitizeDestroyedReferences();
			MoteDebugEvents.ExecutionCreated += OnExecutionCreated;
			MoteDebugTargetRegistry.TargetRegistered += OnTargetRegistered;
			MoteDebugTargetRegistry.TargetUnregistered += OnTargetUnregistered;
			MoteDebugTargetRegistry.TargetChanged += OnTargetChanged;
			MoteScriptAssetChangeEvents.Changed += OnMoteAssetChanged;
			if (string.IsNullOrEmpty(_sourceAssetPath)
				&& _sourceAsset != null)
			{
				_sourceAssetPath = AssetDatabase.GetAssetPath(_sourceAsset);
			}
			LoadBreakpoints();
			RestoreInvalidPaneHeights();
			EnsureVariableTree();
			_moteAssetDropdownState ??= new AdvancedDropdownState();
		}

		private void OnDisable()
		{
			MoteDebugEvents.ExecutionCreated -= OnExecutionCreated;
			MoteDebugTargetRegistry.TargetRegistered -= OnTargetRegistered;
			MoteDebugTargetRegistry.TargetUnregistered -= OnTargetUnregistered;
			MoteDebugTargetRegistry.TargetChanged -= OnTargetChanged;
			MoteScriptAssetChangeEvents.Changed -= OnMoteAssetChanged;
		}

		private void OnGUI()
		{
			SanitizeDestroyedReferences();
			EnsureStyles();
			DrawModeSelection();
			if (_windowMode == WindowMode.Immediate)
			{
				DrawImmediateMode();
				return;
			}
			HandleKeyboardShortcuts();
			DrawTargetSelection();
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

		private void DrawModeSelection()
		{
			WindowMode next = (WindowMode)GUILayout.Toolbar(
				(int)_windowMode,
				new[] { "Script", "Immediate" });
			if (next == _windowMode) return;
			_windowMode = next;
			_focusImmediateInput = next == WindowMode.Immediate;
			GUI.FocusControl(null);
		}

		private void DrawImmediateMode()
		{
			EnsureImmediateSession();
			EditorGUI.BeginChangeCheck();
			NumericType nextNumericType = (NumericType)EditorGUILayout.EnumPopup(
				"Numeric Type",
				_numericType);
			if (EditorGUI.EndChangeCheck())
			{
				_numericType = nextNumericType;
				ResetImmediateContext(addNotice: true);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Clear", GUILayout.Width(70f)))
				{
					_immediateResults.Clear();
				}
				if (GUILayout.Button("Reset Context", GUILayout.Width(110f)))
				{
					ResetImmediateContext(addNotice: true);
				}
			}

			_immediateScroll = EditorGUILayout.BeginScrollView(
				_immediateScroll,
				GUILayout.ExpandHeight(true));
			for (int i = 0; i < _immediateResults.Count; ++i)
			{
				MoteImmediateResult result = _immediateResults[i];
				if (!string.IsNullOrEmpty(result.Input))
				{
					EditorGUILayout.SelectableLabel(
						$"> {result.Input}",
						GUILayout.Height(EditorGUIUtility.singleLineHeight));
				}
				Color previous = GUI.contentColor;
				if (result.IsError) GUI.contentColor = new Color(1f, 0.45f, 0.4f);
				EditorGUILayout.SelectableLabel(
					result.Output,
					EditorStyles.wordWrappedLabel,
					GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
				GUI.contentColor = previous;
			}
			EditorGUILayout.EndScrollView();
			Rect variablesRect = GUILayoutUtility.GetRect(
				0f,
				float.MaxValue,
				MinimumPaneHeight,
				Mathf.Max(MinimumPaneHeight, _variablesPaneHeight),
				GUILayout.ExpandWidth(true),
				GUILayout.Height(Mathf.Max(
					MinimumPaneHeight,
					_variablesPaneHeight)));
			DrawVariablesPane(
				variablesRect,
				_immediateSession.GetVariables());

			bool immediateInputWasFocused =
				GUI.GetNameOfFocusedControl() == ImmediateInputControl;
			Event immediateEvent = Event.current;
			bool immediateKeyDown = immediateInputWasFocused
				&& immediateEvent.type == EventType.KeyDown;
			KeyCode immediateKeyCode = immediateKeyDown
				? immediateEvent.keyCode
				: KeyCode.None;
			bool immediateIsComposing =
				!string.IsNullOrEmpty(Input.compositionString);
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Label(">", GUILayout.Width(14f));
				GUI.SetNextControlName(ImmediateInputControl);
				_immediateInput = EditorGUILayout.TextField(_immediateInput);
			}
			HandleImmediateInput(
				immediateKeyCode,
				immediateIsComposing);
			if (_focusImmediateInput
				&& Event.current.type == EventType.Repaint)
			{
				EditorGUI.FocusTextInControl(ImmediateInputControl);
				EditorGUIUtility.editingTextField = true;
				_focusImmediateInput = false;
			}
		}

		private void HandleImmediateInput(
			KeyCode keyCode,
			bool isComposing)
		{
			Event current = Event.current;
			if (keyCode == KeyCode.UpArrow)
			{
				RecallImmediateHistory(-1);
				UseCurrentEventIfAvailable(current);
				return;
			}
			if (keyCode == KeyCode.DownArrow)
			{
				RecallImmediateHistory(1);
				UseCurrentEventIfAvailable(current);
				return;
			}
			if ((keyCode != KeyCode.Return
					&& keyCode != KeyCode.KeypadEnter)
				|| isComposing)
			{
				return;
			}
			UseCurrentEventIfAvailable(current);
			ExecuteImmediateInput();
		}

		private static void UseCurrentEventIfAvailable(Event current)
		{
			if (current.type != EventType.Used)
			{
				current.Use();
			}
		}

		private void ExecuteImmediateInput()
		{
			string input = _immediateInput.Trim();
			if (input.Length == 0) return;
			EnsureImmediateSession();
			MoteImmediateResult result = _immediateSession.Execute(input);
			_immediateResults.Add(result);
			if (_immediateResults.Count > MaximumImmediateResults)
			{
				_immediateResults.RemoveAt(0);
			}
			if (_immediateHistory.Count == 0
				|| !string.Equals(
					_immediateHistory[^1],
					input,
					StringComparison.Ordinal))
			{
				_immediateHistory.Add(input);
			}
			_immediateHistoryIndex = _immediateHistory.Count;
			_immediateInput = string.Empty;
			_immediateScroll.y = float.MaxValue;
			_focusImmediateInput = true;
			Repaint();
		}

		private void RecallImmediateHistory(int direction)
		{
			if (_immediateHistory.Count == 0) return;
			_immediateHistoryIndex = Mathf.Clamp(
				_immediateHistoryIndex + direction,
				0,
				_immediateHistory.Count);
			_immediateInput = _immediateHistoryIndex == _immediateHistory.Count
				? string.Empty
				: _immediateHistory[_immediateHistoryIndex];
			_focusImmediateInput = true;
			Repaint();
		}

		private void EnsureImmediateSession()
		{
			if (_immediateSession is not null
				&& _immediateSessionNumericType == _numericType)
			{
				return;
			}
			_immediateSession = _numericType switch
			{
				NumericType.Double => new MoteImmediateSession<double>(),
				NumericType.Integer => new MoteImmediateSession<int>(),
				NumericType.Decimal => new MoteImmediateSession<decimal>(),
				_ => new MoteImmediateSession<float>(),
			};
			_immediateSessionNumericType = _numericType;
			_immediateHistoryIndex = _immediateHistory.Count;
		}

		private void ResetImmediateContext(bool addNotice)
		{
			_immediateSession = null;
			EnsureImmediateSession();
			if (addNotice)
			{
				_immediateResults.Add(new MoteImmediateResult(
					string.Empty,
					$"Context reset ({_numericType}).",
					isError: false));
				_immediateScroll.y = float.MaxValue;
			}
			_focusImmediateInput = true;
		}

		private void HandleKeyboardShortcuts()
		{
			Event current = Event.current;
			if (current.type != EventType.KeyDown
				|| EditorGUIUtility.editingTextField
				|| current.alt
				|| current.control
				|| current.command)
			{
				return;
			}

			bool paused = _execution?.State == MoteExecutionState.Paused;
			bool yielded = _execution?.State == MoteExecutionState.Yielded;
			bool canStep = paused || yielded;
			bool canStart = _selectedTarget is null
				&& _sourceAsset != null
				&& CanStartNewExecution();
			switch (current.keyCode)
			{
				case KeyCode.F5 when current.shift:
					if (canStep)
					{
						current.Use();
						Execute(_execution.Stop);
					}
					break;
				case KeyCode.F5:
					if (canStep)
					{
						current.Use();
						Execute(_execution.Continue);
					}
					else if (canStart)
					{
						current.Use();
						StartDebugging(startPaused: false);
					}
					break;
				case KeyCode.F10:
					if (canStep)
					{
						current.Use();
						Execute(_execution.StepOver);
					}
					else if (canStart)
					{
						current.Use();
						StartDebugging(startPaused: true);
					}
					break;
				case KeyCode.F11 when current.shift:
					if (canStep)
					{
						current.Use();
						Execute(_execution.StepOut);
					}
					break;
				case KeyCode.F11:
					if (canStep)
					{
						current.Use();
						Execute(_execution.StepInto);
					}
					else if (canStart)
					{
						current.Use();
						StartDebugging(startPaused: true);
					}
					break;
			}
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

		private void DrawTargetSelection()
		{
			IReadOnlyList<IMoteDebugTarget> targets =
				MoteDebugTargetRegistry.Targets;
			var labels = new string[targets.Count + 1];
			labels[0] = "Standalone script";
			int selected = 0;
			for (int i = 0; i < targets.Count; ++i)
			{
				IMoteDebugTarget target = targets[i];
				string state = target.Execution?.State.ToString()
					?? target.LifecycleState.ToString();
				labels[i + 1] = $"{target.DisplayName} ({state})";
				if (ReferenceEquals(target, _selectedTarget)) selected = i + 1;
			}
			int next = EditorGUILayout.Popup("Target", selected, labels);
			if (next == selected) return;
			if (next == 0)
			{
				_selectedTarget = null;
				_execution = null;
				_document = null;
				Repaint();
				return;
			}
			SelectTarget(targets[next - 1]);
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
			string path = _sourceAsset == null
				? string.Empty
				: AssetDatabase.GetAssetPath(_sourceAsset);
			GUIContent content = _sourceAsset == null
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
				ShowMoteAssetDropdown(field);
			}
			HandleMoteAssetDrag(field);
		}

		private void ShowMoteAssetDropdown(Rect field)
		{
			_moteAssetDropdownState ??= new AdvancedDropdownState();
			_moteAssetDropdown = new MoteAssetDropdown(
				_moteAssetDropdownState,
				GetMoteAssetPaths(),
				GetRecentMoteAssetPaths(),
				_sourceAssetPath,
				SelectMoteAssetPath);
			_moteAssetDropdown.Show(field);
		}

		private void SelectMoteAssetPath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				SetSourceAsset(null);
				return;
			}
			TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
			if (asset is null)
			{
				InvalidateMoteAssetPathCache();
				ShowNotification(new GUIContent("The selected .mote asset no longer exists."));
				return;
			}
			SetSourceAsset(asset);
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
			if (asset is not null)
			{
				RememberMoteAsset(AssetDatabase.GetAssetPath(asset));
			}
			if (ReferenceEquals(_sourceAsset, asset))
			{
				return;
			}
			_selectedTarget = null;
			_sourceAsset = asset;
			_sourceAssetPath = asset is null
				? string.Empty
				: AssetDatabase.GetAssetPath(asset);
			_sourceChanged = false;
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
			LoadBreakpoints();
		}

		private static bool IsMoteAssetPath(string path)
		{
			return !string.IsNullOrEmpty(path)
				&& path.EndsWith(".mote", StringComparison.OrdinalIgnoreCase);
		}

		private static IReadOnlyList<string> GetMoteAssetPaths()
		{
			if (s_cachedMoteAssetPaths is not null)
			{
				return s_cachedMoteAssetPaths;
			}
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
			s_cachedMoteAssetPaths = motePaths.ToArray();
			return s_cachedMoteAssetPaths;
		}

		private static IReadOnlyList<string> GetRecentMoteAssetPaths()
		{
			string serialized = SessionState.GetString(
				RecentMoteAssetsSessionKey,
				string.Empty);
			if (string.IsNullOrEmpty(serialized))
			{
				return Array.Empty<string>();
			}
			string[] guids = serialized.Split(';');
			var paths = new List<string>(guids.Length);
			for (int i = 0; i < guids.Length; ++i)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				if (IsMoteAssetPath(path) && !paths.Contains(path))
				{
					paths.Add(path);
				}
			}
			return paths;
		}

		private static void RememberMoteAsset(string path)
		{
			if (!IsMoteAssetPath(path)) return;
			string guid = AssetDatabase.AssetPathToGUID(path);
			if (string.IsNullOrEmpty(guid)) return;
			string serialized = SessionState.GetString(
				RecentMoteAssetsSessionKey,
				string.Empty);
			var guids = new List<string>(serialized.Split(';'));
			guids.RemoveAll(value => string.IsNullOrEmpty(value)
				|| string.Equals(value, guid, StringComparison.Ordinal));
			guids.Insert(0, guid);
			if (guids.Count > MaximumRecentMoteAssets)
			{
				guids.RemoveRange(
					MaximumRecentMoteAssets,
					guids.Count - MaximumRecentMoteAssets);
			}
			SessionState.SetString(
				RecentMoteAssetsSessionKey,
				string.Join(";", guids));
		}

		private static void InvalidateMoteAssetPathCache()
		{
			s_cachedMoteAssetPaths = null;
		}

		private void DrawControls()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				bool stoppable = _execution?.State == MoteExecutionState.Paused
					|| _execution?.State == MoteExecutionState.Yielded;
				EditorGUI.BeginDisabledGroup(
					(_sourceAsset == null || _selectedTarget is not null)
					&& !stoppable);
				GUIContent runContent = stoppable
					? new GUIContent("Stop", "Shift+F5")
					: new GUIContent("Run", "F5");
				if (GUILayout.Button(runContent))
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
				bool canStartDebugging = _selectedTarget is null
					&& _sourceAsset != null
					&& CanStartNewExecution();
				EditorGUI.BeginDisabledGroup(!paused && !yielded);
				if (GUILayout.Button(new GUIContent("Continue", "F5")))
				{
					Execute(_execution.Continue);
				}
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(!canStep && !canStartDebugging);
				if (GUILayout.Button(new GUIContent("Step Over", "F10")))
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
				if (GUILayout.Button(new GUIContent("Step Into", "F11")))
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
				if (GUILayout.Button(
					new GUIContent("Step Out", "Shift+F11")))
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
			if (_sourceChanged)
			{
				locationText += "  [Source changed — restart required]";
			}
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
			int executionLine =
				_execution?.CurrentLocation?.StartLine ?? -1;
			int visibleLine = executionLine > 0
				? executionLine
				: _selectedLine;
			float rowHeight = EditorGUIUtility.singleLineHeight + 2f;
			Rect viewport = new(
				0f,
				titleHeight,
				rect.width,
				Mathf.Max(0f, rect.height - titleHeight));
			if (_execution?.CurrentLocation is MoteSourceSpan
				&& visibleLine != _visibleExecutionLine)
			{
				_sourceScroll.y = Mathf.Max(
					0f,
					(visibleLine - 1) * rowHeight - viewport.height * 0.5f);
				_visibleExecutionLine = visibleLine;
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
				Mathf.Max(viewport.width - 16f, 90f + codeWidth),
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
				MoteSourceLinePresentation presentation =
					MoteSourceLinePresentationResolver.Resolve(
						line,
						_selectedLine,
						executionLine,
						_execution?.State);
				if (presentation != MoteSourceLinePresentation.Normal)
				{
					EditorGUI.DrawRect(
						row,
						GetSourceLineColor(presentation));
				}
				bool hasBreakpoint = HasBreakpoint(line);
				Color previousContentColor = GUI.contentColor;
				GUI.contentColor = hasBreakpoint
					? new Color(0.9f, 0.2f, 0.2f)
					: previousContentColor;
				if (GUI.Button(
					new Rect(0f, y, 24f, rowHeight),
					hasBreakpoint ? "●" : "○"))
				{
					ToggleBreakpoint(line);
				}
				GUI.contentColor = previousContentColor;
				if (IsExecutionPresentation(presentation))
				{
					Color previousColor = GUI.color;
					GUI.color = GetExecutionMarkerColor(presentation);
					GUI.Label(
						new Rect(26f, y, 18f, rowHeight),
						new GUIContent("▶", GetExecutionTooltip(presentation)),
						_executionMarkerStyle);
					GUI.color = previousColor;
				}
				GUI.Label(
					new Rect(46f, y, 38f, rowHeight),
					line.ToString(),
					_lineStyle);
				GUI.Label(
					new Rect(86f, y, codeWidth + 4f, rowHeight),
					lines[index],
					_lineStyle);
				Event current = Event.current;
				if (current.type == EventType.MouseDown
					&& current.button == 0
					&& current.clickCount == 2
					&& row.Contains(current.mousePosition))
				{
					OpenSourceInExternalEditor(line);
					current.Use();
				}
			}
			GUI.EndScrollView();
			GUI.EndGroup();
		}

		private static bool IsExecutionPresentation(
			MoteSourceLinePresentation presentation)
		{
			return presentation == MoteSourceLinePresentation.Paused
				|| presentation == MoteSourceLinePresentation.Yielded
				|| presentation == MoteSourceLinePresentation.Faulted;
		}

		private static Color GetSourceLineColor(
			MoteSourceLinePresentation presentation)
		{
			bool dark = EditorGUIUtility.isProSkin;
			return presentation switch
			{
				MoteSourceLinePresentation.Paused => dark
					? new Color(0.62f, 0.4f, 0.08f, 0.78f)
					: new Color(1f, 0.78f, 0.25f, 0.72f),
				MoteSourceLinePresentation.Yielded => dark
					? new Color(0.08f, 0.46f, 0.48f, 0.68f)
					: new Color(0.45f, 0.85f, 0.85f, 0.72f),
				MoteSourceLinePresentation.Faulted => dark
					? new Color(0.62f, 0.14f, 0.14f, 0.76f)
					: new Color(1f, 0.48f, 0.48f, 0.7f),
				_ => dark
					? new Color(0.22f, 0.34f, 0.52f, 0.45f)
					: new Color(0.62f, 0.78f, 1f, 0.52f),
			};
		}

		private static Color GetExecutionMarkerColor(
			MoteSourceLinePresentation presentation)
		{
			return presentation switch
			{
				MoteSourceLinePresentation.Faulted => new Color(1f, 0.3f, 0.3f),
				MoteSourceLinePresentation.Yielded => new Color(0.25f, 0.9f, 0.9f),
				_ => new Color(1f, 0.72f, 0.18f),
			};
		}

		private static string GetExecutionTooltip(
			MoteSourceLinePresentation presentation)
		{
			return presentation switch
			{
				MoteSourceLinePresentation.Faulted => "Exception location",
				MoteSourceLinePresentation.Yielded => "Yielded execution location",
				_ => "Paused execution location",
			};
		}

		private void OpenSourceInExternalEditor(int line)
		{
			TextAsset asset = _sourceAsset;
			if (asset is null && _document is not null)
			{
				asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
					_document.SourceName);
			}
			if (asset is null)
			{
				ShowNotification(
					new GUIContent("The source asset could not be opened."));
				return;
			}
			AssetDatabase.OpenAsset(asset, Math.Max(1, line));
		}

		private void DrawVariablesPane(Rect rect)
		{
			IReadOnlyList<MoteDebugVariable> variables =
				_execution?.GetVariables() ?? Array.Empty<MoteDebugVariable>();
			DrawVariablesPane(rect, variables);
		}

		private void DrawVariablesPane(
			Rect rect,
			IReadOnlyList<MoteDebugVariable> variables)
		{
			GUI.BeginGroup(rect);
			GUI.Label(
				new Rect(0f, 0f, rect.width, EditorGUIUtility.singleLineHeight),
				"Context Variables",
				EditorStyles.boldLabel);
			EnsureVariableTree();
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
			_variablesTree = new VariableTreeView(
				_variablesTreeState,
				new MultiColumnHeader(headerState));
			_variablesTree.ValueEdited += SetVariableValue;
		}

		private void SetVariableValue(int id, string value)
		{
			if (_execution is null)
			{
				return;
			}
			if (!_execution.TrySetVariableValue(id, value, out string error))
			{
				ShowNotification(new GUIContent(error));
			}
			Repaint();
		}

		private void StartDebugging(bool startPaused)
		{
			ClearValueSelection();
			try
			{
				switch (_numericType)
				{
					case NumericType.Double:
						StartDebugging<double>(startPaused);
						break;
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
			RefreshSourceAsset();
			MoteDecoder<T>.Setup();
			var decoder = new MoteDecoder<T>();
			string path = _sourceAssetPath;
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
			_sourceChanged = false;
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
			SaveBreakpoints();
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
			SaveBreakpoints();
		}

		private void LoadBreakpoints()
		{
			_breakpoints.Clear();
			string key = GetBreakpointSessionKey();
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			string serialized = SessionState.GetString(key, string.Empty);
			if (string.IsNullOrEmpty(serialized))
			{
				return;
			}
			string[] values = serialized.Split(',');
			for (int i = 0; i < values.Length; ++i)
			{
				if (int.TryParse(values[i], out int line) && line > 0)
				{
					_breakpoints.Add(line);
				}
			}
		}

		private void SaveBreakpoints()
		{
			string key = GetBreakpointSessionKey();
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			var lines = new List<int>(_breakpoints);
			lines.Sort();
			SessionState.SetString(key, string.Join(",", lines));
		}

		private string GetBreakpointSessionKey()
		{
			if (string.IsNullOrEmpty(_sourceAssetPath))
			{
				return null;
			}
			string guid = AssetDatabase.AssetPathToGUID(_sourceAssetPath);
			string identity = string.IsNullOrEmpty(guid)
				? _sourceAssetPath
				: guid;
			return BreakpointSessionKeyPrefix + identity;
		}

		private void OnExecutionCreated(IMoteDebugExecution execution)
		{
			IReadOnlyList<IMoteDebugTarget> targets =
				MoteDebugTargetRegistry.Targets;
			if (_selectedTarget is not null || targets.Count != 0)
			{
				return;
			}
			_execution = execution;
			_document = execution.DebugDocument;
			_sourceAssetPath = execution.DebugDocument?.SourceName
				?? string.Empty;
			if (!string.IsNullOrEmpty(_sourceAssetPath))
			{
				_sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
					_sourceAssetPath);
			}
			_sourceChanged = false;
			_visibleExecutionLine = -1;
			Repaint();
		}

		private void OnTargetRegistered(IMoteDebugTarget target)
		{
			Repaint();
		}

		private void OnTargetUnregistered(IMoteDebugTarget target)
		{
			if (!ReferenceEquals(_selectedTarget, target))
			{
				Repaint();
				return;
			}
			_selectedTarget = null;
			_execution = null;
			_document = null;
			_sourceAsset = null;
			_sourceAssetPath = string.Empty;
			Repaint();
		}

		private void OnTargetChanged(IMoteDebugTarget target)
		{
			if (ReferenceEquals(_selectedTarget, target))
			{
				_execution = target.Execution;
				_document = _execution?.DebugDocument;
			}
			Repaint();
		}

		private void SelectTarget(IMoteDebugTarget target)
		{
			SaveBreakpoints();
			_selectedTarget = target;
			_execution = target?.Execution;
			_document = _execution?.DebugDocument;
			_sourceAsset = target?.SourceAsset;
			_sourceAssetPath = _sourceAsset == null
				? string.Empty
				: AssetDatabase.GetAssetPath(_sourceAsset);
			_sourceChanged = false;
			_selectedLine = -1;
			_visibleExecutionLine = -1;
			LoadBreakpoints();
			if (_execution is not null)
			{
				foreach (int line in _breakpoints)
				{
					_execution.SetBreakpoint(line);
				}
			}
			Repaint();
		}

		private void SanitizeDestroyedReferences()
		{
			if (!ReferenceEquals(_sourceAsset, null) && _sourceAsset == null)
			{
				_sourceAsset = null;
				_sourceAssetPath = string.Empty;
				if (_selectedTarget is not null
					&& _selectedTarget.SourceAsset == null)
				{
					_selectedTarget = null;
					_execution = null;
					_document = null;
				}
			}
		}

		private void OnMoteAssetChanged(string oldPath, string newPath)
		{
			InvalidateMoteAssetPathCache();
			if (string.IsNullOrEmpty(_sourceAssetPath)
				|| !string.Equals(
					_sourceAssetPath,
					oldPath,
					StringComparison.Ordinal))
			{
				return;
			}
			if (!string.IsNullOrEmpty(newPath))
			{
				_sourceAssetPath = newPath;
			}
			bool suspended = _execution?.State == MoteExecutionState.Paused
				|| _execution?.State == MoteExecutionState.Yielded;
			if (suspended)
			{
				_sourceChanged = true;
			}
			else
			{
				RefreshSourceAsset();
				_execution = null;
				_document = null;
				_selectedLine = -1;
				_visibleExecutionLine = -1;
			}
			Repaint();
		}

		private void RefreshSourceAsset()
		{
			if (string.IsNullOrEmpty(_sourceAssetPath))
			{
				return;
			}
			_sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
				_sourceAssetPath);
			_sourceChanged = false;
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
			_executionMarkerStyle = new GUIStyle(EditorStyles.label)
			{
				alignment = TextAnchor.MiddleCenter,
				font = EditorStyles.miniBoldFont,
				wordWrap = false,
			};
			SetTextColor(_executionMarkerStyle, Color.white);
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
						|| left[i].CanEdit != right[i].CanEdit
						|| !string.Equals(
							left[i].EditValue,
							right[i].EditValue,
							StringComparison.Ordinal)
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
					if ((Column)columnIndex == Column.Value
						&& item.Variable.CanEdit)
					{
						EditorGUI.BeginChangeCheck();
						string edited = EditorGUI.DelayedTextField(
							cell,
							item.Variable.EditValue ?? string.Empty);
						if (EditorGUI.EndChangeCheck())
						{
							ValueEdited?.Invoke(item.Variable.Id, edited);
						}
						continue;
					}
					string value = (Column)columnIndex switch
					{
						Column.Type => item.Variable.Type,
						_ => item.Variable.Value,
					};
					EditorGUI.LabelField(cell, value ?? string.Empty);
				}
			}

			public event Action<int, string> ValueEdited;

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
				if (column == Column.Name
					&& TryGetArrayIndex(left.Name, out int leftIndex)
					&& TryGetArrayIndex(right.Name, out int rightIndex))
				{
					return leftIndex.CompareTo(rightIndex);
				}
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

			private static bool TryGetArrayIndex(string name, out int index)
			{
				if (!string.IsNullOrEmpty(name)
					&& name.Length > 2
					&& name[0] == '['
					&& name[name.Length - 1] == ']')
				{
					return int.TryParse(
						name.Substring(1, name.Length - 2),
						NumberStyles.None,
						CultureInfo.InvariantCulture,
						out index);
				}
				index = 0;
				return false;
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
