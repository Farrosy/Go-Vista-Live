
// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class IdOffsets
    {
        public int ComponentId = -1;
        public int CommandId = -1;
        public int ArchetypeId = -1;
        public int InputId = -1;
    }

    public struct EntitiesBakeData
    {
        public Dictionary<string, string> InputData;
        public List<SyncedBehaviour> BehaviourData;
    }

    // Used for json generation -- corresponds to one CoherenceSyncNAME being generated.
    // Note: The member names have some stuttering to make the json readable on its own
    public struct SyncedBehaviour
    {
        public string BehaviourName;
        public string UnmangledBehaviourName;
        public string AssetId;
        public bool IsGlobal;
        public SyncedComponent[] Components;
        public CommandDescription[] Commands;

        public SyncedBehaviour(string name,
            string unmangledName,
            string assetId,
            bool isGlobal,
            SyncedComponent[] components,
            CommandDescription[] commands)
        {
            BehaviourName = name;
            UnmangledBehaviourName = unmangledName;
            AssetId = assetId;
            IsGlobal = isGlobal;
            Components = components;
            Commands = commands;
        }
    }

    public class ComponentMember
    {
        public string MemberNameInComponentData;
        public string MemberNameInUnityComponent;
        public string BindingName;
        public string BindingGuid;
        public string BindingClassName;
        public string PropertyGetter;
        public string PropertySetter;
        public string PropertyCSharpType;
        public string SchemaType;
        public int FieldIndex;
        public bool HasSimFrames;
    }

    // Used for json generation
    // Note: The member names have some stuttering to make the json readable on its own
    public class SyncedComponent
    {
        public string
            ComponentName; // Name of the ECS component that the sync script will sync the MonoBehaviour:s data with

        public int ComponentID = -1;
        public bool NeedCachedProperty; // Will generate a _componentName reference in the sync script
        public string Property; // Name of the property to access the MonoBehaviour via, e.g. 'transform'
        public string PropertyType; // Type of the property to access the MonoBehaviour via, e.g. 'transform'
        public string UnityComponentType; // Type of the Unity component sync'd
        public bool OverrideSetter;
        public bool OverrideGetter;
        public int FieldMasks;
        public string FieldMasksString => Convert.ToString(FieldMasks, 2).PadLeft(32, '0');
        public List<ComponentMember> MembersInfo;
        public string BakeConditional; // Inserts #if {BakeConditional} into baked code

        public SyncedComponent(string name, bool needsInitializer, string property, string propertyType,
            string unityComponentType, bool overrideSetter, bool overrideGetter, List<ComponentMember> membersInfo,
            string bakeConditional)
        {
            ComponentName = name;
            NeedCachedProperty = needsInitializer;
            Property = property;
            PropertyType = propertyType;
            UnityComponentType = unityComponentType;
            OverrideSetter = overrideSetter;
            OverrideGetter = overrideGetter;
            MembersInfo = membersInfo;
            BakeConditional = bakeConditional;
        }
    }

    public class CommandDescription
    {
        public int CommandID;
        public string CommandName;
        public string MethodName;
        public string MethodDeclaringClass;
        public string BindingName;
        public string BindingGuid;
        public string Routing;
        public List<CommandParameterInfo> ParametersInfo;
        public string BakeConditional;
        public string Signature;
        public List<string> ClassInterfaces;

        // Extension method generation fields (may differ from MethodDeclaringClass for generic types)
        public string ExtensionMethodDeclaringClass;
        public List<string> ExtensionMethodTypeParams;
        public List<CommandParameterInfo> ExtensionMethodParametersInfo;
        public string ExtensionMethodSignature;

        public CommandDescription(string name,
            string methodName,
            string declaringClass,
            string bindingName,
            string bindingGuid,
            string routing,
            List<CommandParameterInfo> parameters,
            string bakeConditional,
            List<string> interfaces)
        {
            CommandName = name;
            MethodName = methodName;
            MethodDeclaringClass = declaringClass;
            BindingName = bindingName;
            BindingGuid = bindingGuid;
            Routing = routing;
            ParametersInfo = parameters;
            BakeConditional = bakeConditional;
            ClassInterfaces = interfaces;
            Signature = BuildSignature(parameters);

            static string BuildSignature(List<CommandParameterInfo> parameters)
            {
                var sb = new StringBuilder(parameters.Count * 16);
                sb.Append('(');

                foreach (var info in parameters)
                {
                    sb.Append(info.Type);
                }

                sb.Append(')');
                return sb.ToString();
            }
        }
    }

    public class CommandParameterInfo
    {
        public string Name;
        public string BakedType;
        public string Type;
        public bool IsEnum;
        public string SchemaType;
    }
}
