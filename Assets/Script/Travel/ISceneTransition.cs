using System.Collections;

/// Seam transisi scene untuk VenueTravelManager. TravelTo HANYA memanggil Cover() sebelum
/// load/unload dan Reveal() setelah avatar ditempatkan — tidak pernah menyentuh CanvasGroup/fade
/// timing langsung. Ini membuat loading screen (LoadingScreenTransition) bisa menggantikan fade
/// nanti tanpa mengubah TravelTo sama sekali.
///
/// Kontrak:
/// - Cover() menutup layar dan HARUS menahan tutupannya sampai Reveal() dipanggil (bukan tween
///   durasi tetap yang langsung membuka lagi). Avatar/scene berganti selagi tertutup.
/// - Reveal() dipanggil hanya setelah scene target siap & avatar sudah dipindah, jadi implementasi
///   loading screen bisa memakai pemanggilan Reveal() sebagai sinyal "ready".
public interface ISceneTransition
{
    IEnumerator Cover();
    IEnumerator Reveal();

    /// Buka layar seketika tanpa animasi. Dipakai saat travel dibatalkan di tengah jalan
    /// (mis. koneksi room putus), supaya layar tidak tertinggal hitam.
    void ResetImmediate();
}
