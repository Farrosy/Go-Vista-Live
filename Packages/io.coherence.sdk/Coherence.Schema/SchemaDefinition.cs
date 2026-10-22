// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

#if !UNITY
    using DictionaryOfStringString = System.Collections.Generic.Dictionary<string, string>;
#endif

    [Serializable]
    public class SchemaDefinition
    {
        public string SchemaId;
        public List<ComponentDefinition> ComponentDefinitions = new();
        public List<CommandDefinition> CommandDefinitions = new();
        public List<ArchetypeDefinition> ArchetypeDefinitions = new();
        public List<InputDefinition> InputDefinitions = new();
        public List<EnumDefinition> EnumDefinitions = new();
    }

    [Serializable]
    public abstract class BaseDefinition
    {
        public int id;
        public string name;

        protected BaseDefinition(string name)
        {
            this.name = name;
        }
    }

    [Serializable]
    public class ComponentDefinition : BaseDefinition
    {
        public List<FieldDefinition> members;
        public DictionaryOfStringString overrides = new();

        public int baseComponentId;
        public string bakeConditional;

        public ComponentDefinition(string name, string bakeConditional = "") : base(name)
        {
            members = new List<FieldDefinition>();
            this.bakeConditional = bakeConditional;
        }
    }

    [Serializable]
    public class CommandDefinition : BaseDefinition
    {
        public List<FieldDefinition> members;
        public MessageTarget routing;
        public string bakeConditional;

        public CommandDefinition(string name, string bakeConditional = "")
            : this(name, new List<FieldDefinition>(), MessageTarget.All, bakeConditional)
        {
        }

        public CommandDefinition(string name, List<FieldDefinition> members, MessageTarget routing, string bakeConditional) : base(name)
        {
            this.members = members;
            this.routing = routing;
            this.bakeConditional = bakeConditional;
        }
    }

    [Serializable]
    public class InputDefinition : BaseDefinition
    {
        public List<FieldDefinition> members;

        public InputDefinition(string name, List<FieldDefinition> members) : base(name)
        {
            this.members = members;
        }
    }

    [Serializable]
    public class FieldDefinition
    {
        public string name;
        public SchemaType type;
        public string enumName;
        public DictionaryOfStringString overrides;

        public string TypeName => type == SchemaType.Enum ? enumName : type.ToString();

        public FieldDefinition(string name,
            SchemaType type,
            string enumName,
            DictionaryOfStringString overrides)
        {
            this.name = name;
            this.type = type;
            this.enumName = enumName;
            this.overrides = overrides;
        }
    }

    [Serializable]
    public class EnumDefinition : BaseDefinition
    {
        public readonly string[] names;
        public readonly string[] values;
        public int bits;
        public int min;
        public int max;

        public EnumDefinition(string name, string[] names, string[] values, int bits, int min, int max) : base(name)
        {
            this.names = names;
            this.values = values;
            this.bits = bits;
            this.min = min;
            this.max = max;
        }
    }

    [Serializable]
    public class ArchetypeDefinition : BaseDefinition
    {
        public List<ArchetypeLOD> lods;

        public ArchetypeDefinition(string name, List<ArchetypeLOD> lods) : base(name)
        {
            this.lods = lods;
        }
    }

    [Serializable]
    public class ArchetypeLOD
    {
        public int level;
        public List<ArchetypeLODItem> items;
        public float distance;
        public List<string> excludedComponentNames;

        public ArchetypeLOD(int level, float distance)
        {
            this.level = level;
            this.distance = distance;
            items = new List<ArchetypeLODItem>();
            excludedComponentNames = new List<string>();
        }
    }

    [Serializable]
    public class ArchetypeLODItem
    {
        public int id;
        public string name;
        public int baseComponentId;
        public List<ArchetypeLODItemField> fields;

        public ArchetypeLODItem(string name)
        {
            this.name = name;
            fields = new List<ArchetypeLODItemField>();
        }
    }

    [Serializable]
    public class ArchetypeLODItemField
    {
        public string name;
        public DictionaryOfStringString overrides;

        public ArchetypeLODItemField(string name, DictionaryOfStringString overrides)
        {
            this.name = name;
            this.overrides = overrides;
        }
    }
}
