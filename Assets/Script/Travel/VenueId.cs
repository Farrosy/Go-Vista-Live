using System;

/// <summary>
/// Identifies a travel venue (one of the scenes the local avatar can inhabit).
/// The numeric values are serialized on components (e.g. PlayerSpawnPoint) and
/// synced over Normcore later, so DO NOT reorder or renumber existing entries.
/// </summary>
public enum VenueId
{
    World = 0,
    DanceClub = 1,
    RooftopBar = 2,
    Barber = 3,
}

/// <summary>
/// Static map between <see cref="VenueId"/> and the Unity scene that backs it.
/// Scenes are referenced by name (not path) because they are loaded additively
/// via SceneManager — each must be present and enabled in Build Settings.
/// </summary>
public static class VenueMap
{
    public const string WorldSceneName = "WorldScene";
    public const string DanceClubSceneName = "Demo_DanceClub_01";
    public const string RooftopBarSceneName = "Demo_RooftopBar_01";
    public const string BarberSceneName = "Interior_Barber";

    /// <summary>
    /// True for building interiors (fixed top-down camera via InteriorCameraZone, avatar keeps world scale).
    /// False for the World island and the big KPOP venues.
    /// </summary>
    public static bool IsInterior(VenueId venue) => venue == VenueId.Barber;

    /// <summary>Scene name to load additively for the given venue.</summary>
    public static string SceneName(VenueId venue)
    {
        switch (venue)
        {
            case VenueId.World:      return WorldSceneName;
            case VenueId.DanceClub:  return DanceClubSceneName;
            case VenueId.RooftopBar: return RooftopBarSceneName;
            case VenueId.Barber:     return BarberSceneName;
            default:
                throw new ArgumentOutOfRangeException(nameof(venue), venue, "Unknown VenueId.");
        }
    }

    /// <summary>
    /// Resolve a <see cref="VenueId"/> from a scene name or full asset path.
    /// Returns null when the scene is not one of the known venues.
    /// </summary>
    public static VenueId? FromScene(string sceneNameOrPath)
    {
        if (string.IsNullOrEmpty(sceneNameOrPath))
            return null;

        // Accept "WorldScene", "Assets/.../WorldScene.unity", backslashes, etc.
        string name = sceneNameOrPath;
        int slash = name.LastIndexOfAny(new[] { '/', '\\' });
        if (slash >= 0)
            name = name.Substring(slash + 1);
        if (name.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - ".unity".Length);

        if (string.Equals(name, WorldSceneName, StringComparison.Ordinal))      return VenueId.World;
        if (string.Equals(name, DanceClubSceneName, StringComparison.Ordinal))  return VenueId.DanceClub;
        if (string.Equals(name, RooftopBarSceneName, StringComparison.Ordinal)) return VenueId.RooftopBar;
        if (string.Equals(name, BarberSceneName, StringComparison.Ordinal))     return VenueId.Barber;
        return null;
    }
}
