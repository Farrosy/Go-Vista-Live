// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using Coherence.Entities;
    using Coherence.Log;

    /// <summary>
    /// Interface combining common properties and methods for Commands and Inputs.
    /// Be careful when using to not cause boxing allocations.
    /// To avoid boxing, use generic methods with constraints to IMessageData.
    /// </summary>
    public interface IMessageData<TSelf> : IFieldDataCollection, IDisposable where TSelf : IFieldDataCollection
    {
        Entity Entity { get; set; }
        MessageTarget MessageTarget { get; }
        uint TypeID { get; }
        uint SenderParticipant { get; }
        FieldIterator<TSelf> EntityFields { get; }

        void NullEntityRefs(Entity entity);
        IEntityMapper.Error MapToAbsolute(IEntityMapper mapper, Logger logger);
        IEntityMapper.Error MapToRelative(IEntityMapper mapper, Logger logger);

        TSelf ReleaseOwnership();
    }
}
