// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Cloud;

    internal interface IWebSocketManager : IDisposable
    {
        bool Enabled { get; }
        bool IsConnected();
        event Action OnConnect;
        event Action OnDisconnect;
        event Action OnWebSocketFail;
        event Action<string> OnWebSocketParametersNotValid;
        bool AddPushCallback(string requestID, OnRequest callback);

        /// <summary>The <c>resume_id</c> of the push being dispatched, if it carries one.</summary>
        /// <remarks>Only set while a push callback runs; <see langword="null"/> otherwise.</remarks>
        [MaybeNull]
        string PushResumeId { get; }
        void Connect();
        void SendRequest(string path, string method, string body, Dictionary<string, string> headers, string sessionToken, OnRequest callback);
        void Update();
    }
}
