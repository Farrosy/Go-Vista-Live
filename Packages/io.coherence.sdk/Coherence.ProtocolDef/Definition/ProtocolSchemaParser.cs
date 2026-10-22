// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Coherence.Schema;

    public static class ProtocolSchemaParser
    {
        public static ProtocolDefinition Parse(string hash, IEnumerable<SchemaDefinition> schemaDefinitions, bool includeRsSchema)
        {
            var components = new Dictionary<uint, ProtocolComponentDefinition>();
            var commands = new Dictionary<uint, ProtocolCommandDefinition>();
            var inputs = new Dictionary<uint, ProtocolInputDefinition>();
            var archetypes = new Dictionary<uint, ProtocolArchetypeDefinition>();

            var componentIDDictionary = new Dictionary<string, uint>();

            if (includeRsSchema)
            {
                foreach (var (id, component) in ProtocolDefinition.RSSchema.Components)
                {
                    components[id] = component;
                    componentIDDictionary[component.Name] = id;
                }

                foreach (var (id, command) in ProtocolDefinition.RSSchema.Commands)
                {
                    commands[id] = command;
                }

                foreach (var (id, input) in ProtocolDefinition.RSSchema.Inputs)
                {
                    inputs[id] = input;
                }

                foreach (var (id, archetype) in ProtocolDefinition.RSSchema.Archetypes)
                {
                    archetypes[id] = archetype;
                }
            }

            foreach (var schema in schemaDefinitions)
            {
                foreach (var component in schema.ComponentDefinitions)
                {
                    LoadComponent(component, schema.EnumDefinitions, ref components, ref componentIDDictionary);
                }

                foreach (var command in schema.CommandDefinitions)
                {
                    LoadCommand(command, schema.EnumDefinitions, ref commands);
                }

                foreach (var input in schema.InputDefinitions)
                {
                    LoadInput(input, schema.EnumDefinitions, ref inputs);
                }

                foreach (var archetype in schema.ArchetypeDefinitions)
                {
                    LoadArchetype(archetype, ref archetypes, ref components, componentIDDictionary);
                }
            }

            return new ProtocolDefinition(
                hash,
                components,
                commands,
                inputs,
                archetypes);
        }

        private static void LoadComponent(
            ComponentDefinition component,
            List<EnumDefinition> enumDefinitions,
            ref Dictionary<uint, ProtocolComponentDefinition> components,
            ref Dictionary<string, uint> componentIDDictionary)
        {
            var fields = new List<ProtocolFieldDefinition>(component.members.Count);

            foreach (var member in component.members)
            {
                var context = $"component `{component.name}`, field `{member.name}`";
                var hasSimFrames = GetOverrideValue(member.overrides, "sim-frames") == "true";

                var field = new ProtocolFieldDefinition(
                    member.name,
                    member.type,
                    GetFieldMeta(context, member, enumDefinitions),
                    hasSimFrames);

                fields.Add(field);
            }

            var newComp = new ProtocolComponentDefinition(
                (uint)component.id,
                component.name,
                fields,
                GetComponentAttributesFromOverrides($"component `{component.name}`", component.overrides));

            components[newComp.ComponentID] = newComp;
            componentIDDictionary[newComp.Name] = newComp.ComponentID;
        }

        private static void LoadCommand(
            CommandDefinition command,
            List<EnumDefinition> enumDefinitions,
            ref Dictionary<uint, ProtocolCommandDefinition> commands)
        {
            var fields = new List<ProtocolFieldDefinition>(command.members.Count);

            foreach (var member in command.members)
            {
                var context = $"command `{command.name}`, field `{member.name}`";

                var field = new ProtocolFieldDefinition(
                    member.name,
                    member.type,
                    GetFieldMeta(context, member, enumDefinitions),
                    hasSimFrames: false);

                fields.Add(field);
            }

            var newCommand = new ProtocolCommandDefinition(
                (uint)command.id,
                command.name,
                fields,
                command.routing);

            commands[newCommand.CommandID] = newCommand;
        }

        private static void LoadInput(
            InputDefinition input,
            List<EnumDefinition> enumDefinitions,
            ref Dictionary<uint, ProtocolInputDefinition> inputs)
        {
            var fields = new List<ProtocolFieldDefinition>(input.members.Count);

            foreach (var member in input.members)
            {
                var context = $"input `{input.name}`, field `{member.name}`";

                var field = new ProtocolFieldDefinition(
                    member.name,
                    member.type,
                    GetFieldMeta(context, member, enumDefinitions),
                    hasSimFrames: false);

                fields.Add(field);
            }

            var newInput = new ProtocolInputDefinition(
                (uint)input.id,
                input.name,
                fields);

            inputs[newInput.InputID] = newInput;
        }

        private static void LoadArchetype(
            ArchetypeDefinition archetype,
            ref Dictionary<uint, ProtocolArchetypeDefinition> archetypes,
            ref Dictionary<uint, ProtocolComponentDefinition> components,
            IReadOnlyDictionary<string, uint> componentIDDictionary)
        {
            var lods = new List<ProtocolArchetypeLODDefinition>();

            foreach (var lod in archetype.lods)
            {
                var componentReplacements = new Dictionary<uint, uint>();

                foreach (var componentItem in lod.items)
                {
                    if (!components.TryGetValue((uint)componentItem.baseComponentId, out var baseComponent))
                    {
                        throw new Exception($"Base component ID `{componentItem.baseComponentId}` for archetype `{archetype.name}` LOD `{lod.level}` item `{componentItem.name}` not found in schema.");
                    }

                    if (baseComponent.Fields.Count != componentItem.fields.Count)
                    {
                        throw new Exception($"Field count mismatch for archetype `{archetype.name}` LOD `{lod.level}` item `{componentItem.name}`: " +
                            $"base component has {baseComponent.Fields.Count} fields, but override has {componentItem.fields.Count} fields.");
                    }

                    var fields = new List<ProtocolFieldDefinition>(componentItem.fields.Count);

                    for (var index = 0; index < baseComponent.Fields.Count; index++)
                    {
                        var member = componentItem.fields[index];

                        var baseField = baseComponent.Fields[index];
                        if (baseField.Name != member.name)
                        {
                            throw new Exception($"Field name mismatch for archetype `{archetype.name}` LOD `{lod.level}` item `{componentItem.name}`, field index {index}: " +
                                $"base component field name is `{baseField.Name}`, but override field name is `{member.name}`.");
                        }

                        var context = $"archetype component `{componentItem.name}`, field `{member.name}`";
                        var hasSimFrames = GetOverrideValue(member.overrides, "sim-frames") == "true";

                        // Enums use the base field meta, as the enum meta is defined in the enum definition, not per field.
                        var fieldMeta = baseField.Type == SchemaType.Enum ? baseField.Meta : GetFieldMetaFromOverrides(context, baseField.Type, member.overrides);

                        var field = new ProtocolFieldDefinition(
                            member.name,
                            baseField.Type,
                            fieldMeta,
                            hasSimFrames);

                        fields.Add(field);
                    }

                    var newComp = new ProtocolComponentDefinition(
                        (uint)componentItem.id,
                        componentItem.name,
                        fields,
                        new ComponentAttributes(
                            baseComponent.Attributes.SendOrdered,
                            baseComponent.Attributes.ComponentOrder,
                            hasBaseComponent: true,
                            baseComponentID: baseComponent.ComponentID,
                            baseComponent.Attributes.Priority));

                    components[newComp.ComponentID] = newComp;
                    componentReplacements.Add((uint)componentItem.baseComponentId, (uint)componentItem.id);
                }

                var excludedComponents = new List<uint>();

                foreach (var excludedComponentName in lod.excludedComponentNames)
                {
                    if (!componentIDDictionary.TryGetValue(excludedComponentName, out var excludedComponentID))
                    {
                        throw new Exception($"Excluded component `{excludedComponentName}` in archetype `{archetype.name}` LOD `{lod.level}` not found in schema.");
                    }

                    excludedComponents.Add(excludedComponentID);
                }

                var newLod = new ProtocolArchetypeLODDefinition(
                    (uint)lod.level,
                    lod.distance,
                    componentReplacements,
                    excludedComponents.ToArray());

                lods.Add(newLod);
            }

            var newArchetype = new ProtocolArchetypeDefinition(
                (uint)archetype.id,
                archetype.name,
                lods.ToArray());

            archetypes[newArchetype.ArchetypeID] = newArchetype;
        }

        private static FieldMeta GetFieldMeta(string context, FieldDefinition member, List<EnumDefinition> enumDefinitions)
        {
            if (member.type == SchemaType.Enum)
            {
                foreach (var def in enumDefinitions)
                {
                    if (def.name == member.enumName)
                    {
                        return FieldMeta.ForInt(def.min, def.max);
                    }
                }

                throw new Exception($"Enum definition `{member.enumName}` not found in {context}");
            }

            return GetFieldMetaFromOverrides(context, member.type, member.overrides);
        }

        private static FieldMeta GetFieldMetaFromOverrides(string context, SchemaType fieldType, Dictionary<string, string> overrides)
        {
            switch (fieldType)
            {
                case SchemaType.Float:
                case SchemaType.Vector2:
                case SchemaType.Vector3:
                case SchemaType.Color:
                    return GetFloatFieldMetaFromOverrides(context, overrides);
                case SchemaType.Quaternion:
                    return GetQuaternionFieldMetaFromOverrides(context, overrides);
                case SchemaType.Int:
                case SchemaType.Enum:
                    return GetIntFieldMetaFromOverrides(context, overrides);
                case SchemaType.UInt:
                    return GetUIntFieldMetaFromOverrides(context, overrides);
                default:
                    return default;
            }
        }

        private static FieldMeta GetFloatFieldMetaFromOverrides(string context, Dictionary<string, string> overrides)
        {
            var compression = GetOverrideValue(overrides, "compression", defaultVal: FloatCompression.None.ToString());

            if (!Enum.TryParse<FloatCompression>(compression, ignoreCase: true, out var compressionType))
            {
                throw new Exception($"Unknown float compression type: {compression}");
            }

            switch (compressionType)
            {
                case FloatCompression.None:
                    return FieldMeta.ForUncompressedFloat();
                case FloatCompression.Truncated:
                    return FieldMeta.ForTruncatedFloat(int.Parse(GetOverrideValue(overrides, "bits", mandatory: true, context)));
                case FloatCompression.FixedPoint:
                    return FieldMeta.ForFixedPointFloat(
                        GetIntOverrideValue(overrides, "range-min", mandatory: true, context: context),
                        GetIntOverrideValue(overrides, "range-max", mandatory: true, context: context),
                        GetDoubleOverrideValue(overrides, "precision", mandatory: true, context: context));
                default:
                    throw new Exception($"Unsupported float compression type: {compressionType} in {context}");
            }
        }

        private static FieldMeta GetQuaternionFieldMetaFromOverrides(string context, Dictionary<string, string> overrides)
        {
            return FieldMeta.ForBitCount(GetIntOverrideValue(overrides, "bits", context: context, defaultVal: 32));
        }

        private static FieldMeta GetIntFieldMetaFromOverrides(string context, Dictionary<string, string> overrides)
        {
            return FieldMeta.ForInt(
                GetIntOverrideValue(overrides, "range-min", context: context, defaultVal: int.MinValue),
                GetIntOverrideValue(overrides, "range-max", context: context, defaultVal: int.MaxValue));
        }

        private static FieldMeta GetUIntFieldMetaFromOverrides(string context, Dictionary<string, string> overrides)
        {
            return FieldMeta.ForUInt(
                GetUIntOverrideValue(overrides, "range-min", context: context, defaultVal: uint.MinValue),
                GetUIntOverrideValue(overrides, "range-max", context: context, defaultVal: uint.MaxValue));
        }

        private static ComponentAttributes GetComponentAttributesFromOverrides(string context, Dictionary<string, string> overrides)
        {
            var attributes = new ComponentAttributes
            {
                SendOrdered = GetOverrideValue(overrides, "send-ordered") == "true",
                ComponentOrder = GetIntOverrideValue(overrides, "order", context: context, defaultVal: 0),
                HasBaseComponent = overrides.ContainsKey("base-id"),
                BaseComponentID = GetUIntOverrideValue(overrides, "base-id", context: context, defaultVal: 0),
            };

            return attributes;
        }

        private static int GetIntOverrideValue(Dictionary<string, string> overrides, string key, bool mandatory = false, string context = null, int defaultVal = default)
        {
            var valueStr = GetOverrideValue(overrides, key, mandatory, context);
            if (valueStr == null)
            {
                return defaultVal;
            }

            if (!int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new Exception($"Invalid integer `{valueStr}` override value for key: {key}" + (context != null ? " in context: " + context : ""));
            }

            return value;
        }

        private static uint GetUIntOverrideValue(Dictionary<string, string> overrides, string key, bool mandatory = false, string context = null, uint defaultVal = default)
        {
            var valueStr = GetOverrideValue(overrides, key, mandatory, context);
            if (valueStr == null)
            {
                return defaultVal;
            }

            if (!uint.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new Exception($"Invalid unsigned integer `{valueStr}` override value for key: {key}" + (context != null ? " in context: " + context : ""));
            }

            return value;
        }

        private static double GetDoubleOverrideValue(Dictionary<string, string> overrides, string key, bool mandatory = false, string context = null)
        {
            var valueStr = GetOverrideValue(overrides, key, mandatory, context);
            if (valueStr == null)
            {
                return default;
            }

            if (!double.TryParse(valueStr, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
            {
                throw new Exception($"Invalid double `{valueStr}` override value for key: {key}" + (context != null ? " in context: " + context : ""));
            }

            return value;
        }

        private static string GetOverrideValue(Dictionary<string, string> overrides, string key, bool mandatory = false, string context = null, string defaultVal = null)
        {
            if (!overrides.TryGetValue(key, out var value))
            {
                if (mandatory)
                {
                    throw new Exception("Missing override value for key: " + key + (context != null ? " in context: " + context : ""));
                }

                return defaultVal;
            }

            return value;
        }
    }
}
