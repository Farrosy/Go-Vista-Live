// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using Coherence.Common;
    using Coherence.Tests;
    using NUnit.Framework;
    using System;
    using UnityEngine;

    public class Vector3dConversionTests : CoherenceTest
    {
        #region ToUnityVector3

        [Test]
        public void ToUnityVector3_ConvertsWithinFloatRange()
        {
            var vector3d = new Vector3d(1.5, -2.5, 3.5);
            var result = vector3d.ToUnityVector3();
            Assert.That(result, Is.EqualTo(new Vector3(1.5f, -2.5f, 3.5f)));
        }

        [Test]
        public void ToUnityVector3_ThrowsOverflowException_WhenXExceedsFloatMax()
        {
            var vector3d = new Vector3d((double)float.MaxValue + 1e32, 0, 0);
            Assert.Throws<OverflowException>(() => vector3d.ToUnityVector3());
        }

        [Test]
        public void ToUnityVector3_ThrowsOverflowException_WhenYExceedsFloatMax()
        {
            var vector3d = new Vector3d(0, (double)float.MaxValue + 1e32, 0);
            Assert.Throws<OverflowException>(() => vector3d.ToUnityVector3());
        }

        [Test]
        public void ToUnityVector3_ThrowsOverflowException_WhenZExceedsFloatMax()
        {
            var vector3d = new Vector3d(0, 0, (double)float.MaxValue + 1e32);
            Assert.Throws<OverflowException>(() => vector3d.ToUnityVector3());
        }

        [Test]
        public void ToUnityVector3_AllowsDoubleInfinity()
        {
            var vector3d = new Vector3d(double.PositiveInfinity, double.NegativeInfinity, 0);
            Assert.DoesNotThrow(() => vector3d.ToUnityVector3());
        }

        #endregion
    }
}
