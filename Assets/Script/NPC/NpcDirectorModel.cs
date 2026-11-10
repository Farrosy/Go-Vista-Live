using Normal.Realtime;

/// Synced state for the NPC crowd director (one scene object in MainMultiplayerScene). Source-generated
/// by Normcore (partial + [RealtimeModel]), no "Compile Model" step.
[RealtimeModel]
public partial class NpcDirectorModel
{
    // Reliable + change callback: exactly ONE client ever instantiates the shared crowd. The first
    // elected authority sets this true after spawning the roster; it blocks a second spawn if two
    // clients briefly both believe they're authority during a migration race.
    [RealtimeProperty(1, true, true)] private bool _rosterSpawned;
}
