using System;
using Normal.Realtime;
using UnityEngine;

/// Pasang di root prefab avatar (sebelah PlayerController + RealtimeView + RealtimeTransform).
/// HARUS terdaftar di RealtimeView._components seperti PlayerAnimSync supaya ikut tersinkron.
///
/// Menyinkronkan "venue" avatar lewat Normcore. Dalam satu room, pemain hanya melihat/menabrak
/// avatar lain yang venue-nya sama (lihat VenueVisibilityFilter). Avatar lokal adalah sumber
/// kebenaran venue pemain ini; nilainya ditulis saat travel selesai (VenueTravelManager, Phase 3).
[RequireComponent(typeof(PlayerController))]
public class PlayerVenueSync : RealtimeComponent<PlayerVenueModel>
{
    /// Dipicu saat venue avatar INI berubah (nilai baru disertakan). Dipakai VenueVisibilityFilter.
    public event Action<PlayerVenueSync, VenueId> VenueChanged;

    /// Venue avatar ini sekarang. World (default) kalau model belum ada (mis. mode offline/DevScene).
    public VenueId Venue => model != null ? (VenueId)model.venue : VenueId.World;

    /// True kalau avatar ini dimiliki client lokal. False selama model Normcore belum ada
    /// (mengakses ownership sebelum model siap akan melempar exception).
    public bool IsLocalAvatar => model != null && isOwnedLocallyInHierarchy;

    protected override void OnRealtimeModelReplaced(PlayerVenueModel previousModel, PlayerVenueModel currentModel)
    {
        if (previousModel != null)
            previousModel.venueDidChange -= OnVenueDidChange;

        if (currentModel != null)
        {
            currentModel.venueDidChange += OnVenueDidChange;
            // Evaluasi awal dengan nilai yang sudah ada di model (penting untuk avatar yang sudah ada
            // di room saat kita bergabung).
            Raise((VenueId)currentModel.venue);
        }
    }

    private void OnVenueDidChange(PlayerVenueModel m, int value) => Raise((VenueId)value);

    private void Raise(VenueId venue)
    {
        VenueChanged?.Invoke(this, venue);
        // Avatar lokal mencerminkan venue-nya ke state global supaya semua filter ikut re-evaluasi.
        if (IsLocalAvatar)
            VenueTravelManager.SetLocalVenue(venue);
    }

    /// Hanya avatar lokal: set venue avatar ini. Dipanggil VenueTravelManager saat travel selesai (Phase 3).
    public void SetVenue(VenueId venue)
    {
        if (model == null || !isOwnedLocallyInHierarchy)
            return;
        if (model.venue != (int)venue)
            model.venue = (int)venue;
    }
}
