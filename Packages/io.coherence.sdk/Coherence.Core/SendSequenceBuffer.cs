// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System.Collections.Generic;
    using System.Linq;
    using Brook;
    using Common.Pooling;
    using Serializer;

    internal class SendSequenceBufferEntry
    {
        public bool Sent;
        public bool Acked;
        public SerializedEntityMessage Message;
    }

    /// <summary>
    /// SentSequenceCache records message ids sent with a packet.
    /// </summary>
    internal class SentSequenceCache
    {
        private struct CacheItem
        {
            public List<MessageID> MessageIDs;
        }

        private readonly List<CacheItem> cache = new();

        private readonly ListPool<MessageID> sentPool = new(32, poolPrefillSize: 16);

        public void EnqueueEmpty() => cache.Insert(0, new CacheItem());

        public void Enqueue(IReadOnlyList<MessageID> messageIds)
        {
            var sent = sentPool.Rent();
            sent.AddRange(messageIds);

            var cacheItem = new CacheItem
            {
                MessageIDs = sent,
            };
            cache.Insert(0, cacheItem);
        }

        public bool Dequeue(List<MessageID> messageIdBuffer)
        {
            if (cache.Count == 0)
            {
                return false;
            }

            var item = cache[^1];
            cache.RemoveAt(cache.Count - 1);

            if (item.MessageIDs != null)
            {
                messageIdBuffer.AddRange(item.MessageIDs);

                sentPool.Return(item.MessageIDs);
            }

            return true;
        }

        public void Clear() => cache.Clear();
    }

    /// <summary>
    /// SendSequenceBuffer contains a list of ordered messages to be sent or in-flight.
    /// It works like a ring buffer, but it cannot overflow, ensuring that no
    /// more messages are being processed that the receiving end can buffer.
    /// </summary>
    internal class SendSequenceBuffer : SequenceBuffer<SendSequenceBufferEntry>
    {
        // The current message ID (most recently added to the buffer).
        private MessageID currentID = new MessageID(0);
        // The next message ID in the sequence pending ack.
        private MessageID nextAckID = new MessageID(0);

        public SendSequenceBuffer(int size) : base(size)
        {
        }

        // Appends a message to the send sequence, returns false if there is no space.
        public bool AppendMessage(SerializedEntityMessage message)
        {
            if (IsFull())
            {
                return false;
            }

            Insert(currentID, new SendSequenceBufferEntry()
            {
                Sent = false,
                Acked = false,
                Message = message,
            });

            currentID = currentID.Next();

            return true;
        }

        public void OnMessagesDelivered(IReadOnlyList<MessageID> messageIDs, bool wasDelivered)
        {
            if (messageIDs != null)
            {
                for (var i = 0; i < messageIDs.Count; i++)
                {
                    var id = messageIDs[i];

                    var entry = Find(id);
                    if (!entry.HasValue)
                    {
                        continue; // Is this possible?
                    }

                    if (wasDelivered)
                    {
                        entry.Value.Acked = true;
                    }
                    else
                    {
                        entry.Value.Sent = false;
                    }

                    Insert(id, entry.Value);
                }
            }

            var nextEntry = Find(nextAckID);
            while (nextEntry.HasValue && nextEntry.Value.Acked)
            {
                nextEntry.Value.Message.Dispose();
                Remove(nextAckID);
                nextAckID = nextAckID.Next();
                nextEntry = Find(nextAckID);
            }
        }

        public void OnMessagesSent(List<MessageID> messageIDs)
        {
            if (messageIDs == null)
            {
                return;
            }

            foreach (var id in messageIDs)
            {
                var entry = Find(id);
                if (entry.HasValue)
                {
                    entry.Value.Sent = true;
                    Insert(id, entry.Value);
                }
            }
        }

        public void GetMessagesToSend(List<(MessageID ID, SerializedEntityMessage Message)> messages)
        {
            messages.Clear();

            var id = nextAckID;
            var entry = Find(id);
            while (entry.HasValue && !id.Equals(currentID))
            {
                if (!entry.Value.Acked && !entry.Value.Sent)
                {
                    messages.Add((id, entry.Value.Message));
                }

                id = id.Next();
                entry = Find(id);
            }
        }

        public bool IsFull() => nextAckID.Distance(currentID) >= Size;
        public bool IsEmpty() => nextAckID.Distance(currentID) == 0;
        public int Length() => nextAckID.Distance(currentID);

        public void Clear()
        {
            currentID = new MessageID(0);
            nextAckID = new MessageID(0);
            ClearBuffer();
        }
    }
}
