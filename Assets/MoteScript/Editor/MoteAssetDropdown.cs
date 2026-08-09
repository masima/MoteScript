#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace MoteScript.Editor
{
	internal sealed class MoteAssetDropdown : AdvancedDropdown
	{
		private sealed class AssetItem : AdvancedDropdownItem
		{
			internal AssetItem(string name, string assetPath)
				: base(name)
			{
				AssetPath = assetPath;
			}

			internal string AssetPath { get; }
		}

		private readonly IReadOnlyList<string> _assetPaths;
		private readonly IReadOnlyList<string> _recentPaths;
		private readonly string _currentPath;
		private readonly Action<string> _selected;

		internal MoteAssetDropdown(
			AdvancedDropdownState state,
			IReadOnlyList<string> assetPaths,
			IReadOnlyList<string> recentPaths,
			string currentPath,
			Action<string> selected)
			: base(state)
		{
			_assetPaths = assetPaths;
			_recentPaths = recentPaths;
			_currentPath = currentPath;
			_selected = selected;
			minimumSize = new Vector2(460f, 320f);
		}

		protected override AdvancedDropdownItem BuildRoot()
		{
			var root = new AdvancedDropdownItem("MoteScript Assets");
			root.AddChild(new AssetItem("None", string.Empty));

			if (_recentPaths.Count != 0)
			{
				var recent = new AdvancedDropdownItem("Recent");
				for (int i = 0; i < _recentPaths.Count; ++i)
				{
					recent.AddChild(CreateAssetItem(_recentPaths[i]));
				}
				root.AddChild(recent);
			}

			MoteAssetMenuNode tree = MoteAssetMenuTree.Build(_assetPaths);
			if (tree.Children.Count == 0)
			{
				var empty = new AdvancedDropdownItem("No .mote assets found")
				{
					enabled = false,
				};
				root.AddChild(empty);
				return root;
			}

			var allScripts = new AdvancedDropdownItem("All Scripts");
			for (int i = 0; i < tree.Children.Count; ++i)
			{
				AddNode(allScripts, tree.Children[i]);
			}
			root.AddChild(allScripts);
			return root;
		}

		protected override void ItemSelected(AdvancedDropdownItem item)
		{
			if (item is AssetItem asset)
			{
				_selected(asset.AssetPath);
			}
		}

		private void AddNode(
			AdvancedDropdownItem parent,
			MoteAssetMenuNode node)
		{
			if (node.IsAsset)
			{
				parent.AddChild(CreateAssetItem(node.AssetPath));
				return;
			}

			MoteAssetMenuNode target = MoteAssetMenuTree.Compress(
				node,
				out string label);
			var folder = new AdvancedDropdownItem(label);
			for (int i = 0; i < target.Children.Count; ++i)
			{
				AddNode(folder, target.Children[i]);
			}
			parent.AddChild(folder);
		}

		private AssetItem CreateAssetItem(string path)
		{
			string fileName = Path.GetFileName(path);
			string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
			string label = string.IsNullOrEmpty(directory)
				? fileName
				: $"{fileName}    —    {directory}";
			if (string.Equals(path, _currentPath, StringComparison.Ordinal))
			{
				label = "✓  " + label;
			}
			return new AssetItem(label, path);
		}
	}
}
#endif
