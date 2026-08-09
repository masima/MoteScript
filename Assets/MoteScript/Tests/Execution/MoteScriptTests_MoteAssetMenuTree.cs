using MoteScript.Editor;
using NUnit.Framework;

namespace MoteScript.Tests
{
	internal sealed class MoteScriptTests_MoteAssetMenuTree
	{
		[Test]
		public void Build_CompressesDirectoriesWithoutBranches()
		{
			MoteAssetMenuNode root = MoteAssetMenuTree.Build(new[]
			{
				"Assets/Game/Scripts/Mote/Characters/Player/Player.mote",
				"Assets/Game/Scripts/Mote/Characters/Enemy/Enemy.mote",
				"Assets/Game/Scripts/Mote/System.mote",
			});

			MoteAssetMenuNode mote = MoteAssetMenuTree.Compress(
				root.Children[0],
				out string label);

			Assert.That(label, Is.EqualTo("Assets  ›  Game  ›  Scripts  ›  Mote"));
			Assert.That(mote.Name, Is.EqualTo("Mote"));
			Assert.That(mote.Children, Has.Count.EqualTo(2));
		}

		[Test]
		public void Build_SortsDirectoriesBeforeAssets()
		{
			MoteAssetMenuNode root = MoteAssetMenuTree.Build(new[]
			{
				"Assets/Zeta.mote",
				"Assets/Beta/Second.mote",
				"Assets/Alpha/First.mote",
				"Assets/Alpha.mote",
			});
			MoteAssetMenuNode assets = root.Children[0];

			Assert.That(assets.Children[0].Name, Is.EqualTo("Alpha"));
			Assert.That(assets.Children[1].Name, Is.EqualTo("Beta"));
			Assert.That(assets.Children[2].Name, Is.EqualTo("Alpha.mote"));
			Assert.That(assets.Children[3].Name, Is.EqualTo("Zeta.mote"));
		}
	}
}
