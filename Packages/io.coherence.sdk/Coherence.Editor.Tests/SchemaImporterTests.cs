// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tests.Editor
{
    using System;
    using System.IO;
    using Coherence.Editor;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class SchemaImporterTests : CoherenceTest
    {
        private string tempPath;
        private SchemaAsset asset;

        [SetUp]
        public void CreateTempAssets()
        {
            tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.schema");
            asset = ScriptableObject.CreateInstance<SchemaAsset>();
        }

        [TearDown]
        public void CleanupTempAssets()
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            Object.DestroyImmediate(asset);
        }

        [Test]
        [Description("A valid schema file populates raw with the file's text content.")]
        public void TryPopulate_ValidSchema_SetsRaw()
        {
            const string schema = "component Foo [id \"0\"]\n";
            File.WriteAllText(tempPath, schema);

            SchemaImporter.TryPopulate(asset, tempPath);

            Assert.That(asset.raw, Is.EqualTo(schema));
        }

        [Test]
        [Description("A valid schema file sets identifier to the filename.")]
        public void TryPopulate_ValidSchema_SetsIdentifier()
        {
            File.WriteAllText(tempPath, "component Foo [id \"0\"]\n");

            SchemaImporter.TryPopulate(asset, tempPath);

            Assert.That(asset.identifier, Is.EqualTo(Path.GetFileName(tempPath)));
        }

        [Test]
        [Description("A valid schema file populates SchemaDefinition with the parsed components.")]
        public void TryPopulate_ValidSchema_SetsSchemaDefinition()
        {
            File.WriteAllText(tempPath, "component Foo [id \"0\"]\n");

            SchemaImporter.TryPopulate(asset, tempPath);

            Assert.That(asset.SchemaDefinition.ComponentDefinitions, Has.Count.EqualTo(1));
        }

        [Test]
        [Description("A valid schema file returns null, indicating success.")]
        public void TryPopulate_ValidSchema_ReturnsNull()
        {
            File.WriteAllText(tempPath, "component Foo [id \"0\"]\n");

            var result = SchemaImporter.TryPopulate(asset, tempPath);

            Assert.That(result, Is.Null);
        }

        [Test]
        [Description("A missing file returns an IOException without throwing.")]
        public void TryPopulate_MissingFile_ReturnsIOException() =>
            Assert.That(SchemaImporter.TryPopulate(asset, tempPath), Is.InstanceOf<IOException>());

        [Test]
        [Description("An empty schema file returns null, indicating no error.")]
        public void TryPopulate_EmptyFile_ReturnsNull()
        {
            File.WriteAllText(tempPath, "");

            var result = SchemaImporter.TryPopulate(asset, tempPath);

            Assert.That(result, Is.Null);
        }

        [Test]
        [Description("An empty schema file produces a SchemaDefinition with no components.")]
        public void TryPopulate_EmptyFile_SetsEmptySchemaDefinition()
        {
            File.WriteAllText(tempPath, "");

            SchemaImporter.TryPopulate(asset, tempPath);

            Assert.That(asset.SchemaDefinition.ComponentDefinitions, Is.Empty);
        }
    }
}
