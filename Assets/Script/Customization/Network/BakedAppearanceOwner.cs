using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pemilik mesh, material, dan tekstur hasil bake yang dipindah ke avatar pemain (AvatarMeshTransplant).
/// Avatar sementara BoZo dihancurkan setelah bake, jadi objek runtime ini harus dilepas di sini saat penampung
/// "Custom Appearance" hilang (pemain keluar room, avatar dihancurkan) supaya memori tidak bocor.
/// </summary>
public sealed class BakedAppearanceOwner : MonoBehaviour {
    private readonly List<Object> _owned = new List<Object>();
    private readonly List<RenderTexture> _textures = new List<RenderTexture>();

    public void Own(Object asset) {
        if (asset && !_owned.Contains(asset)) _owned.Add(asset);
    }

    public void OwnTexture(RenderTexture texture) {
        if (texture && !_textures.Contains(texture)) _textures.Add(texture);
    }

    void OnDestroy() {
        foreach (RenderTexture texture in _textures) {
            if (!texture) continue;
            if (RenderTexture.active == texture) RenderTexture.active = null; // BoZo bisa meninggalkannya aktif
            texture.Release();
            Destroy(texture);
        }
        foreach (Object asset in _owned) {
            if (asset) Destroy(asset);
        }
        _textures.Clear();
        _owned.Clear();
    }
}
