// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System;
    using System.Linq;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class ConfigsAnalyzerHandlerTests : CoherenceTest
    {
        private ConfigsAnalyzerHandler handler;
        private CoherenceSyncConfig config;
        private CoherenceSync sync;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            handler = new();
            config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            sync = new GameObject().AddComponent<CoherenceSync>();
            config.Init(sync.gameObject);
            var provider = new DirectReferenceProvider();
            provider.Initialize(config);
            config.Provider = provider;
            sync.CoherenceSyncConfig = config;
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(sync.gameObject);
            base.TearDown();
        }

        [Test]
        [Description("Validate returns false when config is null.")]
        public void Validate_WithNullConfig_ReturnsFalse() => Assert.IsFalse(handler.Validate(null));

        [Test]
        [Description("Validate returns false when config has no EditorTarget assigned.")]
        public void Validate_WithMissingEditorTarget_ReturnsFalse()
        {
            config.EditorTarget = null;
            Assert.IsFalse(handler.Validate(config));
        }

        [Test]
        [Description("Validate returns false when config has no NetworkObjectInstantiator.")]
        public void Validate_WithMissingInstantiator_ReturnsFalse()
        {
            config.Instantiator = null;
            Assert.IsFalse(handler.Validate(config));
        }

        [Test]
        [Description("Validate returns false when config has no NetworkObjectProvider.")]
        public void Validate_WithMissingProvider_ReturnsFalse()
        {
            config.Provider = null;
            Assert.IsFalse(handler.Validate(config));
        }

        [Test]
        [Description("Validate returns true when config is valid.")]
        public void Validate_WithValidConfig_ReturnsTrue()
        {
            // Remove any user-created analyzers that could cause the test to fail.
            handler.Analyzers.RemoveAll(x => !x.GetType().Assembly.GetName().Name.StartsWith("Coherence."));

            Assert.That(handler.Analyzers.Count, Is.Not.Zero, "No analyzers belonging to a coherence assembly found.");
            Assert.That(handler.Validate(config), Is.True, "Validate should return true for a \"valid\" config.");
        }

        [Test]
        [Description("GetCompoundedErrorMessage returns an empty string when config is valid.")]
        public void GetCompoundedErrorMessage_WithValidConfig_ReturnsEmptyString() => Assert.IsEmpty(handler.GetCompoundedErrorMessage(config));

        [Test]
        [Description("GetCompoundedErrorMessage returns a non-empty string when config is null.")]
        public void GetCompoundedErrorMessage_WithNullConfig_ReturnsNonEmptyMessage()
            => Assert.That(handler.GetCompoundedErrorMessage(null).Trim(), Has.Length.GreaterThan(0));

        [Test]
        [Description("GetCompoundedErrorMessage returns a non-empty string when config has no EditorTarget assigned.")]
        public void GetCompoundedErrorMessage_WithMissingEditorTarget_ReturnsNonEmptyMessage()
        {
            config.EditorTarget = null;
            Assert.That(handler.GetCompoundedErrorMessage(config).Trim(), Has.Length.GreaterThan(0));
        }

        [Test]
        [Description("GetCompoundedErrorMessage returns a non-empty string when config has no NetworkObjectInstantiator.")]
        public void GetCompoundedErrorMessage_WithMissingInstantiator_ReturnsNonEmptyMessage()
        {
            config.Instantiator = null;
            Assert.That(handler.GetCompoundedErrorMessage(config).Trim(), Has.Length.GreaterThan(0));
        }

        [Test]
        [Description("GetCompoundedErrorMessage returns a non-empty string when config has no NetworkObjectProvider.")]
        public void GetCompoundedErrorMessage_WithMissingProvider_ReturnsNonEmptyMessage()
        {
            config.Provider = null;
            Assert.That(handler.GetCompoundedErrorMessage(config).Trim(), Has.Length.GreaterThan(0));
        }

        [Test]
        [Description("Multiple analyzer errors are separated by blank lines.")]
        public void GetCompoundedErrorMessage_WithMultipleErrors_SeparatesErrorsWithBlankLines()
        {
            config.EditorTarget = null; // <- Should make ObjectReferenceAnalyzer fail.
            config.Instantiator = null; // <- Should make ObjectInstantiatorAnalyzer fail.
            config.Provider = null; // <- Should make ObjectProviderAnalyzer fail.

            var message = handler.GetCompoundedErrorMessage(config);

            Assert.That(message, Does.Contain(Environment.NewLine + Environment.NewLine));
        }

        [Test]
        [Description("TryGetInfo returns false for a config that was not registered in the registry.")]
        public void TryGetInfo_WithUnregisteredConfig_ReturnsFalse()
            => Assert.IsFalse(handler.TryGetInfo(config, out _));

        [Test]
        [Description("AllEntriesInfo contains more than zero variables when the CoherenceSyncConfigRegistry contains networked prefabs.")]
        public void AllEntriesInfo_Has_Variables_If_CoherenceSyncConfigRegistry_Has_Networked_Prefabs()
        {
            var info = handler.AllEntriesInfo;
            if (CoherenceSyncConfigRegistry.Instance.Any(x=> x && x.EditorTarget && x.EditorTarget is GameObject gameObject && gameObject.TryGetComponent<CoherenceSync>(out _)))
            {
                Assert.That(info.Variables, Is.Not.Zero);
            }
            else
            {
                Assert.That(info.Variables, Is.Zero);
            }
        }
    }
}
