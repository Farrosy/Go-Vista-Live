// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Utils;
    using Common;
    using Moq;
    using UnityEngine;
    using Utils;

    /// <summary>
    /// Can be used to <see cref="Build"/> a mock <see cref="IRequestFactoryInternal"/>
    /// object for use in a test.
    /// </summary>
    internal sealed class MockRequestFactoryBuilder
    {
        /// <summary>
        /// <see cref="LoginResponse.SessionToken"/> value that the mock returns be default when
        /// <see cref="IRequestFactoryInternal.SendRequestAsync"/> is called, if <see cref="SetSessionToken"/>
        /// is not used to override it.
        /// </summary>
        public const string DefaultSessionToken = "DefaultSessionToken";

        private IRequestFactoryInternal requestFactory;
        private Mock<IRequestFactoryInternal> mock;
        private bool isReady = true;
        private float connectDelay;
        private bool canConnectWebSocket = true;
        private Func<bool> isReadyGetter;
        private Func<Task<string>> sendRequestAsyncResult;
        private RequestException sendRequestAsyncThrows;
        private string sessionToken = DefaultSessionToken;
        private (string basePath, string pathParams, string method, string body, Dictionary<string, string> headers, string requestName, string sessionToken)? sendRequestAsyncWasCalledWith;
        private Func<string, string, string, string, Dictionary<string, string>, string, string, Task<string>> sendRequestAsyncFunc;
        private Func<Task<string>>[] sendRequestAsyncSequence;
        private string[] sendRequestSequence;
        private string[] sendCustomRequestSequence;
        private string[] sendCustomRequestAsyncSequence;
        private bool buildExecuted;
        private readonly Dictionary<string, List<Action<string>>> pushCallbacks = new();
        private string pushResumeId;
        private string addPushCallbackThrowsFor;
        public bool SendRequestAsyncWasCalled => sendRequestAsyncWasCalledWith.HasValue;

        private (string pathParams, string body)? sendRequestWasCalledWith;

        public bool SendRequestWasCalled => sendRequestWasCalledWith.HasValue;

        /// <summary>The path and body of the last callback-based <c>SendRequest</c> call.</summary>
        public (string pathParams, string body) SendRequestWasCalledWith => sendRequestWasCalledWith.Value;
        public (string basePath, string pathParams, string method, string body, Dictionary<string, string> headers, string requestName, string sessionToken) SendRequestAsyncWasCalledWith => sendRequestAsyncWasCalledWith.Value;
        public IRequestFactoryInternal RequestFactory => Build();

        public MockRequestFactoryBuilder() => isReadyGetter = ()=> isReady;

        public MockRequestFactoryBuilder SetSessionToken(string sessionToken)
        {
            this.sessionToken = sessionToken;
            return this;
        }

        /// <summary>Makes <see cref="IRequestFactory.AddPushCallback"/> throw for <paramref name="path"/>.</summary>
        public MockRequestFactoryBuilder AddPushCallbackThrows(string path)
        {
            addPushCallbackThrowsFor = path;
            return this;
        }

        public MockRequestFactoryBuilder SetIsReady(bool isReady = true)
        {
            this.isReady = isReady;
            return this;
        }

        public MockRequestFactoryBuilder SetCanConnectWebSocket(bool canConnectWebSocket = true)
        {
            this.canConnectWebSocket = canConnectWebSocket;
            return this;
        }

        public MockRequestFactoryBuilder SetConnectDelay(float connectDelay)
        {
            this.connectDelay = connectDelay;
            return this;
        }

        public MockRequestFactoryBuilder SendRequestAsyncReturns(string result) => SendRequestAsyncReturns(Task.FromResult(result));
        public MockRequestFactoryBuilder SendRequestAsyncReturns(Task<string> result) => SendRequestAsyncReturns(()=> result);
        public MockRequestFactoryBuilder SendRequestAsyncReturns(Func<Task<string>> getResult)
        {
            sendRequestAsyncResult = getResult;
            return this;
        }

        public MockRequestFactoryBuilder OnSendRequestAsyncCalled(RequestException throws)
        {
            sendRequestAsyncThrows = throws;
            return this;
        }

        /// <summary>
        /// Delegate that gets executed when the method
        /// <see cref="IRequestFactory.SendRequestAsync(string, string, string, string, Dictionary{string, string}, string, string)"/>
        /// is called, which returns a value of type <see cref="Task{String}"/>.
        /// </summary>
        public MockRequestFactoryBuilder SendRequestAsyncReturns(Func<string, string, string, string, Dictionary<string, string>, string, string, Task<string>> func)
        {
            sendRequestAsyncFunc = func;
            return this;
        }

        /// <summary>
        /// Delegate that gets executed when the method
        /// <see cref="IRequestFactory.SendRequestAsync(string, string, string, string, Dictionary{string, string}, string, string)"/>
        /// is called, which returns a value of type <see cref="Task{String}"/>.
        /// </summary>
        public MockRequestFactoryBuilder SendRequestAsyncReturns(params Func<Task<string>>[] sequence)
        {
            sendRequestAsyncSequence = sequence;
            return this;
        }

        /// <summary>
        /// Configures the mock to invoke the <see cref="IRequestFactory.SendRequest"/> callback in sequence,
        /// once per call, each time with <see cref="RequestStatus.Success"/> and the corresponding result string.
        /// </summary>
        public MockRequestFactoryBuilder SendRequestReturns(params string[] sequence)
        {
            sendRequestSequence = sequence;
            return this;
        }

        /// <summary>
        /// Configures the mock to invoke the <see cref="IRequestFactory.SendCustomRequest"/> callback in sequence,
        /// once per call, each time with <see cref="RequestStatus.Success"/> and the corresponding result string.
        /// </summary>
        public MockRequestFactoryBuilder SendCustomRequestReturns(params string[] sequence)
        {
            sendCustomRequestSequence = sequence;
            return this;
        }

        /// <summary>
        /// Configures the mock to return each string in sequence from <see cref="IRequestFactory.SendCustomRequestAsync"/>,
        /// wrapped in a completed <see cref="Task{String}"/>.
        /// </summary>
        public MockRequestFactoryBuilder SendCustomRequestAsyncReturns(params string[] sequence)
        {
            sendCustomRequestAsyncSequence = sequence;
            return this;
        }

        public IRequestFactoryInternal Build()
        {
            if (buildExecuted)
            {
                return requestFactory ?? throw new NullReferenceException($"{GetType().Name}.Build was called again while previous Build execution is still in progress!");
            }

            buildExecuted = true;
            mock = new(MockBehavior.Strict);
            SetupSendRequestAsync(mock);
            SetupSendRequest(mock);
            SetupSendCustomRequest(mock);
            SetupSendCustomRequestAsync(mock);
            SetupIsReady(mock);
            SetupCanConnectWebSocket(mock);
            SetupThrottle(mock);
            mock.Setup(x => x.GetRequestCooldown(It.IsAny<string>(), It.IsAny<string>())).Returns(TimeSpan.Zero);
            mock.Setup(x => x.RemovePushCallback(It.IsAny<string>(), It.IsAny<Action<string>>())).Callback<string, Action<string>>((path, callback) =>
            {
                if (pushCallbacks.TryGetValue(path, out var callbacks))
                {
                    callbacks.Remove(callback);
                }
            });
            mock.Setup(x => x.AddPushCallback(It.IsAny<string>(), It.IsAny<Action<string>>())).Callback<string, Action<string>>((path, callback) =>
            {
                if (path == addPushCallbackThrowsFor)
                {
                    throw new InvalidOperationException($"Adding a push callback for '{path}' failed.");
                }

                if (!pushCallbacks.TryGetValue(path, out var callbacks))
                {
                    pushCallbacks[path] = callbacks = new();
                }

                callbacks.Add(callback);
            });
            mock.Setup(x => x.ForceCreateWebSocket()).Callback(Connect);
            mock.SetupGet(x => x.PushResumeId).Returns(() => pushResumeId);
            requestFactory = mock.Object;
            return requestFactory;
        }

        public void Connect() => ConnectAsync().Then(task => Debug.LogException(task.Exception), TaskContinuationOptions.OnlyOnFaulted);

        public async Task ConnectAsync()
        {
            if (!canConnectWebSocket)
            {
                return;
            }

            if (connectDelay > 0f)
            {
                await Wait.For(TimeSpan.FromSeconds(connectDelay));
            }

            SetIsReady();
            RaiseOnWebSocketConnect();
        }

        public void Disconnect()
        {
            SetIsReady(false);
            RaiseOnWebSocketDisconnect();
        }

        public void RaiseOnWebSocketConnect()
        {
            Build();
            mock.Raise(mock => mock.OnWebSocketConnect += null);
        }

        public void RaiseOnWebSocketDisconnect()
        {
            Build();
            mock.Raise(mock => mock.OnWebSocketDisconnect += null);
        }

        public void RaiseOnWebSocketConnectionError()
        {
            Build();
            mock.Raise(mock => mock.OnWebSocketConnectionError += null);
        }

        /// <summary>
        /// Invokes every callback registered for <paramref name="path"/> via
        /// <see cref="IRequestFactory.AddPushCallback"/>, simulating the server sending a push message.
        /// </summary>
        /// <param name="resumeId">The push's <c>resume_id</c>, reported by the factory while its callbacks run.</param>
        public void RaisePushCallback(string path, string responseBody = "", string resumeId = null)
        {
            Build();
            if (!pushCallbacks.TryGetValue(path, out var callbacks) || callbacks.Count is 0)
            {
                throw new InvalidOperationException($"No push callback has been registered for path \"{path}\".");
            }

            pushResumeId = resumeId;

            try
            {
                foreach (var callback in callbacks.ToArray())
                {
                    callback(responseBody);
                }
            }
            finally
            {
                pushResumeId = null;
            }
        }

        /// <summary>
        /// How many callbacks are currently registered for <paramref name="path"/> via
        /// <see cref="IRequestFactory.AddPushCallback"/>, for asserting that nothing piles up or leaks.
        /// </summary>
        public int GetPushCallbackCount(string path)
            => pushCallbacks.TryGetValue(path, out var callbacks) ? callbacks.Count : 0;

        private void SetupSendRequest(Mock<IRequestFactoryInternal> mock)
        {
            if (sendRequestSequence is null)
            {
                return;
            }

            mock.Setup(requestFactory => requestFactory.SendRequest(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Action<RequestResponse<string>>>(),
                    It.IsAny<bool>()))
                .Callback<string, string, string, Dictionary<string, string>, string, string, Action<RequestResponse<string>>, bool>(
                    (basePath, method, body, headers, requestName, sessionToken, callback, silenceWarning)
                        => mock.Object.SendRequest(basePath, "", method, body, headers, requestName, sessionToken, callback, silenceWarning));

            var sequence = new MockSequence();
            foreach (var result in sendRequestSequence)
            {
                var captured = result;
                mock.InSequence(sequence)
                    .Setup(requestFactory => requestFactory.SendRequest(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Dictionary<string, string>>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Action<RequestResponse<string>>>(),
                        It.IsAny<bool>()))
                    .Callback<string, string, string, string, Dictionary<string, string>, string, string, Action<RequestResponse<string>>, bool>(
                        (_, pathParams, _, body, _, _, _, cb, _) =>
                        {
                            sendRequestWasCalledWith = (pathParams, body);
                            cb.Invoke(new RequestResponse<string>
                            {
                                Result = captured,
                                Status = RequestStatus.Success,
                            });
                        });
            }
        }

        private void SetupSendCustomRequest(Mock<IRequestFactoryInternal> mock)
        {
            if (sendCustomRequestSequence is null)
            {
                return;
            }

            var sequence = new MockSequence();
            foreach (var result in sendCustomRequestSequence)
            {
                var captured = result;
                mock.InSequence(sequence)
                    .Setup(requestFactory => requestFactory.SendCustomRequest(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Action<RequestResponse<string>>>()))
                    .Callback<string, string, string, string, Action<RequestResponse<string>>>(
                        (_, _, _, _, cb) => cb.Invoke(new RequestResponse<string>
                        {
                            Result = captured,
                            Status = RequestStatus.Success,
                        }));
            }
        }

        private void SetupSendCustomRequestAsync(Mock<IRequestFactoryInternal> mock)
        {
            if (sendCustomRequestAsyncSequence is null)
            {
                return;
            }

            var sequence = new MockSequence();
            foreach (var result in sendCustomRequestAsyncSequence)
            {
                var captured = result;
                mock.InSequence(sequence)
                    .Setup(requestFactory => requestFactory.SendCustomRequestAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns(() => Task.FromResult(captured));
            }
        }

        private void SetupSendRequestAsync(Mock<IRequestFactoryInternal> mock)
        {
            mock.Setup(GetSendRequestAsyncWithoutPathParamsSetupExpression())
                .Returns((string basePath, string method, string body, Dictionary<string, string> headers, string requestName, string sessionToken, bool silenceWarning)
                    => mock.Object.SendRequestAsync(basePath, "", method, body, headers, requestName, sessionToken, silenceWarning));

            if (sendRequestAsyncSequence is not null)
            {
                var sequence = new MockSequence();
                var setup = GetSendRequestAsyncWithPathParamsSetupExpression();
                foreach (var func in sendRequestAsyncSequence)
                {
                    mock.InSequence(sequence).Setup(setup).Returns(func);
                }
            }
            else
            {
                var setup = mock.Setup(GetSendRequestAsyncWithPathParamsSetupExpression());

                setup.Callback((string basePath, string pathParams, string method, string body, Dictionary<string, string> headers, string requestName, string sessionToken, bool _)
                    => sendRequestAsyncWasCalledWith = (basePath, pathParams, method, body, headers, requestName, sessionToken));

                if (sendRequestAsyncFunc is not null)
                {
                    var func = sendRequestAsyncFunc;
                    setup.Returns((string basePath, string pathParams, string method, string body, Dictionary<string, string> headers, string requestName, string sessionToken, bool _)
                        => func(basePath, pathParams, method, body, headers, requestName, sessionToken));
                }
                else
                {
                    setup.Returns(GetSendRequestAsyncResult);
                }
            }

            Expression<Func<IRequestFactoryInternal, Task<string>>> GetSendRequestAsyncWithoutPathParamsSetupExpression() => requestFactory => requestFactory.SendRequestAsync
            (
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()
            );

            Expression<Func<IRequestFactoryInternal, Task<string>>> GetSendRequestAsyncWithPathParamsSetupExpression() => requestFactory => requestFactory.SendRequestAsync
            (
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()
            );

            Task<string> GetSendRequestAsyncResult()
            {
                if (sendRequestAsyncThrows is not null)
                {
                    return Task.FromException<string>(sendRequestAsyncThrows);
                }

                if (sendRequestAsyncResult is not null)
                {
                    return sendRequestAsyncResult();
                }

                var loginResponse = new LoginResponse { sessionToken = sessionToken };
                return Task.FromResult(CoherenceJson.SerializeObject(loginResponse));
            }
        }

        private void SetupIsReady(Mock<IRequestFactoryInternal> requestFactoryMock) => requestFactoryMock.SetupGet(factory => factory.IsReady).Returns(isReadyGetter);
        private void SetupCanConnectWebSocket(Mock<IRequestFactoryInternal> requestFactoryMock) => requestFactoryMock.SetupGet(factory => factory.CanConnectWebSocket).Returns(canConnectWebSocket);

        private void SetupThrottle(Mock<IRequestFactoryInternal> requestFactoryMock)
        {
            var throttle = new Mock<RequestThrottle>(TimeSpan.Zero, new Mock<IDateTimeProvider>().Object);
            throttle.Setup(x => x.WaitForCooldown(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            requestFactoryMock.SetupGet(factory => factory.Throttle).Returns(throttle.Object);
        }
    }
}
