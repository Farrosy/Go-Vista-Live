// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Tests;
    using Log;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit Mode unit tests for <see cref="PrefabSyncGroup"/>.
    /// </summary>
    public class PrefabSyncGroupTests : CoherenceTest
    {
        private GameObject rootGo;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            rootGo = null;
        }

        [TearDown]
        public override void TearDown()
        {
            if (rootGo)
            {
                Object.DestroyImmediate(rootGo);
            }

            base.TearDown();
        }

        private (PrefabSyncGroup group, CoherenceSync rootSync, List<CoherenceSync> childSyncs) CreateHierarchy(int childCount, bool childrenNested)
        {
            rootGo = new("Root");
            var rootSync = rootGo.AddComponent<CoherenceSync>();
            var group = rootGo.AddComponent<PrefabSyncGroup>();

            var childSyncs = new List<CoherenceSync>();
            var parent = rootGo.transform;
            for (var i = 0; i < childCount; i++)
            {
                var childGo = new GameObject($"Child_{i}");
                var childSync = childGo.AddComponent<CoherenceSync>();
                childGo.transform.SetParent(parent);
                childSyncs.Add(childSync);

                if(childrenNested)
                {
                    parent = childGo.transform;
                }
            }

            return (group, rootSync, childSyncs);
        }

        [TestCase(false), TestCase(true)]
        [Description("ChildCoherenceSyncs returns child CoherenceSyncs nested under the group")]
        public void ChildCoherenceSyncs_Returns_All_Children(bool childrenNested)
        {
            var (group, _, createdChildSyncs) = CreateHierarchy(3, childrenNested: childrenNested);

            var result = group.ChildCoherenceSyncs();

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result, Is.EquivalentTo(createdChildSyncs));
        }

        [TestCase(false), TestCase(true)]
        [Description("ChildCoherenceSyncs does not include the root CoherenceSync itself")]
        public void ChildCoherenceSyncs_Excludes_Root_Sync(bool childrenNested)
        {
            var (group, rootSync, _) = CreateHierarchy(2, childrenNested: childrenNested);

            var result = group.ChildCoherenceSyncs();

            Assert.That(result, Has.No.Member(rootSync));
        }

        [Test]
        [Description("ChildCoherenceSyncs returns empty list when there are no nested children")]
        public void ChildCoherenceSyncs_Returns_Empty_List_When_No_Child_Coherence_Syncs()
        {
            var (group, _, _) = CreateHierarchy(0, childrenNested: false);

            var result = group.ChildCoherenceSyncs();

            Assert.That(result, Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        [Description("IsChildFromSyncGroup returns true for a tracked child")]
        public void IsChildFromSyncGroup_Returns_True_For_Tracked_Child(bool childrenNested)
        {
            var (group, _, createdChildSyncs) = CreateHierarchy(2, childrenNested: childrenNested);

            group.InitializeChildCoherenceSyncs();

            Assert.That(group.IsChildFromSyncGroup(createdChildSyncs[0]), Is.True);
            Assert.That(group.IsChildFromSyncGroup(createdChildSyncs[1]), Is.True);
        }

        [Test]
        [Description("IsChildFromSyncGroup returns false for an untracked CoherenceSync")]
        public void IsChildFromSyncGroup_Returns_False_For_Unrelated_Sync()
        {
            var (group, _, _) = CreateHierarchy(1, childrenNested: true);
            group.ChildCoherenceSyncs();

            var unrelatedGo = new GameObject("Unrelated");
            var unrelatedSync = unrelatedGo.AddComponent<CoherenceSync>();

            try
            {
                Assert.That(group.IsChildFromSyncGroup(unrelatedSync), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(unrelatedGo);
            }
        }

        [TestCase(false), TestCase(true)]
        [Description("OnEnable allocates ids byte array with correct size based on child count")]
        public void OnEnable_Allocates_Ids_With_Correct_Size(bool childrenNested)
        {
            var (group, _, _) = CreateHierarchy(3, childrenNested: childrenNested);

            group.InitializeChildCoherenceSyncs();

            // ids should be 4 bytes per child
            Assert.That(group.ids, Is.Not.Null);
            Assert.That(group.ids.Length, Is.EqualTo(3 * 4));
        }

        [Test]
        [Description("IsReady remains false after initialization when children share parent with root.")]
        public void IsReady_Is_False_After_Initialization_When_Children_Share_Parent_With_Root()
        {
            var (group, _, _) = CreateHierarchy(0, childrenNested: false);

            var childSync = new GameObject("Child").AddComponent<CoherenceSync>();
            childSync.transform.SetParent(rootGo.transform);

            group.InitializeChildCoherenceSyncs();

            Assert.That(group.IsReady, Is.False);
        }

        [Test]
        [Description("IsReady is true after initialization when children do not share parent with root.")]
        public void IsReady_Is_True_After_Initialization_When_Children_Do_Not_Share_Parent_With_Root()
        {
            var (group, _, _) = CreateHierarchy(0, childrenNested: false);
            var childSync = new GameObject("Child").AddComponent<CoherenceSync>();

            try
            {
                group.InitializeChildCoherenceSyncs();

                Assert.That(group.IsReady, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(childSync.gameObject);
            }
        }

        [Test]
        [Description("OnEnable sets IsReady to true when no children need unique IDs")]
        public void IsReady_Is_True_After_Initialization_When_No_Child_Coherence_Syncs()
        {
            var (group, _, _) = CreateHierarchy(0, childrenNested: false);

            group.InitializeChildCoherenceSyncs();

            Assert.That(group.IsReady, Is.True);
        }

        [Test]
        [Description("OnEnable assigns ManualUniqueId to children whose parent differs from root's parent")]
        public void Initialization_Assigns_ManualUniqueId_To_Nested_Children()
        {
            var (group, _, childSyncs) = CreateHierarchy(2, childrenNested: true);

            group.InitializeChildCoherenceSyncs();

            foreach (var childSync in childSyncs)
            {
                Assert.That(childSync.ManualUniqueId, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        [Description("OnEnable disables nested child CoherenceSyncs that need unique IDs")]
        public void Initialization_Disables_Nested_Children()
        {
            var (group, _, childSyncs) = CreateHierarchy(2, childrenNested: true);

            group.InitializeChildCoherenceSyncs();

            foreach (var childSync in childSyncs)
            {
                Assert.That(childSync.enabled, Is.False);
            }
        }

        [TestCase(false), TestCase(true)]
        [Description("OnEnable sets child uniqueness type to NoDuplicates")]
        public void Initialization_Sets_Child_UniquenessType_NoDuplicates(bool childrenNested)
        {
            var (group, _, childSyncs) = CreateHierarchy(2, childrenNested: childrenNested);

            group.InitializeChildCoherenceSyncs();

            foreach (var childSync in childSyncs)
            {
                Assert.That(childSync.uniquenessType, Is.EqualTo(CoherenceSync.UniquenessType.NoDuplicates));
            }
        }

        [TestCase(false), TestCase(true)]
        [Description("OnEnable sets child UnsyncedEntityPriority to UniqueId")]
        public void Initialization_Sets_Child_UnsyncedEntityPriority_UniqueId(bool childrenNested)
        {
            var (group, _, childSyncs) = CreateHierarchy(2, childrenNested: childrenNested);

            group.InitializeChildCoherenceSyncs();

            foreach (var childSync in childSyncs)
            {
                Assert.That(childSync.UnsyncedEntityPriority, Is.EqualTo(CoherenceSync.UnsyncedNetworkEntityPriority.UniqueId));
            }
        }

        [TestCase(false), TestCase(true)]
        [Description("OnReceivedIds updates ManualUniqueId on children from received byte array")]
        public void OnReceivedIds_Updates_Child_ManualUniqueIds(bool childrenNested)
        {
            var (group, _, childSyncs) = CreateHierarchy(2, childrenNested: childrenNested);

            // First force child discovery
            group.ChildCoherenceSyncs();

            // Create new IDs byte array (2 children * 4 bytes each)
            var newIds = new byte[8];
            var id1Bytes = BitConverter.GetBytes(12345);
            var id2Bytes = BitConverter.GetBytes(67890);
            id1Bytes.CopyTo(newIds, 0);
            id2Bytes.CopyTo(newIds, 4);

            // Invoke OnReceivedIds via reflection (it's private)
            group.OnReceivedIds(new byte[8], newIds);

            Assert.That(childSyncs[0].ManualUniqueId, Is.EqualTo(BitConverter.ToString(id1Bytes)));
            Assert.That(childSyncs[1].ManualUniqueId, Is.EqualTo(BitConverter.ToString(id2Bytes)));
        }

        [TestCase(false), TestCase(true)]
        [Description("OnReceivedIds handles fewer IDs than children gracefully")]
        public void OnReceivedIds_Handles_Fewer_Ids_Than_Children(bool childrenNested)
        {
            var (group, _, childSyncs) = CreateHierarchy(3, childrenNested: childrenNested);
            group.ChildCoherenceSyncs();

            // Only send 2 IDs for 3 children
            var newIds = new byte[8];
            var id1Bytes = BitConverter.GetBytes(111);
            var id2Bytes = BitConverter.GetBytes(222);
            id1Bytes.CopyTo(newIds, 0);
            id2Bytes.CopyTo(newIds, 4);

            group.OnReceivedIds(Array.Empty<byte>(), newIds);

            Assert.That(childSyncs[0].ManualUniqueId, Is.EqualTo(BitConverter.ToString(id1Bytes)));
            Assert.That(childSyncs[1].ManualUniqueId, Is.EqualTo(BitConverter.ToString(id2Bytes)));
        }

        [Test]
        [Description("OnReceivedIds logs error when more IDs received than children")]
        public void OnReceivedIds_Logs_Error_When_Too_Many_Ids()
        {
            var (group, rootSync, _) = CreateHierarchy(1, childrenNested: true);
            // Inject the test logger so we can track errors
            rootSync.logger = logger;
            group.logger = logger;
            group.lastSyncLogger = logger;
            group.InitializeSelf();
            group.ChildCoherenceSyncs();

            // Send 3 IDs for 1 child
            var newIds = new byte[12];

            group.OnReceivedIds(Array.Empty<byte>(), newIds);

            Assert.That(logger.GetCountForErrorID(Error.ToolkitPrefabSyncGroupIDs), Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        [Description("Ids byte array has unique values for each child")]
        public void OnEnable_Generates_Unique_Ids_Per_Child(bool childrenNested)
        {
            var (group, _, _) = CreateHierarchy(3, childrenNested: childrenNested);

            group.InitializeChildCoherenceSyncs();

            var ids = group.ids;
            Assert.That(ids.Length, Is.EqualTo(12));

            // Extract individual IDs and verify they're unique
            var id1 = BitConverter.ToInt32(ids, 0);
            var id2 = BitConverter.ToInt32(ids, 4);
            var id3 = BitConverter.ToInt32(ids, 8);

            Assert.That(id1, Is.Not.EqualTo(id2));
            Assert.That(id1, Is.Not.EqualTo(id3));
            Assert.That(id2, Is.Not.EqualTo(id3));
        }

        [TestCase(false), TestCase(true)]
        [Description("Each child gets a distinct ManualUniqueId after OnEnable")]
        public void OnEnable_Each_Child_Gets_Distinct_UniqueId(bool childrenNested)
        {
            var (group, _, childSyncs) = CreateHierarchy(3, childrenNested: childrenNested);

            group.InitializeChildCoherenceSyncs();

            var uniqueIds = new HashSet<string>();
            foreach (var childSync in childSyncs)
            {
                Assert.That(uniqueIds.Add(childSync.ManualUniqueId), Is.True, $"Duplicate ManualUniqueId found: {childSync.ManualUniqueId}");
            }
        }
    }
}




