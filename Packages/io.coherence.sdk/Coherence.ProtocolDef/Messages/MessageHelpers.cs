// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Entities;
    using Coherence.Log;

    internal static class MessageHelpers
    {
        public static IEntityMapper.Error MapToAbsolute<T>(ref T message, IEntityMapper mapper, Logger logger) where T : IFieldDataCollection, IMessageData<T>
        {
            var err = mapper.MapToAbsoluteEntity(message.Entity, false, out var absoluteEntity);
            if (err != IEntityMapper.Error.None)
            {
                return err;
            }

            message.Entity = absoluteEntity;

            foreach (var entityField in message.EntityFields)
            {
                err = mapper.MapToAbsoluteEntity(entityField.GetEntity(), false, out absoluteEntity);
                if (err != IEntityMapper.Error.None)
                {
                    return err;
                }

                entityField.SetEntity(absoluteEntity);
            }

            return IEntityMapper.Error.None;
        }

        public static IEntityMapper.Error MapToRelative<T>(ref T message, IEntityMapper mapper, Logger logger) where T : IMessageData<T>
        {
            var err = mapper.MapToRelativeEntity(message.Entity, false, out var relativeEntity);
            if (err != IEntityMapper.Error.None)
            {
                return err;
            }

            message.Entity = relativeEntity;

            foreach (var entityField in message.EntityFields)
            {
                err = mapper.MapToRelativeEntity(entityField.GetEntity(), false, out relativeEntity);
                if (err != IEntityMapper.Error.None)
                {
                    return err;
                }

                entityField.SetEntity(relativeEntity);
            }

            return IEntityMapper.Error.None;
        }

        public static void NullEntityRefs<T>(T message, Entity entity) where T : IMessageData<T>
        {
            foreach (var entityField in message.EntityFields)
            {
                if (entityField.GetEntity() == entity)
                {
                    entityField.SetEntity(Entity.InvalidRelative);
                }
            }
        }
    }
}
