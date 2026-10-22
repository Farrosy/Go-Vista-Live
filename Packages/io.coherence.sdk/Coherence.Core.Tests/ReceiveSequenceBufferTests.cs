// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using System.Collections.Generic;
    using Brook;
    using Coherence.Common.Tests;
    using Coherence.Tests;
    using Connection;
    using Entities;
    using NUnit.Framework;
    using ProtocolDef;
    using Assert = UnityEngine.Assertions.Assert;

    public class ReceiveSequenceBufferTests : CoherenceTest
    {
        private const int BufferSize = 128;

        private ReceiveSequenceBuffer buffer;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            buffer = new ReceiveSequenceBuffer(BufferSize);
        }

        [Test(Description = "Already processed messages are not re-inserted in the buffer")]
        public void DropsOldMessages()
        {
            // Add a few messages [0, 1, 2, 3, 4, -, 6, -]
            for (ushort i = 0; i < 8; i++)
            {
                if ((i > 4) && (i % 2 != 0))
                {
                    continue;
                }

                var res = buffer.InsertMessage(new MessageID(i), CreateMessage(i));
                Assert.IsTrue(res);
            }

            // Flush next messages in the sequence.
            var nextMessages = new List<CommandData>();
            buffer.FlushMessages(nextMessages);

            Assert.AreEqual(5, nextMessages.Count);
            Assert.AreEqual((ushort)0, UShortCommand.From(nextMessages[0]).Value);
            Assert.AreEqual((ushort)4, UShortCommand.From(nextMessages[^1]).Value);

            // Verify that we cannot add already processed messages.
            for (ushort i = 0; i <= 4; i++)
            {
                var res = buffer.InsertMessage(new MessageID(i), CreateMessage(i));
                Assert.IsFalse(res);
            }

            Assert.IsTrue(
                buffer.InsertMessage(new MessageID(5), CreateMessage(5)),
                "Should not drop valid message id"
                );
        }

        [Test(Description = "Messages are only flushed when the next message id in the sequence has been received")]
        public void Flush_OnlyWhenNextMessageIsAvailable()
        {
            // First messages are dropped, a few later ones are added [-, -, 2, 3, 4]
            for (ushort i = 0; i < 5; i++)
            {
                if (i < 2)
                {
                    continue;
                }

                var res = buffer.InsertMessage(new MessageID(i), CreateMessage(i));
                Assert.IsTrue(res);
            }

            // Flush next messages in the sequence.
            var nextMessages = new List<CommandData>();
            buffer.FlushMessages(nextMessages);

            Assert.AreEqual(0, nextMessages.Count, "Sequence should not be ready yet");

            // First messages in the sequence arrive later [0, 1, 2, 3, 4]
            for (ushort i = 0; i < 2; i++)
            {
                var res = buffer.InsertMessage(new MessageID(i), CreateMessage(i));
                Assert.IsTrue(res);
            }

            // Flush should work this time.
            buffer.FlushMessages(nextMessages);

            Assert.AreEqual(5, nextMessages.Count);
            for (var i = 0; i < nextMessages.Count; i++)
            {
                Assert.AreEqual((ushort)i, UShortCommand.From(nextMessages[i]).Value);
            }
        }

        [Test(Description = "Test message ids sequence window being updated as received messages are flushed")]
        public void SequenceWindow()
        {
            var halfSize = BufferSize / 2;

            // Fill half of the buffer [0, 1,..., 63, -, ...].
            for (ushort i = 0; i < halfSize; ++i)
            {
                var res = buffer.InsertMessage(new MessageID(i), CreateMessage(i));
                Assert.IsTrue(res);
            }

            // Flush next messages in the sequence.
            var nextMessages = new List<CommandData>();
            buffer.FlushMessages(nextMessages);

            Assert.AreEqual(halfSize, nextMessages.Count);

            // Fill the buffer again [128, 129, ..., 191, 64, 65,..., 127]
            for (var i = (ushort)halfSize; i < (halfSize + BufferSize); i++)
            {
                var res = buffer.InsertMessage(new MessageID(i), CreateMessage(i));
                Assert.IsTrue(res);
            }

            buffer.FlushMessages(nextMessages);

            Assert.AreEqual(BufferSize, nextMessages.Count);
            for (var i = 0; i < nextMessages.Count; i++)
            {
                Assert.AreEqual((ushort)(i + halfSize), UShortCommand.From(nextMessages[i]).Value);
            }
        }

        private CommandData CreateMessage(ushort id) => UShortCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, MessageTarget.All, id);
    }
}
