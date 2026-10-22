// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Transport.Tests
{
    using Coherence.Tests;
    using Coherence.Transport.Web;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using UnityEngine;
    using UnityEngine.TestTools;

    /// <summary>
    /// Tests for WebInterop callback dispatch logic (Coherence.Connection.Web assembly).
    /// Uses reflection to access private statics because the callbacks are only reachable
    /// via native P/Invoke on WebGL; on other platforms we inject entries into the
    /// internal dictionary directly.
    /// </summary>
    public class WebInteropTests : CoherenceTest
    {
        private const int ConnectionId = 42;

        #region OnConnect

        [Test]
        public void OnConnect_DispatchesOnOpen_ForRegisteredId()
        {
            bool opened = false;
            using var registration = new CallbackRegistration(out int id, () => opened = true, _ => { }, _ => { });
            Invoke(WebInterop.Property.OnConnect, id);
            Assert.That(opened, Is.True);
        }

        [Test]
        public void OnConnect_UnknownId_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Invoke(WebInterop.Property.OnConnect, int.MaxValue));
        }

        #endregion

        #region OnPacket

        [Test]
        public void OnPacket_DispatchesCorrectBytes_ForRegisteredId()
        {
            byte[] received = null;
            var expected = new byte[] { 10, 20, 30, 40 };
            using var registration = new CallbackRegistration(out int id, () => { }, data => received = data, _ => { });
            var handle = GCHandle.Alloc(expected, GCHandleType.Pinned);
            try
            {
                Invoke(WebInterop.Property.OnPacket, id, expected.Length, handle.AddrOfPinnedObject());
                Assert.That(received, Is.EqualTo(expected));
            }
            finally { handle.Free(); }
        }

        [Test]
        public void OnPacket_UnknownId_DoesNotThrow()
        {
            var data = new byte[] { 1, 2, 3 };
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Assert.DoesNotThrow(() => Invoke(WebInterop.Property.OnPacket, int.MaxValue, data.Length, handle.AddrOfPinnedObject()));
            }
            finally { handle.Free(); }
        }

        #endregion

        #region OnError

        [Test]
        public void OnError_DispatchesDeserializedError_ForRegisteredId()
        {
            JsError? received = null;
            using var registration = new CallbackRegistration(out int id, () => { }, _ => { }, e => received = e);
            const string json = @"{""statusCode"":404,""errorMessage"":""not found"",""errorType"":""offerError""}";
            Invoke(WebInterop.Property.OnError, id, json);
            Assert.That(received, Is.Not.Null);
            Assert.That(received.Value.StatusCode, Is.EqualTo(404));
            Assert.That(received.Value.ErrorMessage, Is.EqualTo("not found"));
            Assert.That(received.Value.ErrorType, Is.EqualTo(ErrorType.OfferError));
        }

        [Test]
        public void OnError_UnknownId_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Invoke(WebInterop.Property.OnError, int.MaxValue, @"{""statusCode"":0}"));
        }

        [Test]
        public void OnError_MalformedJson_CatchesAndLogsInternally()
        {
            using var registration = new CallbackRegistration(out int id, () => { }, _ => { }, _ => { });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("JSON serialization error"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("OnError"));
            Assert.DoesNotThrow(() => Invoke(WebInterop.Property.OnError, id, "not valid json {{"));
        }

        #endregion

        #region Helpers

        private static Dictionary<int, WebCallbacks> GetCallbacksDictionary()
        {
            var field = typeof(WebInterop).GetField(WebInterop.Property.callbacks, BindingFlags.NonPublic | BindingFlags.Static);
            return (Dictionary<int, WebCallbacks>)field.GetValue(null);
        }

        private static void Invoke(string method, params object[] args)
        {
            var methodInfo = typeof(WebInterop).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            methodInfo.Invoke(null, args);
        }

        private sealed class CallbackRegistration : IDisposable
        {
            public CallbackRegistration(out int connectionId, Action onOpen, Action<byte[]> onPacket, Action<JsError> onError)
            {
                connectionId = ConnectionId;
                GetCallbacksDictionary()[ConnectionId] = new WebCallbacks { OnOpen = onOpen, OnPacket = onPacket, OnError = onError };
            }

            public void Dispose() => GetCallbacksDictionary().Remove(ConnectionId);
        }

        #endregion
    }
}
