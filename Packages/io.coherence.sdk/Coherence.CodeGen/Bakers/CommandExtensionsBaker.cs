namespace Coherence.CodeGen
{
    using System.Collections.Generic;
    using System.Linq;
    using Scriban.Runtime;
    using Toolkit;

    internal class CommandExtensionsBaker : IBaker
    {
        protected virtual string ClassNamePostfix => "";
        protected virtual string Template => "baked_command_extensions";
        protected virtual string ProcessOutputDirectory(string outputDirectory) => outputDirectory;

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

            var writer = new ScribanWriter();

            var opts = new ScribanOptions
            {
                Model = new List<ScriptObject> { new(), },
                TemplateLoader = new TemplateLoaderFromDisk(),
                UsingDirectives = new List<string>
                {
                    "Coherence",
                    "Coherence.Toolkit",
                },
                TemplateNames = new List<string>
                {
                    Template,
                },
            };

            var commandDescriptionsPerType = codeGenData.EntitiesData.BehaviourData
                .SelectMany(b => b.Commands)
                .GroupBy(cmd => cmd.ExtensionMethodDeclaringClass ?? cmd.MethodDeclaringClass)
                .Select(g => (g.Key, g.GroupBy(cmd => (cmd.MethodName, cmd.ExtensionMethodSignature ?? cmd.Signature)).Select(d => d.First()).ToList()))
                .ToList();

            if (commandDescriptionsPerType.Count == 0)
            {
                return new BakeResult
                {
                    Success = true,
                    GeneratedFiles = new HashSet<string>(),
                };
            }

            var success = true;
            var files = new HashSet<string>();
            var outputDir = ProcessOutputDirectory(codeGenData.OutputDirectory);
            foreach (var (type, commands) in commandDescriptionsPerType)
            {
                var name = "CommandsFor_" + NameMangler.MangleCSharpIdentifier(type) + ClassNamePostfix;
                opts.Model[0]["class_name"] = name;
                opts.Model[0]["commands"] = commands;
                var renderResult = writer.Render(opts, outputDir, name);
                success &= renderResult.Success;

                if (!string.IsNullOrEmpty(renderResult.FileGenerated))
                {
                    _ = files.Add(renderResult.FileGenerated);
                }
            }

            return new BakeResult
            {
                Success = success,
                GeneratedFiles = files,
            };
        }
    }
}
