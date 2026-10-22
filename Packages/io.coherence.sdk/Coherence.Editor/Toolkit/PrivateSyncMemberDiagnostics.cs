// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System.Collections.Generic;
    using System.Reflection;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Logs warnings after every assembly reload for MonoBehaviour fields and methods that are marked
    /// with [Sync] or [Command] but are private, making the warnings persistent in the console
    /// until the issue is fixed (similar to compilation errors).
    /// </summary>
    [InitializeOnLoad]
    internal static class PrivateSyncMemberDiagnostics
    {
        static PrivateSyncMemberDiagnostics()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnAfterAssemblyReload()
        {
            LogPrivateMemberWarnings(
                TypeCache.GetFieldsWithAttribute<SyncAttribute>(),
                TypeCache.GetMethodsWithAttribute<CommandAttribute>());
        }

        internal static void LogPrivateMemberWarnings(IEnumerable<FieldInfo> fieldsWithSync, IEnumerable<MethodInfo> methodsWithCommand)
        {
            foreach (var field in fieldsWithSync)
            {
                if (field.IsPublic || field.DeclaringType == null ||
                    !typeof(MonoBehaviour).IsAssignableFrom(field.DeclaringType))
                {
                    continue;
                }

                Debug.LogWarning($"(coherence) Field {field.DeclaringType.Name}.{field.Name} is marked with [Sync] but is not public and will be ignored at runtime. Make it public or remove [Sync].");
            }

            foreach (var method in methodsWithCommand)
            {
                if (method.IsPublic || method.DeclaringType == null ||
                    !typeof(MonoBehaviour).IsAssignableFrom(method.DeclaringType))
                {
                    continue;
                }

                Debug.LogWarning($"(coherence) Method {method.DeclaringType.Name}.{method.Name} is marked with [Command] but is not public and will be ignored at runtime. Make it public or remove [Command].");
            }
        }
    }
}
