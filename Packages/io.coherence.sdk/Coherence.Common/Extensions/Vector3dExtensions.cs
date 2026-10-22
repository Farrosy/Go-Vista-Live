// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common
{
    public static class Vector3dEx
    {
        public static Vector3d ToVector3d(this System.Numerics.Vector3 vector3)
        {
            return new Vector3d(vector3.X, vector3.Y, vector3.Z);
        }

#if UNITY
        public static Vector3d ToVector3d(this Vector3 vector3)
        {
            return new Vector3d(vector3.x, vector3.y, vector3.z);
        }

        public static Vector3 ToUnityVector3(this Vector3d v)
        {
            if (v.x < float.MinValue || v.x > float.MaxValue)
            {
                throw new OverflowException($"Cannot convert vector3d to vector3, component X is overflowing 32-bits ({v.x})");
            }

            if (v.y < float.MinValue || v.y > float.MaxValue)
            {
                throw new OverflowException($"Cannot convert vector3d to vector3, component Y is overflowing 32-bits ({v.y})");
            }

            if (v.z < float.MinValue || v.z > float.MaxValue)
            {
                throw new OverflowException($"Cannot convert vector3d to vector3, component Z is overflowing 32-bits ({v.z})");
            }

            return new Vector3((float)v.x, (float)v.y, (float)v.z);
        }
#endif
    }
}