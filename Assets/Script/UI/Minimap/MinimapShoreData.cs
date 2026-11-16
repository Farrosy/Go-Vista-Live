using UnityEngine;

/// Data garis pantai untuk ombak animasi di peta penuh (GoVistaFullMap).
/// Dibuat oleh menu "Go Vista/Minimap/Bake garis pantai" (MinimapShoreBaker) dari hasil scan Minimap Scanner,
/// jadi tidak perlu diubah manual. Bake ulang setiap kali peta di-scan ulang (menu scan sudah melakukannya).
public sealed class MinimapShoreData : ScriptableObject {
    [Tooltip("Jarak ke darat terdekat per piksel (R: 0 = darat, 1 = maxDistance meter atau lebih).")]
    public Texture2D distanceField;
    [Tooltip("Hasil scan yang dipakai saat bake. Kalau beda dengan scan di scene, data ini sudah usang.")]
    public Texture2D sourceScan;
    [Tooltip("Warna air di hasil scan; dipakai juga untuk area di luar scan.")]
    public Color waterColor = new Color32(34, 128, 127, 255);
    [Tooltip("Posisi dunia (x, z) sudut kiri bawah distanceField.")]
    public Vector2 worldMin;
    [Tooltip("Lebar = tinggi distanceField dalam meter (area scan + margin laut di keempat sisi).")]
    public float worldSize = 1f;
    [Tooltip("Jarak (meter) yang diwakili nilai 1 di distanceField.")]
    public float maxDistance = 28f;
}
