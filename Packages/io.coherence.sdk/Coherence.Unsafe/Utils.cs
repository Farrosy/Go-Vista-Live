// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Unsafe
{
#if UNITY_5_3_OR_NEWER
    using Unsafe = Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
#else
    using Unsafe = System.Runtime.CompilerServices.Unsafe;
#endif

    public static class Utils
    {
        public static int SizeOf<T>() where T : struct => Unsafe.SizeOf<T>();
    }
}
