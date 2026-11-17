using System.Collections.Generic;
using UnityEngine;

/// Melepas + menghapus RenderTexture kecil hasil Shrink saat avatar dihancurkan, supaya tidak bocor.
public class ShrunkTextureOwner : MonoBehaviour {
    public readonly List<RenderTexture> Textures = new List<RenderTexture>();

    private void OnDestroy() {
        foreach (RenderTexture texture in Textures) {
            if (texture == null) continue;
            if (RenderTexture.active == texture) RenderTexture.active = null;
            texture.Release();
            Destroy(texture);
        }
        Textures.Clear();
    }
}
