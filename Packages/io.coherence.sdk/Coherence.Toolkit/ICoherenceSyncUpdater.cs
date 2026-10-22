namespace Coherence.Toolkit
{
    using Entities;
    using System.Collections.Generic;
    using SimulationFrame;
    using Logger = Log.Logger;
    using Coherence.ProtocolDef;

    public interface ICoherenceSyncUpdater
    {
        Logger logger { get; set; }
        bool TaggedForNetworkedDestruction { get; set; }
        bool ChangedConnection { get; }
        Entity NewConnection { get; }

        void InterpolateBindings();

        void InvokeCallbacks();

        void SendState(double time, AbsoluteSimulationFrame simulationFrame);
        void SendInput();
        void ProcessRemoteEntity();

        void SampleBindings(double time);
        void SampleAllBindings(double time);

        void GetComponentUpdates(List<ComponentData> updates, double currentTime, AbsoluteSimulationFrame simulationFrame, bool forceSerialize = false);

        void PerformInterpolationOnAllBindings();

        void ClearAllSampleTimes();

        void OnConnectedEntityChanged();

        /// <summary>
        ///     Sends changes on all bindings manually instead of waiting for the update.
        ///     External use only when the CoherenceSync behaviour is disabled.
        ///     The sending of the packet containing these changes is still dependant on the packet send rate.
        ///     Do no call before the entity has been registered with the client, which will happen after the
        ///     First update after the client is connected and the CoherenceSync behaviour is enabled.
        ///     If called before the entity is registered with the client updates will be lost.
        /// </summary>
        void ManuallySendAllChanges(bool sampleValuesBeforeSending);
        void SendTag();
        bool TryFlushPosition(bool sampleValueBeforeSending);

        void ApplyComponentDestroys(ComponentDestroyMap destroyedComponents);

        void ApplyComponentUpdates(ComponentUpdates componentUpdates);

        void ApplyConnectedEntityChanges();
    }
}
