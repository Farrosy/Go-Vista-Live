// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tests
{
    using System;
    using NUnit.Framework;

    internal interface IAllocCounter
    {
        long Stop();
    }

#if UNITY_5_3_OR_NEWER
    // Borrowed directly from UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint
    internal class UnityAllocCounter : IAllocCounter
    {
        private UnityEngine.Profiling.Recorder _rec;

        public UnityAllocCounter()
        {
            _rec = UnityEngine.Profiling.Recorder.Get("GC.Alloc");

            // The recorder was created enabled, which means it captured the creation of the
            // Recorder object itself, etc. Disabling it flushes its data, so that we can retrieve
            // the sample block count and have it correctly account for these initial allocations.
            _rec.enabled = false;

#if !UNITY_WEBGL
            _rec.FilterToCurrentThread();
#endif

            _rec.enabled = true;
        }

        public long Stop()
        {
            if (_rec == null)
            {
                throw new Exception("UnityAllocCounter already stopped");
            }

            _rec.enabled = false;

#if !UNITY_WEBGL
            _rec.CollectFromAllThreads();
#endif

            var res = _rec.sampleBlockCount;
            _rec = null;
            return res;
        }
    }
#endif

    internal class DotnetAllocCounter : IAllocCounter
    {
        private long? allocatedByteBefore;

        public DotnetAllocCounter()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            allocatedByteBefore = GC.GetAllocatedBytesForCurrentThread();
        }

        public long Stop()
        {
            if (allocatedByteBefore == null)
            {
                throw new Exception("DotnetAllocCounter already stopped");
            }

            var res = GC.GetAllocatedBytesForCurrentThread() - allocatedByteBefore.Value;
            allocatedByteBefore = null;

            return res;
        }
    }
    
    public class AllocCounter
    {
        /// <summary>
        /// Number of times the action will be invoked during the JIT warm-up phase.
        /// </summary>
        public const int WARM_UP_COUNT = 10;

        /// <summary>
        /// Number of times the action will be invoked during the measurement phase.
        /// </summary>
        public const int MEASURE_COUNT = 3;

        /// <summary>
        /// Number of times the action will be invoked during teh warm-up and measurement phase together.
        /// </summary>
        public const int TOTAL_INVOKE_COUNT = WARM_UP_COUNT + MEASURE_COUNT;

        private static long Instrument(Action action)
        {
#if UNITY_5_3_OR_NEWER
            var counter = new UnityAllocCounter();
#else
            var counter = new DotnetAllocCounter();
#endif

            long allocs;
            try
            {
                action();
            }
            finally
            {
                allocs = counter.Stop();
            }

            return allocs;
        }

        /// <summary>
        /// Runs action multiple times to warm up the JIT, then measures allocations across
        /// several runs and returns the median. Taking the median guards against spurious
        /// allocation blocks caused by JIT tiered recompilation firing between the warmup loop
        /// and a measurement, which would produce a false positive on zero-alloc assertions,
        /// while avoiding the risk that minimum would hide real allocations if a single
        /// measurement returned an anomalously low count.
        /// It is important that the action is idempotent!
        /// </summary>
        public static long InstrumentWithWarmup(Action action)
        {
            for (var i = 0; i < WARM_UP_COUNT; i++)
            {
                action();
            }

            var counts = new long[MEASURE_COUNT];
            for (var i = 0; i < MEASURE_COUNT; i++)
            {
                counts[i] = Instrument(action);
            }

            Array.Sort(counts);
            return counts[MEASURE_COUNT / 2];
        }

        /// <summary>
        /// Runs action multiple time to warm up the JIT, then once more and asserts
        /// that no allocation happened in the last run.
        /// It is important that the action is idempotent!
        /// </summary>
        public static void AssertNoAllocs(Action action) => Assert.That(InstrumentWithWarmup(action), Is.Zero);
    }
}
