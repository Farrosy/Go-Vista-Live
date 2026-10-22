// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEngine;

    internal static class BuildTargetIconUtil
    {
        private static readonly Type BuildPlatformsType =
            typeof(Editor).Assembly.GetType("UnityEditor.Build.BuildPlatforms");

        private static readonly object Instance =
            BuildPlatformsType?.GetProperty("instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

        private static readonly MethodInfo GetPlatform =
            BuildPlatformsType?.GetMethod("BuildPlatformFromNamedBuildTarget",
                BindingFlags.Public | BindingFlags.Instance);

        private static readonly PropertyInfo Title =
            GetPlatform?.ReturnType.GetProperty("title",
                BindingFlags.Public | BindingFlags.Instance);

        private static readonly PropertyInfo SmallIcon =
            GetPlatform?.ReturnType.GetProperty("smallIcon",
                BindingFlags.Public | BindingFlags.Instance);

        // compoundSmallIcon combines the base platform icon with any derived platform
        // icons (e.g. Standalone + Dedicated Server shown side by side).
        private static readonly PropertyInfo CompoundSmallIcon =
            GetPlatform?.ReturnType.GetProperty("compoundSmallIcon",
                BindingFlags.Public | BindingFlags.Instance);

        public static GUIContent GetContent(NamedBuildTarget target)
        {
            if (Instance == null || GetPlatform == null || Title == null)
            {
                return null;
            }

            var platform = GetPlatform.Invoke(Instance, new object[] { target });
            return platform == null ? null : Title.GetValue(platform) as GUIContent;
        }

        public static GUIContent GetContent(BuildTargetGroup group) =>
            GetContent(NamedBuildTarget.FromBuildTargetGroup(group));

        public static Texture2D GetSmallIcon(NamedBuildTarget target)
        {
            if (Instance == null || GetPlatform == null || SmallIcon == null)
            {
                return null;
            }

            var platform = GetPlatform.Invoke(Instance, new object[] { target });
            return platform == null ? null : SmallIcon.GetValue(platform) as Texture2D;
        }

        public static Texture2D GetSmallIcon(BuildTargetGroup group) =>
            GetSmallIcon(NamedBuildTarget.FromBuildTargetGroup(group));

        public static Texture2D GetCompoundSmallIcon(NamedBuildTarget target)
        {
            if (Instance == null || GetPlatform == null || CompoundSmallIcon == null)
            {
                return null;
            }

            var platform = GetPlatform.Invoke(Instance, new object[] { target });
            return platform == null ? null : CompoundSmallIcon.GetValue(platform) as Texture2D;
        }

        public static Texture2D GetCompoundSmallIcon(BuildTargetGroup group) =>
            GetCompoundSmallIcon(NamedBuildTarget.FromBuildTargetGroup(group));

        public static string GetTitle(NamedBuildTarget target) => GetContent(target)?.text;

        public static string GetTitle(BuildTargetGroup group) =>
            GetTitle(NamedBuildTarget.FromBuildTargetGroup(group));
    }
}
