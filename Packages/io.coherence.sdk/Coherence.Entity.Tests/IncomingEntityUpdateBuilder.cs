// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities.Tests
{
    using System;
    using Coherence.Common.Tests;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;

    public class IncomingEntityUpdateBuilder
    {
        public static Entity DefaultEntity => new Entity((Entities.Index)1, 0, false);

        private IncomingEntityUpdate update = IncomingEntityUpdate.New();
        private readonly DeltaComponentsBuilder componentsBuilder;

        public IncomingEntityUpdateBuilder(ISchemaDataFactory schemaDataFactory, EntityOperation operation, Entity? entity = null)
        {
            update.Meta.HasMeta = true;
            update.Meta.Operation = operation;
            update.Meta.EntityId = entity.GetValueOrDefault(DefaultEntity);
            componentsBuilder = new DeltaComponentsBuilder(schemaDataFactory);
        }

        public IncomingEntityUpdate Build()
        {
            update.Components = componentsBuilder.Build();
            return update;
        }

        public IncomingEntityUpdateBuilder Components(Action<DeltaComponentsBuilder> build)
        {
            build(componentsBuilder);
            return this;
        }
    }
}
