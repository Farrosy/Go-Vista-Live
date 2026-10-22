namespace Coherence.CodeGen
{
    using Coherence.Schema;

    public class CodeGenData
    {
        public SchemaDefinition SchemaDefinition;
        public EntitiesBakeData EntitiesData;
        public string OutputDirectory;
        public bool NoUnityReferences;
        public bool BakeToolkitImplementations;
    }
}
