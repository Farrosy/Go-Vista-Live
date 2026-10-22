// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using Coherence.Entities;
    using Coherence.ProtocolDef;

    public class MessageResolver
    {
        private IEntityRegistry entityRegistry;

        public MessageResolver(IEntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public bool IsResolvable<T>(T message) where T : IMessageData<T>
        {
            if (typeof(T) == typeof(CommandData) && message.MessageTarget == MessageTarget.StateAuthorityOnly &&
                !entityRegistry.HasAuthorityOverEntity(message.Entity, AuthorityType.State))
            {
                return false;
            }

            if (!entityRegistry.EntityExists(message.Entity))
            {
                return false;
            }

            foreach (var entityField in message.EntityFields)
            {
                var entity = entityField.GetEntity();

                if (!entity.IsValid)
                {
                    continue;
                }

                if (!entityRegistry.EntityExists(entity))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
