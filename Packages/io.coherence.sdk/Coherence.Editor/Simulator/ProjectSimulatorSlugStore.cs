// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using UnityEditor;
    using UnityEngine;

    internal sealed class ProjectSimulatorSlugStore : ScriptableObject
    {
        [SerializeField] private Item[] projectSlugs = Array.Empty<Item>();
        private static ProjectSimulatorSlugStore instance;

        private static ProjectSimulatorSlugStore GetInstance()
        {
            if (instance)
            {
                return instance;
            }

            instance = AssetDatabase.LoadAssetAtPath<ProjectSimulatorSlugStore>(Paths.simulatorProjectSlugsPath);
            return instance ? instance : null;
        }

        public static void SetUseSimulator(string key, bool useSimulator, string defaultSimulatorSlug)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var instance = GetOrCreateInstance();
            using var serializedObject = new SerializedObject(instance);
            using var projectSlugsProperty = serializedObject.FindProperty(nameof(projectSlugs));
            var index = Array.FindIndex(instance.projectSlugs, x => string.Equals(x.ProjectId, key));

            if(index is not -1)
            {
                var existing = instance.projectSlugs[index];
                if (existing.UseSimulator == useSimulator && !string.IsNullOrEmpty(existing.SimulatorSlug))
                {
                    return;
                }

                using var elementProperty = projectSlugsProperty.GetArrayElementAtIndex(index);
                using var useSimulatorProperty = elementProperty.FindPropertyRelative(nameof(Item.UseSimulator));
                useSimulatorProperty.boolValue = useSimulator;
                if (string.IsNullOrEmpty(existing.SimulatorSlug))
                {
                    using var simulatorSlugProperty = elementProperty.FindPropertyRelative(nameof(Item.SimulatorSlug));
                    simulatorSlugProperty.stringValue = defaultSimulatorSlug;
                }
            }
            else
            {
                index = projectSlugsProperty.arraySize;
                projectSlugsProperty.InsertArrayElementAtIndex(index);
                using var elementProperty = projectSlugsProperty.GetArrayElementAtIndex(index);
                using var projectIdProperty = elementProperty.FindPropertyRelative(nameof(Item.ProjectId));
                using var useSimulatorProperty = elementProperty.FindPropertyRelative(nameof(Item.UseSimulator));
                using var simulatorSlugProperty = elementProperty.FindPropertyRelative(nameof(Item.SimulatorSlug));
                projectIdProperty.stringValue = key;
                useSimulatorProperty.boolValue = useSimulator;
                simulatorSlugProperty.stringValue = defaultSimulatorSlug;
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                Save();
            }
        }

        public static void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            value ??= "";
            var instance = GetOrCreateInstance();
            using var serializedObject = new SerializedObject(instance);
            using var projectSlugsProperty = serializedObject.FindProperty(nameof(projectSlugs));
            var index = Array.FindIndex(instance.projectSlugs, x => string.Equals(x.ProjectId, key));
            if (index is not -1)
            {
                var existing = instance.projectSlugs[index];
                if (string.Equals(existing.SimulatorSlug, value)
                    && existing.UseSimulator == value.Length > 0)
                {
                    return;
                }

                using var elementProperty = projectSlugsProperty.GetArrayElementAtIndex(index);
                using var useSimulatorProperty = elementProperty.FindPropertyRelative(nameof(Item.UseSimulator));
                if (value.Length is 0)
                {
                    useSimulatorProperty.boolValue = false;
                }
                else
                {
                    using var simulatorSlugProperty = elementProperty.FindPropertyRelative(nameof(Item.SimulatorSlug));
                    simulatorSlugProperty.stringValue = value;
                    useSimulatorProperty.boolValue = true;
                }
            }
            else
            {
                index = projectSlugsProperty.arraySize;
                projectSlugsProperty.InsertArrayElementAtIndex(index);
                using var elementProperty = projectSlugsProperty.GetArrayElementAtIndex(index);
                using var projectIdProperty = elementProperty.FindPropertyRelative(nameof(Item.ProjectId));
                using var useSimulatorProperty = elementProperty.FindPropertyRelative(nameof(Item.UseSimulator));
                using var simulatorSlugProperty = elementProperty.FindPropertyRelative(nameof(Item.SimulatorSlug));
                projectIdProperty.stringValue = key;
                useSimulatorProperty.boolValue = value.Length > 0;
                simulatorSlugProperty.stringValue = value;
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                Save();
            }
        }

        public static bool GetUseSimulator([MaybeNull] string key)
        {
            if (string.IsNullOrEmpty(key) || GetInstance()?.projectSlugs is not { } projectSlugs)
            {
                return false;
            }

            foreach (var projectSlug in projectSlugs)
            {
                if (string.Equals(projectSlug.ProjectId, key))
                {
                    return projectSlug.UseSimulator;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the stored slug for the given key.
        /// </summary>
        /// <param name="key">A valid key.</param>
        /// <returns>The slug for the given key.
        /// If the key is <c>null</c> or cannot be found then a <c>null</c> is returned.</returns>
        [return: MaybeNull]
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key) || GetInstance()?.projectSlugs is not { } projectSlugs)
            {
                return null;
            }

            foreach (var projectSlug in projectSlugs)
            {
                if (string.Equals(projectSlug.ProjectId, key))
                {
                    return projectSlug.UseSimulator ? projectSlug.SimulatorSlug : null;
                }
            }

            return null;
        }

        private static void Save()
        {
            if (instance)
            {
                AssetDatabase.SaveAssetIfDirty(instance);
            }
        }

        /// <summary>
        /// Keep only the keys that match the filter.
        /// </summary>
        /// <param name="filter">Filter containing the keys to keep.</param>
        public static void KeepOnly(Predicate<string> filter)
        {
            if (GetInstance() is not { } instance)
            {
                return;
            }

            var filtered = instance.projectSlugs.Where(x => filter(x.ProjectId)).ToArray();
            if (filtered.Length == instance.projectSlugs.Length)
            {
                return;
            }

            instance.projectSlugs = filtered;
            EditorUtility.SetDirty(instance);
            Save();
        }

        private static ProjectSimulatorSlugStore GetOrCreateInstance()
        {
            if (GetInstance() is { } existing)
            {
                return existing;
            }

            instance = CreateInstance<ProjectSimulatorSlugStore>();

            if (File.Exists(Paths.simulatorProjectSlugsPathLegacy))
            {
                try
                {
                    var json = File.ReadAllText(Paths.simulatorProjectSlugsPathLegacy);
                    if (JsonConvert.DeserializeObject<Dictionary<string, string>>(json) is { } loadedSlugs)
                    {
                        instance.projectSlugs = loadedSlugs.Select(x => new Item(x.Key, x.Value)).ToArray();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                AssetUtils.DeleteFile(Paths.simulatorProjectSlugsPathLegacy);
            }

            var folderPath = Path.GetDirectoryName(Paths.simulatorProjectSlugsPath);
            if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
            {
                AssetUtils.CreateFolder(folderPath);
            }

            AssetDatabase.CreateAsset(instance, Paths.simulatorProjectSlugsPath);
            return instance;
        }

        [Serializable]
        private struct Item
        {
            public string ProjectId;
            public bool UseSimulator;
            public string SimulatorSlug;

            public Item(string projectId, string simulatorSlug)
            {
                ProjectId = projectId;
                SimulatorSlug = simulatorSlug;
                UseSimulator = !string.IsNullOrEmpty(simulatorSlug);
            }
        }
    }
}
