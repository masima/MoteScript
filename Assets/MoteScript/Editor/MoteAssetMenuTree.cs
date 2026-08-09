#if UNITY_EDITOR || MOTESCRIPT_DEBUGGER
using System;
using System.Collections.Generic;

namespace MoteScript.Editor
{
	internal sealed class MoteAssetMenuNode
	{
		internal MoteAssetMenuNode(string name, string assetPath = null)
		{
			Name = name;
			AssetPath = assetPath;
		}

		internal string Name { get; }
		internal string AssetPath { get; }
		internal bool IsAsset => AssetPath is not null;
		internal List<MoteAssetMenuNode> Children { get; } = new();
	}

	internal static class MoteAssetMenuTree
	{
		internal static MoteAssetMenuNode Build(IReadOnlyList<string> assetPaths)
		{
			var root = new MoteAssetMenuNode("MoteScript Assets");
			for (int pathIndex = 0; pathIndex < assetPaths.Count; ++pathIndex)
			{
				string path = assetPaths[pathIndex];
				if (string.IsNullOrEmpty(path)) continue;
				string[] parts = path.Split('/');
				MoteAssetMenuNode parent = root;
				for (int i = 0; i < parts.Length - 1; ++i)
				{
					MoteAssetMenuNode directory = parent.Children.Find(
						child => !child.IsAsset
							&& string.Equals(child.Name, parts[i], StringComparison.Ordinal));
					if (directory is null)
					{
						directory = new MoteAssetMenuNode(parts[i]);
						parent.Children.Add(directory);
					}
					parent = directory;
				}
				parent.Children.Add(new MoteAssetMenuNode(
					parts[^1],
					path));
			}
			Sort(root);
			return root;
		}

		internal static MoteAssetMenuNode Compress(
			MoteAssetMenuNode node,
			out string label)
		{
			label = node.Name;
			MoteAssetMenuNode current = node;
			while (!current.IsAsset
				&& current.Children.Count == 1
				&& !current.Children[0].IsAsset)
			{
				current = current.Children[0];
				label += "  ›  " + current.Name;
			}
			return current;
		}

		private static void Sort(MoteAssetMenuNode node)
		{
			node.Children.Sort((left, right) =>
			{
				if (left.IsAsset != right.IsAsset)
				{
					return left.IsAsset ? 1 : -1;
				}
				return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
			});
			for (int i = 0; i < node.Children.Count; ++i)
			{
				if (!node.Children[i].IsAsset) Sort(node.Children[i]);
			}
		}
	}
}
#endif
