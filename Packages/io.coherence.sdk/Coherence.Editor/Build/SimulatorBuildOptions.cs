// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Build
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using Paths = Coherence.Editor.Paths;

    public class SimulatorBuildOptions : ScriptableObject
    {
        public static string BuildName => "sim_x86_64";

        public IEnumerable<SceneAsset> ScenesToBuild => scenesToBuild;
        public OptimizeForSize BuildSizeOptimizations => buildSizeOptimizations;
        public bool HeadlessMode => headlessMode;
        public ScriptingImplementation ScriptingImplementation => scriptingImplementation;
        public bool DevBuild => developmentBuild;
        public bool AutoConnectProfiler => autoConnectProfiler;
        public bool DeepProfilingSupport => deepProfilingSupport;
        public bool ScriptDebugging => scriptDebugging;

        [SerializeField]
        private List<SceneAsset> scenesToBuild = new List<SceneAsset>();

        [SerializeField, Tooltip("This option will always be true for Linux clients that are built to be uploaded to the Portal")]
        private bool headlessMode = true;

        [SerializeField]
        private ScriptingImplementation scriptingImplementation;

        [SerializeField]
        private bool developmentBuild;

        [SerializeField]
        private bool autoConnectProfiler;

        [SerializeField]
        private bool deepProfilingSupport;

        [SerializeField]
        private bool scriptDebugging;

        [SerializeField]
        private OptimizeForSize buildSizeOptimizations = new OptimizeForSize();

        private static string DefaultOptionsAssetName => "io.coherence.simulator.build.options";

        public bool HasAnyScenes => scenesToBuild.Any(s => s != null);

        public static SimulatorBuildOptions Get()
        {
            if (!EditorBuildSettings.TryGetConfigObject(DefaultOptionsAssetName, out SimulatorBuildOptions settings))
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Paths.simulatorBuildOptionsPath)));
                settings = CreateInstance<SimulatorBuildOptions>();
                AssetDatabase.CreateAsset(settings, Paths.simulatorBuildOptionsPath);
                EditorBuildSettings.AddConfigObject(DefaultOptionsAssetName, settings, true);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties found in <see cref="SimulatorBuildOptions"/>.
        /// Can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string headlessMode = nameof(SimulatorBuildOptions.headlessMode);
            public const string scriptingImplementation = nameof(SimulatorBuildOptions.scriptingImplementation);
            public const string developmentBuild = nameof(SimulatorBuildOptions.developmentBuild);
            public const string autoConnectProfiler = nameof(SimulatorBuildOptions.autoConnectProfiler);
            public const string deepProfilingSupport = nameof(SimulatorBuildOptions.deepProfilingSupport);
            public const string scriptDebugging = nameof(SimulatorBuildOptions.scriptDebugging);
            public const string buildSizeOptimizations = nameof(SimulatorBuildOptions.buildSizeOptimizations);
        }
#endif
    }
}
