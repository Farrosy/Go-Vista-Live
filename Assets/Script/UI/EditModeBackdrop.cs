using UnityEngine;

/// <summary>
/// Latar mode ATUR KONTROL (gelap tipis + grid) yang diletakkan DI BELAKANG tombol layar, supaya tombolnya
/// tetap terlihat jelas saat diatur. Objek editor sendiri harus jadi child terakhir canvas (menangkap semua
/// sentuhan), jadi latarnya dipisah: objek "Control Edit Backdrop" di child pertama canvas.
///
/// Pasang di objek "Control Layout Editor" (dilakukan Go Vista > Desain Baru > Atur Kontrol).
/// Latar ikut muncul/hilang bersama editor.
/// </summary>
public sealed class EditModeBackdrop : MonoBehaviour {
    [SerializeField] private GameObject backdrop;

    public void Init(GameObject backdropObject) => backdrop = backdropObject;

    private void OnEnable() {
        if (backdrop == null)
            return;
        backdrop.transform.SetAsFirstSibling();
        backdrop.SetActive(true);
    }

    private void OnDisable() {
        if (backdrop != null)
            backdrop.SetActive(false);
    }
}
