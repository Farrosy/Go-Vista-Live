// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log
{
    using System;
    using System.Diagnostics;

    public class LazyLogger
    {
        public Logger Logger {
            get => logger.Value;
            internal set => logger = new Lazy<Logger>(() => value, false);
        }
        private Lazy<Logger> logger;

        public LazyLogger(Func<Logger> loggerFactory)
        {
            logger = new Lazy<Logger>(loggerFactory, false);
        }

#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        [Conditional(LogConditionals.Trace)]
        public virtual void Trace(string log, params (string key, object value)[] args)
        {
            logger.Value.Trace(log, args);
        }

#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        [Conditional(LogConditionals.Debug)]
        public virtual void Debug(string log, params (string key, object value)[] args)
        {
            logger.Value.Debug(log, args);
        }

#if COHERENCE_DISABLE_LOG_INFO
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        public virtual void Info(string log, params (string key, object value)[] args)
        {
            logger.Value.Info(log, args);
        }

#if COHERENCE_DISABLE_LOG_WARNING
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        public virtual void Warning(Warning id, params (string key, object value)[] args)
        {
            logger.Value.Warning(id, args);
        }

#if COHERENCE_DISABLE_LOG_WARNING
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        public virtual void Warning(Warning id, string msg, params (string key, object value)[] args)
        {
            logger.Value.Warning(id, msg, args);
        }

#if COHERENCE_DISABLE_LOG_ERROR
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        public virtual void Error(Error id, params (string key, object value)[] args)
        {
            logger.Value.Error(id, args);
        }

#if COHERENCE_DISABLE_LOG_ERROR
        [Conditional(LogConditionals.False)]
#endif
#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        public virtual void Error(Error id, string msg, params (string key, object value)[] args)
        {
            logger.Value.Error(id, msg, args);
        }
    }
}
