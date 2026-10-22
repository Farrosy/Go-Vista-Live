// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

// If these tests fail, then it's likely the code gen is broken.
// Take a look at README_BAKING in Coherence.Common.Tests to regenerate the code.

namespace Coherence.Core.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Brook;
    using Brook.Octet;
    using Entities;
    using NUnit.Framework;
    using ProtocolDef;
    using Serializer;
    using Coherence.Tests;
    using Coherence.Common.Tests;
    using System.Numerics;
    using Coherence.Common;
    using UnityEngine.TestTools.Utils;

    public class SerializeInputsTest : CoherenceTest
    {
        private bool useDebugStreams = false;
        private readonly Entity entityID = new Entity((Index)1, 0, Entity.Relative);

        private MessageSerializer messageSerializer;
        private MessageDeserializer messageDeserializer;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            messageSerializer = new MessageSerializer(this.logger);
            messageDeserializer = new MessageDeserializer(TestSchema.DataFactory, this.logger);
        }

        private (uint, uint) SerializeMessageBuffer(Queue<SerializedEntityMessage> inputs, int packetSize)
        {
            (int _, uint expectedBitCount) =
                messageSerializer.GetCountFromBudget(inputs, (uint)packetSize, packetSize, false, useDebugStreams, (uint)packetSize);

            var packetStream = new OutOctetStream(packetSize);
            var outBitStream = useDebugStreams
                ? new DebugOutBitStream(new OutBitStream(packetStream))
                : (IOutBitStream)new OutBitStream(packetStream);

            messageSerializer.WriteMessages(new List<SerializedEntityMessage>(), MessageType.Input, inputs, new SerializerContext<IOutBitStream>(outBitStream, useDebugStreams, logger));

            var actualBitCount = outBitStream.Position;

            return (actualBitCount, expectedBitCount);
        }

        private InputData[] ProcessInputs(Queue<SerializedEntityMessage> inputs, int packetSize,
            out int numSerialized)
        {
            var packetStream = new OutOctetStream(packetSize);
            var outBitStream = useDebugStreams
                ? new DebugOutBitStream(new OutBitStream(packetStream))
                : (IOutBitStream)new OutBitStream(packetStream);

            var inputsSent = new List<SerializedEntityMessage>();
            messageSerializer.WriteMessages(inputsSent, MessageType.Input, inputs, new SerializerContext<IOutBitStream>(outBitStream, useDebugStreams, logger));
            numSerialized = inputsSent.Count();

            outBitStream.Flush();

            var octetReader = new InOctetStream(packetStream.Close().ToArray());
            var inBitStream = useDebugStreams
                ? new DebugInBitStream(new InBitStream(octetReader, (int)packetStream.Position))
                : (IInBitStream)new InBitStream(octetReader, (int)packetStream.Position);

            _ = DeserializeCommands.DeserializeCommand(inBitStream, out _);

            var inputsBuffer = new List<InputData>();
            messageDeserializer.ReadInputs(inBitStream, inputsBuffer);
            return inputsBuffer.ToArray();
        }

        private void CreateInput<T>(ref List<InputData> inputs, ref Queue<SerializedEntityMessage> messageQueue,
            T inputValue)
        {
            InputData input;

            switch (inputValue)
            {
                case bool b:
                    input = BoolInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, b);
                    break;
                case int i:
                    input = IntInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, i);
                    break;
                case float f:
                    input = FloatInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, f);
                    break;
                case Quaternion q:
                    input = QuaternionInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, q);
                    break;
                case float[] fa:
                    var length = fa.Length;
                    if (length == 2)
                    {
                        input = Vector2Input.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, new Vector2(fa[0], fa[1]));
                    }
                    else if (length == 3)
                    {
                        input = Vector3Input.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, new Vector3d(fa[0], fa[1], fa[2]));
                    }
                    else if (length == 4)
                    {
                        input = QuaternionInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0,
                            new Quaternion(fa[0], fa[1], fa[2], fa[3]));
                    }
                    else
                    {
                        throw new System.ArgumentException($"Unsupported float array length: {length}");
                    }

                    break;
                case Vector2 v:
                    input = Vector2Input.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, v);
                    break;
                case Vector3d v:
                    input = Vector3Input.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, v);
                    break;
                case string s:
                    input = StringInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0, s);
                    break;
                case object[] o:
                    var inputTypeName = (string)o[0];
                    if (inputTypeName == nameof(MultiInput))
                    {
                        var fa = (float[])o[3];

                        input = MultiInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0,
                            (int)o[1],
                            (float)o[2],
                            new Vector2(fa[0], fa[1]),
                            (string)o[4]);
                    }
                    else if (inputTypeName == nameof(CompressedInput))
                    {
                        var fa = (float[])o[3];

                        input = CompressedInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0,
                            (int)o[1],
                            (float)o[2],
                            new Vector2(fa[0], fa[1]),
                            (string)o[4]);
                    }
                    else
                    {
                        throw new System.ArgumentException($"Unsupported input type: {inputTypeName}");
                    }

                    break;
                default:
                    throw new System.ArgumentException("Unsupported input value type");
            }

            var serializedMessage = messageSerializer.SerializeInput(input, useDebugStreams);
            messageQueue.Enqueue(serializedMessage);

            inputs.Add(input);
        }

        private void CompareInput(InputData input, InputData original)
        {
            const float
                epsilon = 0.001f; // This should be somehow computed based on the bit range and scale of the floats compression.

            var inputType = input.InputID;
            var originalType = original.InputID;

            Assert.AreEqual(inputType, originalType);

            switch (inputType)
            {
                case BoolInput.InputID:
                    var originalBool = BoolInput.From(original);
                    var inputBool = BoolInput.From(input);

                    Assert.AreEqual(originalBool.BoolField, inputBool.BoolField);
                    break;

                case IntInput.InputID:
                    var originalInt = IntInput.From(original);
                    var inputInt = IntInput.From(input);

                    Assert.AreEqual(originalInt.IntField, inputInt.IntField);
                    break;
                case FloatInput.InputID:
                    var originalFloat = FloatInput.From(original);
                    var inputFloat = FloatInput.From(input);

                    Assert.That(Utils.AreFloatsEqual(originalFloat.FloatField, inputFloat.FloatField, epsilon),
                        Is.True);
                    break;
                case QuaternionInput.InputID:
                    var originalQuaternion = QuaternionInput.From(original);
                    var inputQuaternion = QuaternionInput.From(input);

                    Assert.That(
                        Utils.AreFloatsEqual(originalQuaternion.QuaternionField.X, inputQuaternion.QuaternionField.X,
                            epsilon), Is.True);
                    Assert.That(
                        Utils.AreFloatsEqual(originalQuaternion.QuaternionField.Y, inputQuaternion.QuaternionField.Y,
                            epsilon), Is.True);
                    Assert.That(
                        Utils.AreFloatsEqual(originalQuaternion.QuaternionField.Z, inputQuaternion.QuaternionField.Z,
                            epsilon), Is.True);
                    Assert.That(
                        Utils.AreFloatsEqual(originalQuaternion.QuaternionField.W, inputQuaternion.QuaternionField.W,
                            epsilon), Is.True);
                    break;
                case Vector2Input.InputID:
                    var originalVector2 = Vector2Input.From(original);
                    var inputVector2 = Vector2Input.From(input);

                    Assert.That(
                        Utils.AreFloatsEqual(originalVector2.Vector2Field.X, inputVector2.Vector2Field.X, epsilon),
                        Is.True);
                    Assert.That(
                        Utils.AreFloatsEqual(originalVector2.Vector2Field.Y, inputVector2.Vector2Field.Y, epsilon),
                        Is.True);
                    break;
                case Vector3Input.InputID:
                    var originalVector3 = Vector3Input.From(original);
                    var inputVector3 = Vector3Input.From(input);

                    Assert.That(
                        Utils.AreFloatsEqual((float)originalVector3.Vector3Field.x, (float)inputVector3.Vector3Field.x, epsilon),
                        Is.True);
                    Assert.That(
                        Utils.AreFloatsEqual((float)originalVector3.Vector3Field.y, (float)inputVector3.Vector3Field.y, epsilon),
                        Is.True);
                    Assert.That(
                        Utils.AreFloatsEqual((float)originalVector3.Vector3Field.z, (float)inputVector3.Vector3Field.z, epsilon),
                        Is.True);
                    break;
                case StringInput.InputID:
                    var originalString = StringInput.From(original);
                    var inputString = StringInput.From(input);

                    Assert.AreEqual(originalString.StringField, inputString.StringField);
                    for (int i = 0; i < originalString.StringField.Length; i++)
                    {
                        Assert.AreEqual(originalString.StringField[i], inputString.StringField[i]);
                    }

                    break;
            }
        }

        [Test]
        public void TestBasicInputMessages()
        {
            // Arrange
            var originalInputs = new List<InputData>();
            var messageBuffer = new Queue<SerializedEntityMessage>();

            CreateInput(ref originalInputs, ref messageBuffer, true);
            CreateInput(ref originalInputs, ref messageBuffer, 123);
            CreateInput(ref originalInputs, ref messageBuffer, 123.456f);
            CreateInput(ref originalInputs, ref messageBuffer, Quaternion.CreateFromYawPitchRoll(1.0f, 2.0f, 3.0f));
            CreateInput(ref originalInputs, ref messageBuffer, new Vector2(1.0f, 2.0f));
            CreateInput(ref originalInputs, ref messageBuffer, new Vector3d(1.0f, 2.0f, 3.0f));
            CreateInput(ref originalInputs, ref messageBuffer, "Hello World");

            var numInputs = messageBuffer.Count();

            // Act
            var inputsRes = ProcessInputs(messageBuffer, 1024, out var numSerialized);

            // Assert
            Assert.AreEqual(numInputs, numSerialized);
            Assert.AreEqual(numInputs, inputsRes.Length);

            for (int i = 0; i < inputsRes.Count(); i++)
            {
                var input = inputsRes[i];
                var original = originalInputs[i];

                CompareInput(input, original);
            }
        }

        private void ProcessTooManyInputs(List<InputData> originalInputs,
            Queue<SerializedEntityMessage> messageBuffer, int packetSize)
        {
            // Act
            var inputsRes = ProcessInputs(messageBuffer, packetSize, out var numSerialized);

            UnityEngine.Debug.Log($"inputs processed: {inputsRes.Count()}");

            Assert.NotZero(numSerialized);
            Assert.Less(inputsRes.Count(), originalInputs.Count());

            // Assert
            for (int i = 0; i < inputsRes.Count(); i++)
            {
                var input = inputsRes[i];
                var original = originalInputs[i];

                CompareInput(input, original);
            }
        }

        [Test]
        [TestCase("TooManyBool", 128, new object[]
        {
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            false
        })]
        [TestCase("TooManyInts", 128, new object[]
        {
            0,
            -9999,
            9999,
            1234,
            -1234,
            1212,
            -1212,
            777,
            -777,
            -123,
            123,
            0
        })]
        [TestCase("TooManyFloats", 128, new object[]
        {
            0.0f,
            -2300.0f,
            2300.0f,
            1234.0f,
            -1234.0f,
            1212.0f,
            -1212.0f,
            777.0f,
            -777.0f,
            -123.0f,
            123.0f,
            0.0f
        })]
        [TestCase("TooManyStrings", 128, new object[]
        {
            "Hello",
            "World",
            "Hello",
            "World",
            "Hello",
            "World",
            "Hello",
            "World",
            "Hello",
            "World",
            "Hello",
            "World"
        })]
        [TestCase("TooManyVector2s", 128, new object[]
        {
            new[]
            {
                0.0f,
                1.0f
            },
            new[]
            {
                2.0f,
                3.0f
            },
            new[]
            {
                4.0f,
                5.0f
            },
            new[]
            {
                6.0f,
                7.0f
            },
            new[]
            {
                8.0f,
                9.0f
            },
            new[]
            {
                10.0f,
                11.0f
            },
            new[]
            {
                123.45f,
                123.45f
            },
            new[]
            {
                -123.45f,
                -123.45f
            },
            new[]
            {
                -2300.0f,
                2300.0f
            },
            new[]
            {
                111.0f,
                222.0f
            },
            new[]
            {
                333.0f,
                444.0f
            },
            new[]
            {
                555.0f,
                666.0f
            },
            new[]
            {
                0.0f,
                -1.0f
            },
            new[]
            {
                -2.0f,
                -3.0f
            },
            new[]
            {
                -4.0f,
                -5.0f
            },
            new[]
            {
                -6.0f,
                -7.0f
            },
            new[]
            {
                -8.0f,
                -9.0f
            },
            new[]
            {
                -10.0f,
                -11.0f
            },
        })]
        [TestCase("TooManyVector3s", 128, new object[]
        {
            new[]
            {
                0.0f,
                1.0f,
                2.0f
            },
            new[]
            {
                3.0f,
                4.0f,
                5.0f
            },
            new[]
            {
                6.0f,
                7.0f,
                8.0f
            },
            new[]
            {
                9.0f,
                10.0f,
                11.0f
            },
            new[]
            {
                12.0f,
                13.0f,
                14.0f
            },
            new[]
            {
                15.0f,
                16.0f,
                17.0f
            },
            new[]
            {
                123.45f,
                123.45f,
                123.45f
            },
            new[]
            {
                -123.45f,
                -123.45f,
                -123.45f
            },
            new[]
            {
                -2300.0f,
                2300.0f,
                2300.0f
            },
            new[]
            {
                111.0f,
                222.0f,
                333.0f
            },
            new[]
            {
                444.0f,
                555.0f,
                666.0f
            },
            new[]
            {
                777.0f,
                888.0f,
                999.0f
            },
            new[]
            {
                0.0f,
                -1.0f,
                -2.0f
            },
            new[]
            {
                -3.0f,
                -4.0f,
                -5.0f
            },
            new[]
            {
                -6.0f,
                -7.0f,
                -8.0f
            },
            new[]
            {
                -9.0f,
                -10.0f,
                -11.0f
            },
            new[]
            {
                -12.0f,
                -13.0f,
                -14.0f
            },
            new[]
            {
                -15.0f,
                -16.0f,
                -17.0f
            },
        })]
        [TestCase("TooManyQuaternions", 128, new object[]
        {
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
        })]
        public void TestTooManyInputs(string testName, int packetSize, object[] inputs)
        {
            // Arrange
            var originalInputs = new List<InputData>();
            var messageBuffer = new Queue<SerializedEntityMessage>();
            var numInputs = inputs.Length;

            for (int i = 0; i < numInputs; i++)
            {
                CreateInput(ref originalInputs, ref messageBuffer, inputs[i]);
            }

            ProcessTooManyInputs(originalInputs, messageBuffer, packetSize);
        }

        [TestCase("Empty buffer", new object[]
        {
        })]
        [TestCase("Bool inputs", new object[]
        {
            true,
            false
        })]
        [TestCase("Integer inputs", new object[]
        {
            1,
            -781,
            0
        })]
        [TestCase("Float inputs", new object[]
        {
            2.3f,
            0.0f,
            -543.0123f
        })]
        [TestCase("Quaternion inputs", new object[]
        {
            new[]
            {
                0.0f,
                0.0f,
                0.0f,
                1.0f
            },
        })]
        [TestCase("Vector2 inputs", new object[]
        {
            new[]
            {
                0.0f,
                0.0f
            },
            new[]
            {
                -2300.0f,
                2300.0f
            },
            new[]
            {
                123.45f,
                -123.45f
            },
        })]
        [TestCase("Vector3 inputs", new object[]
        {
            new[]
            {
                0.0f,
                0.0f,
                0.0f
            },
            new[]
            {
                -2300.0f,
                2300.0f,
                2300.0f
            },
            new[]
            {
                123.45f,
                -123.45f,
                123.45f
            },
        })]
        [TestCase("Multiple fields", new object[]
        {
            new object[]
            {
                "MultiInput",
                2,
                1.3f,
                new float[]
                {
                    123,
                    -4
                },
                "string"
            },
            new object[]
            {
                "MultiInput",
                0,
                748.5f,
                new[]
                {
                    0,
                    13.5f
                },
                ""
            },
        })]
        [TestCase("Compressed fields", new object[]
        {
            new object[]
            {
                "CompressedInput",
                2,
                1.3f,
                new float[]
                {
                    123,
                    -4
                },
                "string"
            },
            new object[]
            {
                "CompressedInput",
                0,
                748.5f,
                new[]
                {
                    0,
                    13.5f
                },
                ""
            },
        })]
        [TestCase("Mixed input types", new object[]
        {
            145,
            200.0f,
            new object[]
            {
                "MultiInput",
                0,
                748.5f,
                new[]
                {
                    0,
                    13.5f
                },
                "string"
            },
            new object[]
            {
                "CompressedInput",
                0,
                748.5f,
                new[]
                {
                    0,
                    13.5f
                },
                "string"
            },
        })]
        public void TestInputMessageBitCount(string testName, object[] data)
        {
            // Arrange
            const int packetSize = 8 * 1280;

            var originalInputs = new List<InputData>();
            var messageBuffer = new Queue<SerializedEntityMessage>();

            foreach (var input in data)
            {
                CreateInput(ref originalInputs, ref messageBuffer, input);
            }

            (uint actualBitCount, uint expectedBitCount) = SerializeMessageBuffer(messageBuffer, packetSize);

            Assert.That(actualBitCount, Is.EqualTo(expectedBitCount));
        }

        [TestCase("Zero char", 0)]
        [TestCase("One char", 1)]
        [TestCase("23 char", 23)]
        [TestCase("Many chars", 550)]
        public void TestInputStringMessages(string testName, int numChars)
        {
            var chars = new char[numChars];
            for (int i = 0; i < numChars; i++)
            {
                chars[i] = (char)(i % 256);
            }

            TestInputMessageBitCount(testName, new object[]
            {
                new string(chars)
            });
        }
    }
}
