// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using System.IO;
    using Coherence.ProtocolDef;
    using UnityEditor.PackageManager;

    public static class TestSchema
    {
        private static ProtocolDefinition definition;
        private static ISchemaDataFactory dataFactory;

        public static ProtocolDefinition Definition => definition;
        public static ISchemaDataFactory DataFactory => dataFactory;

        static TestSchema()
        {
            var package = PackageInfo.FindForAssembly(typeof(TestSchema).Assembly);
            var schemaPath = Path.Combine(package.resolvedPath, "Coherence.Common.Tests", "test.schema");

            definition = ProtocolDefinition.LoadFromPath(schemaPath, includeRSSchema: true);
            dataFactory = new DefaultSchemaDataFactory(definition);
        }
    }
}
