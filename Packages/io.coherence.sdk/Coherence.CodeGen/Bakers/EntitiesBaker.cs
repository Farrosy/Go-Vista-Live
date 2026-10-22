namespace Coherence.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Coherence.Schema;
    using Scriban.Runtime;

    public class EntitiesBaker : IBaker
    {
        public BakeResult Bake(CodeGenData codeGenData)
        {
            if (codeGenData.NoUnityReferences)
            {
                return new BakeResult
                {
                    Success = true,
                    GeneratedFiles = new HashSet<string>(),
                };
            }

            var entitiesData = codeGenData.EntitiesData;
            var result = true;
            var files = new HashSet<string>();
            var scribanWriter = new ScribanWriter();

            foreach (var syncedBehaviour in entitiesData.BehaviourData)
            {
                var scribanOptions = GetOptions();

                if (codeGenData.EntitiesData.InputData.TryGetValue(syncedBehaviour.BehaviourName, out var inputName))
                {
                    foreach (var input in codeGenData.SchemaDefinition.InputDefinitions.Where(input =>
                                 input.name.Equals(inputName)))
                    {
                        scribanOptions.Model[0]["inputDefinition"] = input;
                        break;
                    }
                }

                scribanOptions.Model[0]["entityData"] = syncedBehaviour;

                var renderResult = scribanWriter.Render(scribanOptions, codeGenData.OutputDirectory,
                    syncedBehaviour.BehaviourName);
                result &= renderResult.Success;

                if (!string.IsNullOrEmpty(renderResult.FileGenerated))
                {
                    _ = files.Add(renderResult.FileGenerated);
                }
            }

            return new BakeResult
            {
                Success = result,
                GeneratedFiles = files,
            };
        }

        private static ScribanOptions GetOptions()
        {
            var componentsObject = new ScriptObject();
            componentsObject.Import("resolveCsharpType", new Func<string, string, string>(ResolveCsharpType));
            componentsObject.Import("resolveSchemaTypeToCsharpType", new Func<string, string>(ResolveSchemaTypeToCsharpType));
            componentsObject.Import("resolveCsharpTypeToSchemaType", new Func<string, string>(ResolveCsharpTypeToSchemaType));
            componentsObject.Import("ResolveCommandParam", new Func<object, string>(ResolveCommandParam));
            componentsObject.Import("ResolveCommandRequestArgs", new Func<object, string>(ResolveCommandRequestArgs));

            var scribanOptions = new ScribanOptions
            {
                Namespace = "Coherence.Generated",
                UsingDirectives = new List<string>
                {
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "UnityEngine",
                    "Coherence.Toolkit",
                    "Coherence.Toolkit.Bindings",
                    "Coherence.Entities",
                    "Coherence.ProtocolDef",
                    "Coherence.Brook",
                    "Coherence.Toolkit.Bindings.ValueBindings",
                    "Coherence.Toolkit.Bindings.TransformBindings",
                    "Coherence.Connection",
                    "Coherence.SimulationFrame",
                    "Coherence.Interpolation",
                    "Coherence.Log",
                    "Logger = Coherence.Log.Logger",
                    "UnityEngine.Scripting",
                },
                TemplateNames = new List<string>
                {
                    "baked_binding",
                    "baked_entity",
                },
                TemplateLoader = new TemplateLoaderFromDisk(),
                Model = new List<ScriptObject>
                {
                    componentsObject,
                },
            };

            return scribanOptions;
        }

        private static string ResolveCsharpType(string bindingClassName, string cSharpType)
        {
            if (bindingClassName.Equals("ReferenceBinding"))
            {
                return "Entity";
            }

            if (bindingClassName.Equals("EnumBinding"))
            {
                return "System.Int32";
            }

            return cSharpType;
        }

        private static string ResolveSchemaTypeToCsharpType(string schemaType)
        {
            if (schemaType == SchemaType.Vector3.ToString())
            {
                return ".ToUnityVector3()";
            }
            else if (schemaType == SchemaType.Vector2.ToString())
            {
                return ".ToUnityVector2()";
            }
            else if (schemaType == SchemaType.Quaternion.ToString())
            {
                return ".ToUnityQuaternion()";
            }
            else if (schemaType == SchemaType.Color.ToString())
            {
                return ".ToUnityColor()";
            }

            return "";
        }

        private static string ResolveCsharpTypeToSchemaType(string schemaType)
        {
            if (schemaType == SchemaType.Vector3.ToString())
            {
                return ".ToVector3d()";
            }
            else if (schemaType == SchemaType.Vector2.ToString())
            {
                return ".ToCoreVector2()";
            }
            else if (schemaType == SchemaType.Quaternion.ToString())
            {
                return ".ToCoreQuaternion()";
            }
            else if (schemaType == SchemaType.Color.ToString())
            {
                return ".ToCoreColor()";
            }

            return "";
        }

        private static string ResolveCommandParam(object field)
        {
            if (field is not CommandParameterInfo member)
            {
                return null;
            }

            return member.Type switch
            {
                "UnityEngine.Transform" => $"bridge.EntityIdToTransform",
                "Coherence.Toolkit.CoherenceSync" => $"bridge.EntityIdToCoherenceSync",
                "UnityEngine.GameObject" => $"bridge.EntityIdToGameObject",
                "UnityEngine.RectTransform" => $"bridge.EntityIdToRectTransform",
                _ when member.BakedType == "Entity" => $"bridge.EntityIdToComponent<global::{ResolveNestedNameSpace(member.Type)}>",
                _ => $"({ResolveNestedNameSpace(member.Type)})",
            };
        }

        private static string ResolveCommandRequestArgs(object field)
        {
            if (field is not List<CommandParameterInfo> list)
            {
                return null;
            }

            var parameters = new StringBuilder();

            for (var i = 0; i < list.Count; i++)
            {
                if (i != 0)
                {
                    _ = parameters.Append(", ");
                }

                var member = list[i];

                var getter = $"requestArgs.Args[{i}]";
                var set = $"(global::{ResolveNestedNameSpace(member.Type)})({getter})";

                _ = parameters.Append(set);
            }

            return parameters.ToString();
        }

        private static string ResolveNestedNameSpace(string name)
        {
            return name.Replace("+", ".");
        }
    }
}
