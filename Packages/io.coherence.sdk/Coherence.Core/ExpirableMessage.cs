// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System;
    using ProtocolDef;

    internal struct ExpirableMessage<T> where T : IMessageData<T>
    {
        public readonly T Message;
        public readonly DateTime ExpirationDate;

        public ExpirableMessage(T message, DateTime expirationDate)
        {
            Message = message;
            ExpirationDate = expirationDate;
        }

        public bool HasExpired(in DateTime now) => now >= ExpirationDate;

        public void Dispose()
        {
            Message.Dispose();
        }
    }
}
