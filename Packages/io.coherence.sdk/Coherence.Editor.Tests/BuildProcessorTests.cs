// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.IO;
    using Coherence.Build;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;

    public class BuildProcessorTests : CoherenceTest
    {
        [Test]
        public void DeleteStreamingAssetsIfEmpty_Should_CleanUpFilesCreatedBy_EnsureStreamingAssetsPath()
        {
            var path = Application.streamingAssetsPath;
            var metaFilePath = PathUtils.GetMetaFilePath(path);

            if (Directory.Exists(path))
            {
                Assert.Ignore($"StreamingAssets folder already exists at '{path}'; skipping to avoid touching pre-existing data.");
            }

            BuildPreprocessor.EnsureStreamingAssetsPath();
            var directoryExisted = Directory.Exists(path);
            var metaFileExisted = File.Exists(metaFilePath);
            BuildPostprocessor.DeleteStreamingAssetsIfEmpty();
            var directoryExists = Directory.Exists(path);
            var metaFileExists = File.Exists(metaFilePath);

            Assert.IsTrue(directoryExisted);
            Assert.IsTrue(metaFileExisted);
            Assert.IsFalse(directoryExists);
            Assert.IsFalse(metaFileExists);
        }

        [Test]
        [TestCase(true, true, ExpectedResult = false, TestName = "Skips refresh while building a simulator (avoids hanging the upload)")]
        [TestCase(true, false, ExpectedResult = true, TestName = "Refreshes after a regular player build when bake on build is enabled")]
        [TestCase(false, true, ExpectedResult = false, TestName = "Does not refresh when bake on build is disabled (simulator)")]
        [TestCase(false, false, ExpectedResult = false, TestName = "Does not refresh when bake on build is disabled (player)")]
        public bool ShouldRefreshAfterBuild_Skips_Recompilation_While_Building_Simulator(bool bakeOnBuild, bool isBuildingSimulator)
            => BuildPostprocessor.ShouldRefreshAfterBuild(bakeOnBuild, isBuildingSimulator);

        // Baking must run before the simulator build (after the simulator scripting symbols are set) so the
        // freshly baked code is compiled into the uploaded build. In the editor the build is deferred until
        // the bake's recompilation completes; in headless/batch mode there is no reload loop, so the build
        // proceeds right after baking (BuildPipeline.BuildPlayer compiles the freshly written source).
        [Test]
        public void GetBakeBeforeBuildAction_Sequences_Bake_Before_Build()
        {
            // The automation is disabled: never bake, regardless of mode.
            Assert.AreEqual(SimulatorBuildPipeline.BakeBeforeBuildAction.Build,
                SimulatorBuildPipeline.GetBakeBeforeBuildAction(bakeOnBuild: false, alreadyBaked: false, isBatchMode: false),
                "Should not bake when the automation is disabled (editor).");
            Assert.AreEqual(SimulatorBuildPipeline.BakeBeforeBuildAction.Build,
                SimulatorBuildPipeline.GetBakeBeforeBuildAction(bakeOnBuild: false, alreadyBaked: false, isBatchMode: true),
                "Should not bake when the automation is disabled (headless).");

            // Already baked for this build: don't bake again, so the deferred build can't loop.
            Assert.AreEqual(SimulatorBuildPipeline.BakeBeforeBuildAction.Build,
                SimulatorBuildPipeline.GetBakeBeforeBuildAction(bakeOnBuild: true, alreadyBaked: true, isBatchMode: false),
                "Should not re-bake once already baked for this build (editor).");
            Assert.AreEqual(SimulatorBuildPipeline.BakeBeforeBuildAction.Build,
                SimulatorBuildPipeline.GetBakeBeforeBuildAction(bakeOnBuild: true, alreadyBaked: true, isBatchMode: true),
                "Should not re-bake once already baked for this build (headless).");

            // Editor: bake, then defer the build until the bake's recompilation completes.
            Assert.AreEqual(SimulatorBuildPipeline.BakeBeforeBuildAction.BakeThenDefer,
                SimulatorBuildPipeline.GetBakeBeforeBuildAction(bakeOnBuild: true, alreadyBaked: false, isBatchMode: false),
                "Editor should bake and defer the build until recompilation completes.");

            // Headless/batch: bake, then build immediately. There is no editor loop to defer to, and
            // BuildPipeline.BuildPlayer compiles the freshly written source into the build.
            Assert.AreEqual(SimulatorBuildPipeline.BakeBeforeBuildAction.BakeThenBuild,
                SimulatorBuildPipeline.GetBakeBeforeBuildAction(bakeOnBuild: true, alreadyBaked: false, isBatchMode: true),
                "Headless should bake and build immediately without a reload round-trip.");
        }
    }
}
