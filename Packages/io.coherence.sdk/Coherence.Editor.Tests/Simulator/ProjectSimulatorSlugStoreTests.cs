// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests.Simulator
{
    using Coherence.Tests;
    using Editor.Toolkit;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    public class ProjectSimulatorSlugStoreTests : CoherenceTest
    {
        [Test]
        [Description("Value is set correctly in the store.")]
        public void Get_Returns_Value_Previously_Stored_Using_Set()
        {
            ProjectSimulatorSlugStore.Set(nameof(Get_Returns_Value_Previously_Stored_Using_Set),
                nameof(ProjectSimulatorSlugStoreTests));
            var value = ProjectSimulatorSlugStore.Get(nameof(Get_Returns_Value_Previously_Stored_Using_Set));
            Assert.That(value, Is.EqualTo(nameof(ProjectSimulatorSlugStoreTests)));
        }

        [Test]
        [Description("Set with a null key does not throw.")]
        public void Set_With_Null_Key_Does_Not_Throw() =>
            Assert.DoesNotThrow(() => ProjectSimulatorSlugStore.Set(null, "some-slug"));

        [Test]
        [Description("Set with an empty key does not throw.")]
        public void Set_With_Empty_Key_Does_Not_Throw() =>
            Assert.DoesNotThrow(() => ProjectSimulatorSlugStore.Set("", "some-slug"));

        [Test]
        [Description("SetUseSimulator with a null key does not throw.")]
        public void SetUseSimulator_With_Null_Key_Does_Not_Throw() =>
            Assert.DoesNotThrow(() => ProjectSimulatorSlugStore.SetUseSimulator(null, true, "some-slug"));

        [Test]
        [Description("SetUseSimulator with an empty key does not throw.")]
        public void SetUseSimulator_With_Empty_Key_Does_Not_Throw() =>
            Assert.DoesNotThrow(() => ProjectSimulatorSlugStore.SetUseSimulator("", true, "some-slug"));

        [Test]
        [Description("Disused keys are removed.")]
        public void Disused_Keys_Removed()
        {
            var keys = new[] { "a", "b", "c", "d" };
            foreach (var key in keys)
            {
                ProjectSimulatorSlugStore.Set(key, key);
            }

            foreach (var key in keys)
            {
                Assert.That(ProjectSimulatorSlugStore.Get(key), Is.EqualTo(key));
            }

            var keysToKeep = new List<string>(new []
            {
                "a",
                "c"
            });
            ProjectSimulatorSlugStore.KeepOnly(key => keysToKeep.Contains(key));

            Assert.That(ProjectSimulatorSlugStore.Get("a"), Is.EqualTo("a"));
            Assert.That(ProjectSimulatorSlugStore.Get("b"), Is.Null);
            Assert.That(ProjectSimulatorSlugStore.Get("c"), Is.EqualTo("c"));
            Assert.That(ProjectSimulatorSlugStore.Get("d"), Is.Null);
        }

        [Test]
        [Description("SetUseSimulator with true makes Get return the slug.")]
        public void SetUseSimulator_True_Makes_Get_Return_Slug()
        {
            const string key = nameof(SetUseSimulator_True_Makes_Get_Return_Slug);
            const string slug = "some-slug";

            ProjectSimulatorSlugStore.SetUseSimulator(key, true, slug);

            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.EqualTo(slug));
        }

        [Test]
        [Description("SetUseSimulator with false makes Get return null.")]
        public void SetUseSimulator_False_Makes_Get_Return_Null()
        {
            const string key = nameof(SetUseSimulator_False_Makes_Get_Return_Null);
            const string slug = "some-slug";

            ProjectSimulatorSlugStore.SetUseSimulator(key, false, slug);

            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.Null);
        }

        [Test]
        [Description("SetUseSimulator can disable a previously enabled simulator.")]
        public void SetUseSimulator_Can_Disable_After_Enable()
        {
            const string key = nameof(SetUseSimulator_Can_Disable_After_Enable);
            const string slug = "some-slug";

            ProjectSimulatorSlugStore.SetUseSimulator(key, true, slug);
            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.EqualTo(slug));

            ProjectSimulatorSlugStore.SetUseSimulator(key, false, slug);
            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.Null);
        }

        [Test]
        [Description("SetUseSimulator can re-enable a previously disabled simulator.")]
        public void SetUseSimulator_Can_Enable_After_Disable()
        {
            const string key = nameof(SetUseSimulator_Can_Enable_After_Disable);
            const string slug1 = "slug1";
            const string slug2 = "slug2";

            ProjectSimulatorSlugStore.SetUseSimulator(key, false, slug1);
            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.Null);

            ProjectSimulatorSlugStore.SetUseSimulator(key, true, slug2);
            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.EqualTo(slug1));
        }

        [Test]
        [Description("SetUseSimulator on an existing key set via Set updates UseSimulator flag.")]
        public void SetUseSimulator_Does_Not_Modify_Preexisting_Simulator_Slug()
        {
            const string key = nameof(SetUseSimulator_Does_Not_Modify_Preexisting_Simulator_Slug);
            const string originalSimulatorSlug = "original-slug";
            const string defaultSimulatorSlug = "default-slug";

            ProjectSimulatorSlugStore.Set(key, originalSimulatorSlug);
            ProjectSimulatorSlugStore.SetUseSimulator(key, false, defaultSimulatorSlug);
            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.Null);
            ProjectSimulatorSlugStore.SetUseSimulator(key, true, defaultSimulatorSlug);
            Assert.That(ProjectSimulatorSlugStore.Get(key), Is.EqualTo(originalSimulatorSlug));
        }
    }
}
