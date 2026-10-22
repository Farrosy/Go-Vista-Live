// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using Coherence.ProtocolDef;

    public struct MultiComponent
    {
        public const uint ComponentID = 22;

        public ComponentData Data { get; private set; }

        public int IntValue
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        public bool IsIntValueSet
        {
            get => Data.IsFieldSet(0);
            set => Data = Data.SetFieldSet(0, value);
        }

        public float FloatValue0
        {
            get => Data.GetField(1).GetFloat();
            set => Data.GetField(1).SetFloat(value);
        }

        public bool IsFloatValue0Set
        {
            get => Data.IsFieldSet(1);
            set => Data = Data.SetFieldSet(1, value);
        }

        public float FloatValue1
        {
            get => Data.GetField(2).GetFloat();
            set => Data.GetField(2).SetFloat(value);
        }

        public bool IsFloatValue1Set
        {
            get => Data.IsFieldSet(2);
            set => Data = Data.SetFieldSet(2, value);
        }

        private MultiComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static MultiComponent From(ComponentData componentData) => new(componentData);

        public static ComponentData Create(ISchemaDataFactory schemaDataFactory, int intValue, float floatValue0, float floatValue1)
        {
            var componentData = schemaDataFactory.CreateComponent(ComponentID);
            componentData.FieldsMask = componentData.InitialFieldsMask();

            var component = From(componentData);
            component.IntValue = intValue;
            component.FloatValue0 = floatValue0;
            component.FloatValue1 = floatValue1;

            return componentData;
        }
    }
}
