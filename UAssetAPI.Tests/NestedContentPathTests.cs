using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace UAssetAPI.Tests
{
    /// <summary>
    /// FindAssetOnDiskFromPath used to treat the rightmost directory named Content as the
    /// /Game root. A cooked tree can contain another folder with that name (Clair Obscur
    /// does, under BattleLineSystem, inventory shards, and HUD widgets). The dependency
    /// then has to be found from an earlier Content segment, while a Plugins tree and an
    /// extract whose parent is also named Content keep the anchors they already had.
    /// </summary>
    [TestClass]
    public class NestedContentPathTests
    {
        private string _root;

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "uassetapi-nested-content-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestMethod]
        public void NestedContent_ResolvesGameDependencyAboveTheInnerFolder()
        {
            // atenfyr/UAssetAPI#158 — DA_BattleLineSettings_Expedition33 sits in a second Content folder.
            string openFile = Touch("Sandfall", "Content", "Gameplay", "Audio", "BattleLineSystem", "Content", "DA_BattleLineSettings_Expedition33.uasset");
            string parent = Touch("Sandfall", "Content", "Gameplay", "Audio", "BattleLineSystem", "BP_BattleLineSettings.uasset");

            AssertSamePath(parent, Resolve(openFile, "/Game/Gameplay/Audio/BattleLineSystem/BP_BattleLineSettings"));
        }

        [TestMethod]
        public void SingleContentRoot_StillResolvesUnderThatRoot()
        {
            string openFile = Touch("Sandfall", "Content", "Gameplay", "Pictos", "Gear", "DA_GearPictos_DodgeBurning.uasset");
            string dep = Touch("Sandfall", "Content", "Gameplay", "Inventory", "BP_DataAsset_Item_Pictos.uasset");

            AssertSamePath(dep, Resolve(openFile, "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos"));
        }

        [TestMethod]
        public void NearerContentWins_WhenAnOuterFolderIsAlsoNamedContent()
        {
            string openFile = Touch("Extracts", "Content", "Sandfall", "Content", "Gameplay", "Audio", "BattleLineSystem", "Content", "DA.uasset");
            string project = Touch("Extracts", "Content", "Sandfall", "Content", "Gameplay", "Audio", "BattleLineSystem", "BP_BattleLineSettings.uasset");
            Touch("Extracts", "Content", "Gameplay", "Audio", "BattleLineSystem", "BP_BattleLineSettings.uasset");

            AssertSamePath(project, Resolve(openFile, "/Game/Gameplay/Audio/BattleLineSystem/BP_BattleLineSettings"));
        }

        [TestMethod]
        public void InnermostContentWins_WhenTheDependencyActuallyLivesThere()
        {
            string openFile = Touch("Sandfall", "Content", "Mods", "Content", "Foo.uasset");
            string inner = Touch("Sandfall", "Content", "Mods", "Content", "Bar.uasset");
            Touch("Sandfall", "Content", "Bar.uasset");

            AssertSamePath(inner, Resolve(openFile, "/Game/Bar"));
        }

        [TestMethod]
        public void PluginAsset_PrefersGameContentBesidePluginsOverAPluginLocalDecoy()
        {
            string openFile = Touch("Sandfall", "Plugins", "MyPlugin", "Content", "Foo.uasset");
            string real = Touch("Sandfall", "Content", "Gameplay", "Real.uasset");
            Touch("Sandfall", "Plugins", "MyPlugin", "Content", "Gameplay", "Real.uasset");

            AssertSamePath(real, Resolve(openFile, "/Game/Gameplay/Real"));
        }

        [TestMethod]
        public void PluginAsset_DoesNotTreatPluginContentAsTheGameRoot()
        {
            string openFile = Touch("Sandfall", "Plugins", "MyPlugin", "Content", "Widget", "Content", "DA.uasset");
            Touch("Sandfall", "Plugins", "MyPlugin", "Content", "Gameplay", "Foo.uasset");

            Assert.IsNull(Resolve(openFile, "/Game/Gameplay/Foo"));
        }

        [TestMethod]
        public void PluginPackagePath_ResolvesThroughTheGameContentAnchor()
        {
            string openFile = Touch("Sandfall", "Content", "Some", "Asset.uasset");
            string pluginAsset = Touch("Sandfall", "Plugins", "MyPlugin", "Content", "Foo.uasset");

            AssertSamePath(pluginAsset, Resolve(openFile, "/MyPlugin/Foo"));
        }

        [TestMethod]
        public void SameDirectoryFallback_StillFindsASiblingWhenNoContentAnchorMatches()
        {
            string openFile = Touch("Loose", "Bar.uasset");
            string sibling = Touch("Loose", "BP_BattleLineSettings.uasset");

            AssertSamePath(sibling, Resolve(openFile, "/Game/Gameplay/Audio/BattleLineSystem/BP_BattleLineSettings"));
        }

        [TestMethod]
        public void MissingDependency_ReturnsNull()
        {
            string openFile = Touch("Sandfall", "Content", "Gameplay", "Foo.uasset");

            Assert.IsNull(Resolve(openFile, "/Game/Gameplay/DoesNotExist"));
        }

        private string Touch(params string[] parts)
        {
            string path = _root;
            foreach (string part in parts) path = Path.Combine(path, part);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Array.Empty<byte>());
            return path;
        }

        private static string Resolve(string openFile, string assetPath)
        {
            var asset = new UAsset();
            asset.FilePath = openFile;
            return asset.FindAssetOnDiskFromPath(assetPath);
        }

        private static void AssertSamePath(string expected, string actual)
        {
            Assert.IsNotNull(actual, "resolver returned null");
            Assert.AreEqual(Path.GetFullPath(expected), Path.GetFullPath(actual));
        }
    }
}
