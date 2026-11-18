using NUnit.Framework;

public class VenueMapRulesTests {
    // Lantai bawah KPOP Arena di Y −15, balkon di Y −11,9, batas label 2 m di atas lantai.
    private const float ArenaFloor = -15f;
    private const float Lift = 2f;

    private static readonly VenueArea[] Areas = {
        new VenueArea("Lantai Dansa", 0f, -8.5f, 6.9f, false),
        new VenueArea("Booth VIP Biru", 16.8f, -9.8f, 5.6f, false),
        new VenueArea("Balkon VIP", 16.2f, -1.8f, 5.2f, true),
    };

    // ------------------------------------------------------------ Balkon

    [Test]
    public void Player_on_the_main_floor_is_not_on_the_balcony() {
        Assert.IsFalse(VenueMapRules.IsUpperLevel(-15f, ArenaFloor, Lift));
    }

    [Test]
    public void Player_standing_in_a_booth_under_the_balcony_is_still_downstairs() {
        // Uji jalan: booth di bawah balkon berdiri di Y −14,2 sampai −14,5.
        Assert.IsFalse(VenueMapRules.IsUpperLevel(-14.17f, ArenaFloor, Lift));
    }

    [Test]
    public void Player_on_the_stair_landing_is_still_downstairs() {
        Assert.IsFalse(VenueMapRules.IsUpperLevel(-13.4f, ArenaFloor, Lift));
    }

    [Test]
    public void Player_on_the_balcony_is_upstairs() {
        Assert.IsTrue(VenueMapRules.IsUpperLevel(-11.9f, ArenaFloor, Lift));
    }

    [Test]
    public void Venue_without_an_upper_level_never_reports_upstairs() {
        Assert.IsFalse(VenueMapRules.IsUpperLevel(50f, 0f, 0f));
    }

    // ------------------------------------------------------------ Area

    [Test]
    public void Area_containing_the_player_is_found() {
        Assert.AreEqual(0, VenueMapRules.AreaIndexAt(Areas, 1f, -9f, upper: false));
    }

    [Test]
    public void Player_slightly_outside_an_area_still_counts_as_near_it() {
        // 1,3 × jari-jari dari tengah Lantai Dansa.
        Assert.AreEqual(0, VenueMapRules.AreaIndexAt(Areas, 0f, -8.5f + 6.9f * 1.3f, upper: false));
    }

    [Test]
    public void Player_far_from_every_area_is_in_no_area() {
        Assert.AreEqual(-1, VenueMapRules.AreaIndexAt(Areas, -20f, 20f, upper: false));
    }

    [Test]
    public void Downstairs_player_under_the_balcony_gets_the_booth_not_the_balcony() {
        Assert.AreEqual(1, VenueMapRules.AreaIndexAt(Areas, 16.5f, -5f, upper: false));
    }

    [Test]
    public void Upstairs_player_gets_the_nearest_upper_area_even_when_outside_its_radius() {
        // Di jembatan, di luar jari-jari Balkon VIP, tapi tetap di lantai atas.
        Assert.AreEqual(2, VenueMapRules.AreaIndexAt(Areas, 5f, -8.8f, upper: true));
    }

    [Test]
    public void Upstairs_player_in_a_venue_without_upper_areas_is_in_no_area() {
        var lowerOnly = new[] { Areas[0] };
        Assert.AreEqual(-1, VenueMapRules.AreaIndexAt(lowerOnly, 0f, -8.5f, upper: true));
    }

    [Test]
    public void Empty_or_missing_area_list_returns_no_area() {
        Assert.AreEqual(-1, VenueMapRules.AreaIndexAt(new VenueArea[0], 0f, 0f, upper: false));
        Assert.AreEqual(-1, VenueMapRules.AreaIndexAt(null, 0f, 0f, upper: false));
    }

    // ------------------------------------------------------------ Jangkauan radar

    [Test]
    public void Venue_radar_zooms_out_by_the_same_ratio_as_the_island_when_sprinting() {
        // Pulau: 60 m → 72 m saat sprint (×1,2). Venue 12 m → 14,4 m.
        Assert.AreEqual(14.4f, VenueMapRules.SprintRadius(12f, 60f, 72f), 0.001f);
    }

    [Test]
    public void Sprint_radius_falls_back_to_walk_radius_for_a_bad_island_setting() {
        Assert.AreEqual(12f, VenueMapRules.SprintRadius(12f, 0f, 72f), 0.001f);
    }
}
