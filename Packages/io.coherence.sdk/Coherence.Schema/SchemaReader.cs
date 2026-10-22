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
    using System.Globalization;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using Coherence.Utils;

#if !UNITY
    using DictionaryOfStringString = System.Collections.Generic.Dictionary<string, string>;
#endif

    public static class SchemaReader
    {
        public static SchemaDefinition Read(string schemaText)
        {
            // Normalize line endings so the schema ID (a hash of the raw text) is invariant to
            // CRLF/LF differences between platforms and bake paths. Without this, a CRLF-saved
            // schema file hashes differently here than via the normalizing editor path
            // (Schema.GetFromString), desyncing the baked client SchemaID from the RS/portal.
            schemaText = schemaText.Replace("\r\n", "\n");

            AntlrInputStream antlerStream = new AntlrInputStream(schemaText);
            var lexer = new SchemaLexer(antlerStream);
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            SchemaParser parser = new SchemaParser(tokenStream);

            var listener = new SchemaListener();
            ParseTreeWalker.Default.Walk(listener, parser.schema());

            listener.schemaDefinition.SchemaId = HashCalc.SHA1Hash(schemaText);

            return listener.schemaDefinition;
        }
    }

    public class SchemaListener : SchemaBaseListener
    {
        public SchemaDefinition schemaDefinition = new();

        /// <summary>
        /// Removes the surrounding quotes from a quoted string.
        /// </summary>
        private static ReadOnlySpan<char> Sanitize(ReadOnlySpan<char> input) => input[1..^1];

        public override void EnterArchetype(SchemaParser.ArchetypeContext context)
        {
            var archetype = new ArchetypeDefinition(context.ident().GetText(), new List<ArchetypeLOD>());

            var id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }
                }
            }

            archetype.id = id;
            ArchetypeLOD highestLod = null;

            foreach (var lodContext in context.lod())
            {
                var level = lodContext.INT().GetText();
                var distance = lodContext.lod_distance() != null
                    ? Sanitize(lodContext.lod_distance().STRING().GetText())
                    : "0";

                var lod = new ArchetypeLOD(int.Parse(level, NumberStyles.Integer, CultureInfo.InvariantCulture), float.Parse(distance, NumberStyles.Float, CultureInfo.InvariantCulture));

                foreach (var componentContext in lodContext.component_override())
                {
                    var item = new ArchetypeLODItem(componentContext.ident().GetText());
                    var componentOverrideId = 0;
                    var baseComponentId = 0;

                    if (componentContext.@override() != null)
                    {
                        foreach (var overrContext in componentContext.@override().pair())
                        {
                            if (overrContext.ident().GetText().Equals("id"))
                            {
                                componentOverrideId = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                            }
                            else if (overrContext.ident().GetText().Equals("base-id"))
                            {
                                baseComponentId = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                            }
                        }
                    }

                    item.id = componentOverrideId;
                    item.baseComponentId = baseComponentId;

                    foreach (var fieldContext in componentContext.field_override())
                    {
                        var overrides = new DictionaryOfStringString();
                        if (fieldContext.@override() != null)
                        {
                            foreach (var overr in fieldContext.@override().pair())
                            {
                                overrides.Add(overr.ident().GetText(), Sanitize(overr.STRING().GetText()).ToString());
                            }
                        }

                        item.fields.Add(new ArchetypeLODItemField(fieldContext.ident().GetText(), overrides));
                    }

                    lod.items.Add(item);
                }

                FindExcludedComponents(lod, ref highestLod);

                archetype.lods.Add(lod);
            }

            schemaDefinition.ArchetypeDefinitions.Add(archetype);

            base.EnterArchetype(context);
        }

        private void FindExcludedComponents(ArchetypeLOD lod, ref ArchetypeLOD highestLod)
        {
            if (lod.level == 0)
            {
                highestLod = lod;
            }
            else
            {
                foreach (var component in highestLod.items)
                {
                    bool foundInCurrentLod = lod.items.Any(componentInCurrent =>
                        component.name.Equals(componentInCurrent.name));

                    if (!foundInCurrentLod)
                    {
                        lod.excludedComponentNames.Add(component.name);
                    }
                }
            }
        }

        public override void EnterComponent(SchemaParser.ComponentContext context)
        {
            var component = new ComponentDefinition(context.ident().GetText());
            var componentOverrides = new DictionaryOfStringString();

            var id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        componentOverrides.Add(overrContext.ident().GetText(), Sanitize(overrContext.STRING().GetText()).ToString());
                    }
                }
            }

            component.id = id;
            component.overrides = componentOverrides;

            foreach (var field in context.field())
            {
                var name = field.ident().GetText();
                var type = field.field_type().GetText();
                string enumName = null;

                if (!Enum.TryParse<SchemaType>(type, out var schemaType))
                {
                    // If we don't know the type, fallback to Enum
                    schemaType = SchemaType.Enum;
                    enumName = type;
                }

                var overrides = new DictionaryOfStringString();
                if (field.@override() != null)
                {
                    foreach (var overr in field.@override().pair())
                    {
                        overrides.Add(overr.ident().GetText(), Sanitize(overr.STRING().GetText()).ToString());
                    }
                }

                var member = new FieldDefinition(
                    name,
                    schemaType,
                    enumName,
                    overrides);
                component.members.Add(member);
            }

            schemaDefinition.ComponentDefinitions.Add(component);
            base.EnterComponent(context);
        }

        public override void EnterCommand(SchemaParser.CommandContext context)
        {
            var command = new CommandDefinition(context.ident().GetText());

            var routing = "All";
            var id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("routing"))
                    {
                        routing = Sanitize(overrContext.STRING().GetText()).ToString();
                    }

                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }
                }
            }

            command.id = id;
            command.routing = Enum.Parse<MessageTarget>(routing);

            foreach (var fieldContext in context.field())
            {
                var name = fieldContext.ident().GetText();
                var type = fieldContext.field_type().GetText();
                string enumName = null;

                if (!Enum.TryParse<SchemaType>(type, out var schemaType))
                {
                    // If we don't know the type, fallback to Enum
                    schemaType = SchemaType.Enum;
                    enumName = type;
                }

                var overrides = new DictionaryOfStringString();
                if (fieldContext.@override() != null)
                {
                    foreach (var overr in fieldContext.@override().pair())
                    {
                        overrides.Add(overr.ident().GetText(), Sanitize(overr.STRING().GetText()).ToString());
                    }
                }

                var member = new FieldDefinition(
                    name,
                    schemaType,
                    enumName,
                    overrides);
                command.members.Add(member);
            }

            schemaDefinition.CommandDefinitions.Add(command);

            base.EnterCommand(context);
        }

        public override void EnterInput(SchemaParser.InputContext context)
        {
            InputDefinition input = new InputDefinition(context.ident().GetText(), new List<FieldDefinition>());

            int id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }
                }
            }

            input.id = id;

            foreach (var fieldContext in context.field())
            {
                var name = fieldContext.ident().GetText();
                var type = fieldContext.field_type().GetText();
                string enumName = null;

                if (!Enum.TryParse<SchemaType>(type, out var schemaType))
                {
                    // If we don't know the type, fallback to Enum
                    schemaType = SchemaType.Enum;
                    enumName = type;
                }

                var overrides = new DictionaryOfStringString();
                if (fieldContext.@override() != null)
                {
                    foreach (var overr in fieldContext.@override().pair())
                    {
                        overrides.Add(overr.ident().GetText(), Sanitize(overr.STRING().GetText()).ToString());
                    }
                }

                var member = new FieldDefinition(
                    name,
                    schemaType,
                    enumName,
                    overrides);
                input.members.Add(member);
            }

            schemaDefinition.InputDefinitions.Add(input);

            base.EnterInput(context);
        }

        public override void EnterEnum(SchemaParser.EnumContext context)
        {
            var enumName = context.ident().GetText();

            int bits = 0;
            int rangeMin = 0;
            int rangeMax = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("bits"))
                    {
                        bits = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }

                    if (overrContext.ident().GetText().Equals("range-min"))
                    {
                        rangeMin = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }

                    if (overrContext.ident().GetText().Equals("range-max"))
                    {
                        rangeMax = int.Parse(Sanitize(overrContext.STRING().GetText()), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }
                }
            }

            var names = new List<string>();
            var values = new List<string>();

            foreach (var field in context.enum_field())
            {
                names.Add(field.ident().GetText());
                values.Add(field.enum_value().GetText());
            }

            schemaDefinition.EnumDefinitions.Add(new EnumDefinition(enumName, names.ToArray(), values.ToArray(), bits, rangeMin, rangeMax));

            base.EnterEnum(context);
        }
    }
}
