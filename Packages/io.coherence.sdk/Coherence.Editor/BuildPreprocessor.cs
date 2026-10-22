// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.IO;
    using Build;
    using Coherence.Toolkit;
    using Log;
    using Portal;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEngine;

    internal class BuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly LazyLogger Logger = Log.GetLazyLogger(typeof(BuildPreprocessor));

        internal static bool GetIsDevelopmentMode() => SimulatorBuildPipeline.IsBuildingSimulator ? SimulatorBuildOptions.Get().DevBuild : !BuildPipeline.isBuildingPlayer || EditorUserBuildSettings.development;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Simulator builds bake before invoking BuildPipeline.BuildPlayer (see
            // SimulatorBuildPipeline.TryBakeBeforeBuild) so the freshly baked code is compiled into the
            // build. Baking again here (inside the build) would be redundant and, in the editor, its
            // recompilation request would land after the build and interrupt the simulator upload.
            if (BakeUtil.BakeOnBuild && !SimulatorBuildPipeline.IsBuildingSimulator)
            {
                _ = BakeUtil.Bake(waitForUpdateSyncState: ShouldWait.Never);
            }

            if (ManagedCodeStrippingUtils.PreventCodeStrippingUsingLinkXmlFile)
            {
                ManagedCodeStrippingUtils.CopyLinkXmlFileUnderAssetsFolder();
            }

            Postprocessor.UpdateRuntimeSettings();

            if (PortalUtil.UploadOnBuild
                // Only auto-upload schemas to the project being built.
                && ProjectSettings.instance.GetActiveProject() is { } activeProject)
            {
                Schemas.Upload(new[] { activeProject }, updateSyncState: !Application.isBatchMode, waitForUpdateSyncState: ShouldWait.Never);
            }

            if (!ProjectSettings.instance.RSBundlingEnabled)
            {
                return;
            }

            if (!ReplicationServerBinaries.IsSupportedPlatform(report.summary.platform))
            {
                return;
            }

            if (!EnsureStreamingAssetsPath())
            {
                throw new BuildFailedException(
                    "Failed to create the Streaming Assets folder, the Replication Server will not be bundled. Cancelling build.");
            }

            if (!CopyCombinedSchemaToStreamingAssets())
            {
                throw new BuildFailedException(
                    "Failed to copy the combined schema file to the Streaming Assets folder, " +
                    "the Replication Server will not be bundled. Cancelling build.");
            }

            if (!ReplicationServerBundler.BundleWithStreamingAssets(report.summary.platform))
            {
                throw new BuildFailedException(
                    "Failed to copy the Replication Server binary to the Streaming Assets folder, " +
                    "the Replication Server will not be bundled. Cancelling build.");
            }
        }

        internal static bool EnsureStreamingAssetsPath() => AssetUtils.CreateFolder(Paths.streamingAssetsPath);

        private static bool CopyCombinedSchemaToStreamingAssets()
        {
            var combinedSchemaInfo = new FileInfo(Paths.combinedSchemaPath);

            if (!combinedSchemaInfo.Exists)
            {
                Logger.Error(Error.EditorBuildPreprocessorMissingSchema,
                    $"Can't find combined schema at '{Paths.combinedSchemaPath}', won't be able to bundle it with build." +
                    "Make sure you bake the project at least once.");
                return false;
            }

            AssetPath destinationPath = Paths.streamingAssetsCombinedSchemaPath;

            try
            {
                EditorAssetUtils.MakeAssetEditable(destinationPath);
                combinedSchemaInfo.CopyTo(destinationPath, true);
                AssetUtils.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception e)
            {
                Logger.Error(Error.EditorBuildPreprocessorFailedToCopySchema,
                    $"Failed to copy the combined schema file to {destinationPath}.", ("Exception", e));
                return false;
            }

            Logger.Debug("Did successfully bundle combined schema.", ("destinationPath", destinationPath));

            return true;
        }
    }
}
