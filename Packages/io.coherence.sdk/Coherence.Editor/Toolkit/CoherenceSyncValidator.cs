// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Responsible for validating the state of a <see cref="CoherenceSync"/>.
    /// </summary>
    internal static class CoherenceSyncValidator
    {
        private static readonly List<Issue> FoundIssues = new();
        private static readonly List<ArchetypeComponentValidator.Issue> ComponentIssues = new();

        // Keyed by component type; populated lazily and cleared on domain reload (static state).
        private static readonly Dictionary<Type, List<(string Description, string Attribute)>> privateMemberCache = new();

        public static bool HasIssue(CoherenceSync sync, Predicate<Issue> filter)
        {
            if (Validate(sync, new(sync.gameObject), FoundIssues))
            {
                return false;
            }

            try
            {
                foreach (var issue in FoundIssues)
                {
                    if (filter(issue))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                FoundIssues.Clear();
            }
        }

        public static bool Validate(CoherenceSync sync, GameObjectStatus status, List<Issue> foundIssues)
        {
            if (status is { IsAsset: false, IsInPrefabStage: false, IsInstanceInScene: false } && !Application.isPlaying)
            {
                foundIssues.Add(Issue.NotConnectedToPrefab);
            }

            // Without this ToolkitArchetype.BoundComponents might not be populated correctly
            sync.ValidateArchetype();
            foreach (var boundComponent in sync.Archetype.BoundComponents)
            {
                if (ArchetypeComponentValidator.Validate(boundComponent, ComponentIssues))
                {
                    continue;
                }

                foreach (var componentIssue in ComponentIssues)
                {
                    var component = boundComponent.Component;
#pragma warning disable CS8524
                    foundIssues.Add(componentIssue.Type switch
#pragma warning restore CS8524
                    {
                        ArchetypeComponentValidator.IssueType.TooManySyncedVariables
                            => Issue.TooManySyncedVariables(component, (int)componentIssue.SyncedVariableCount),
                    });
                }

                ComponentIssues.Clear();
            }

            if (CoherenceSyncConfigValidator.Validate(sync.CoherenceSyncConfig) is { } configIssue)
            {
#pragma warning disable CS8524
                foundIssues.Add(configIssue.Type switch
#pragma warning restore CS8524
                {
                    CoherenceSyncConfigValidator.IssueType.SyncIsAddressableButConfigNot
                        => Issue.SyncIsAddressableButConfigNot(configIssue.PrefabName, configIssue.AddressableGroupName, configIssue.ConfigName),
                });
            }

            foreach (var component in sync.GetComponents<MonoBehaviour>())
            {
                if (component == null)
                {
                    continue;
                }

                var componentType = component.GetType();

                if (!privateMemberCache.TryGetValue(componentType, out var cachedMembers))
                {
                    cachedMembers = FindNonPublicSyncedMembers(componentType);
                    privateMemberCache[componentType] = cachedMembers;
                }

                foreach (var (description, attribute) in cachedMembers)
                {
                    foundIssues.Add(Issue.PrivateSyncedMember(description, attribute));
                }
            }

            return foundIssues.Count is 0;
        }

        private static List<(string Description, string Attribute)> FindNonPublicSyncedMembers(Type componentType)
        {
            var result = new List<(string, string)>();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            for (var type = componentType; type != null && type != typeof(MonoBehaviour) && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(flags))
                {
                    if (!field.IsPublic && field.IsDefined(typeof(SyncAttribute)))
                    {
                        result.Add(($"Field {type.Name}.{field.Name}", "[Sync]"));
                    }
                }

                foreach (var prop in type.GetProperties(flags))
                {
                    var getter = prop.GetGetMethod(nonPublic: true);
                    var setter = prop.GetSetMethod(nonPublic: true);
                    var isPublic = (getter != null && getter.IsPublic) || (setter != null && setter.IsPublic);
                    if (!isPublic && prop.IsDefined(typeof(SyncAttribute), true))
                    {
                        result.Add(($"Property {type.Name}.{prop.Name}", "[Sync]"));
                    }
                }

                foreach (var method in type.GetMethods(flags))
                {
                    if (!method.IsPublic && method.IsDefined(typeof(CommandAttribute)))
                    {
                        result.Add(($"Method {type.Name}.{method.Name}", "[Command]"));
                    }
                }
            }

            return result;
        }

        public static void DrawIssueHelpBoxes(CoherenceSync sync, SerializedObject serializedObject, GameObjectStatus status)
        {
            if (Validate(sync, status, FoundIssues))
            {
                return;
            }

            try
            {
                foreach (var issue in FoundIssues)
                {
                    if (issue is { Type: IssueType.ConfigNotAddressable })
                    {
                        continue;
                    }

                    DrawIssueHelpBox(issue, serializedObject, status);
                }
            }
            finally
            {
                FoundIssues.Clear();
            }
        }

        private static void DrawIssueHelpBox(Issue issue, SerializedObject serializedObject, GameObjectStatus status) => CoherenceSyncEditor.DrawHelpBox(issue.ToString(), issue.MessageType);

        internal readonly struct Issue
        {
            public readonly IssueType Type;
            public readonly MessageType MessageType;
            private readonly object[] args;

            public Component Component => Type is IssueType.TooManySyncedVariables ? (Component)args[0] : null;
            public int? SyncedVariableCount => Type is IssueType.TooManySyncedVariables ? (int)args[1] : null;
            public string PrefabName => Type is IssueType.ConfigNotAddressable ? (string)args[0] : null;
            public string AddressableGroupName => Type is IssueType.ConfigNotAddressable ? (string)args[1] : null;
            public string ConfigName => Type is IssueType.ConfigNotAddressable ? (string)args[2] : null;
            public string PrivateMemberDescription => Type is IssueType.PrivateSyncedMember ? (string)args[0] : null;
            public string PrivateMemberAttribute => Type is IssueType.PrivateSyncedMember ? (string)args[1] : null;

            private Issue(IssueType type, MessageType messageType = MessageType.Error, params object[] args)
            {
                Type = type;
                MessageType = messageType;
                this.args = args;
            }

            public static Issue TooManySyncedVariables(Component component, int syncedVariableCount)
                => new(IssueType.TooManySyncedVariables, MessageType.Error, component, syncedVariableCount);

            public static Issue NotConnectedToPrefab => new(IssueType.NotConnectedToPrefab);

            public static Issue SyncIsAddressableButConfigNot(string prefabName, string syncAssetGroup, string configName) => new(IssueType.ConfigNotAddressable, MessageType.Warning, prefabName, syncAssetGroup, configName);

            public static Issue PrivateSyncedMember(string memberDescription, string attributeName) =>
                new(IssueType.PrivateSyncedMember, MessageType.Warning, memberDescription, attributeName);

#pragma warning disable CS8524
            public override string ToString() => Type switch
#pragma warning restore CS8524
            {
                IssueType.TooManySyncedVariables => $"The {Component?.GetType().Name} component has {SyncedVariableCount} synced variables. This exceeds the maximum of {ArchetypeComponentValidator.MaxSyncedVariablesPerComponent} per component.",
                IssueType.NotConnectedToPrefab => "CoherenceSync can only be used to network prefabs.",
                IssueType.ConfigNotAddressable => $"The prefab '{PrefabName}' belongs to the addressable group '{AddressableGroupName}' but the CoherenceSyncConfig '{ConfigName}' does not belong to any group. The CoherenceSyncConfig should be added to a group to prevent duplicate instances of getting loaded if multiple assets belonging to different addressable groups reference it.",
                IssueType.PrivateSyncedMember => $"{PrivateMemberDescription} is marked with {PrivateMemberAttribute} but is not public and will be ignored at runtime. Make it public or remove the attribute.",
                default(IssueType) => "None"
            };
        }

        internal enum IssueType
        {
            TooManySyncedVariables = 1,
            NotConnectedToPrefab = 2,
            ConfigNotAddressable = 3,
            PrivateSyncedMember = 4,
        }
    }
}
