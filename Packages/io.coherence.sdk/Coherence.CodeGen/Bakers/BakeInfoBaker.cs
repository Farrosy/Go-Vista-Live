namespace Coherence.CodeGen
{
    using System.Collections.Generic;
    using Scriban.Runtime;

    public class BakeInfoBaker : IBaker
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

            var scribanOptions = new ScribanOptions()
            {
                Namespace = "Coherence.Generated",
                UsingDirectives = new List<string>
                {
                    "UnityEngine",
                    "Coherence.Toolkit"
                },
                TemplateNames = new List<string>
                {
                    "bake_info",
                },
                TemplateLoader = new TemplateLoaderFromDisk(),
                Model = new List<ScriptObject>
                {
                    new ScriptObject
                    {
                        { "schema_id", codeGenData.SchemaDefinition.SchemaId },
                    },
                },
            };

            var scribanWriter = new ScribanWriter();
            var renderResult = scribanWriter.Render(scribanOptions, codeGenData.OutputDirectory, "BakeInfo");
            var files = new HashSet<string>();

            if (renderResult.Success)
            {
                files.Add(renderResult.FileGenerated);
            }

            return new BakeResult
            {
                Success = renderResult.Success,
                GeneratedFiles = files,
            };
        }
    }
}
