// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_6000_4_OR_NEWER
namespace Coherence.Toolkit
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    internal static class EntityIdExtensions
    {
        public static int GetInstanceId(this EntityId entityId) =>
            (int)(EntityId.ToULong(entityId) & 0xFFFFFFFF);

        public static int GetInstanceId(this SceneHandle sceneHandle) =>
            (int)(sceneHandle.GetRawData() & 0xFFFFFFFF);

        public static ulong GetFromInstanceId(int instanceId, Scene scene)
        {
            var upper = scene.handle.GetRawData() & 0xFFFFFFFF00000000;
            var lower = (ulong)(uint)instanceId;
            return upper | lower;
        }
    }
}
#endif
