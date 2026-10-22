// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.IO;
    using System.Text;
    using Coherence.Schema;
    using UnityEditor;
    using UnityEditor.AssetImporters;
    using UnityEngine;

    [ScriptedImporter(5, Constants.schemaExtension)]
    internal sealed class SchemaImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            // GUID as found in SchemaAsset.cs.meta and SchemaDefinition.cs.meta.
            // Using GUID instead of path for resilience.
            // This importer is dependent on SchemaAsset being imported and properly loaded into memory.
            // Without these dependencies set, there can be scenarios where this importer triggers and tries to import
            // a .schema file without Unity having imported and loaded the SchemaAsset ScriptableObject.
            // A way this can manifest is by AddObjectToAsset (see below) throwing an InvalidOperationException.
            var schemaAssetGuid = new GUID("583bf8a6352b543cf896bb83930ccd6e");
            context.DependsOnSourceAsset(schemaAssetGuid);
            var schemaDefinitionGuid = new GUID("fbdb24773aeecae47a9eb0079399827c");
            context.DependsOnSourceAsset(schemaDefinitionGuid);

            var asset = ScriptableObject.CreateInstance<SchemaAsset>();

            var error = TryPopulate(asset, context.assetPath);
            if (error != null)
            {
                context.LogImportError(error.ToString(), asset);
            }

            context.AddObjectToAsset(Constants.schemaAssetIdentifier, asset);
            context.SetMainObject(asset);
        }

        internal static Exception TryPopulate(SchemaAsset asset, string assetPath)
        {
            try
            {
                asset.raw = File.ReadAllText(assetPath, Encoding.UTF8);
                asset.identifier = Path.GetFileName(assetPath);
                asset.SchemaDefinition = SchemaReader.Read(asset.raw);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}
