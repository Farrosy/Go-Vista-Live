using Normal.Realtime;
using UnityEngine;

/// Lives on a scene RealtimeView in MainMultiplayerScene, because WorldScene (and TrafficNetwork with
/// it) is unloaded whenever a player travels to a venue, while the traffic state must outlive that.
[RequireComponent(typeof(RealtimeView))]
public sealed class TrafficSync : RealtimeComponent<TrafficSyncModel>
{
    public static TrafficSync Instance { get; private set; }

    public bool IsReady => model != null && realtime != null && realtime.connected;
    public int LocalClientId => realtime != null ? realtime.clientID : -1;
    public int Seed => model != null ? model.seed : 0;
    public int RouteHash => model != null ? model.routeHash : 0;
    public byte[] State => model != null ? model.state : null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PublishSetup(int seed, int routeHash)
    {
        if (model == null) return;
        model.seed = seed;
        model.routeHash = routeHash;
    }

    public void PublishState(byte[] state)
    {
        if (model != null) model.state = state;
    }
}
