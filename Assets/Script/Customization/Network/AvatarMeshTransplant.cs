using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Memindahkan hasil bake BoZo (mesh gabungan + renderer outfit yang tidak digabung) ke rig avatar pemain
/// yang SUDAH ADA di prefab (objek BodyRig dengan Animator). Rig tidak diganti, jadi Animator,
/// PlayerController, IK pintu mobil, knockdown, dll. tetap memakai objek yang sama.
///
/// Tulang dipetakan lewat nama (rig BoZo sama untuk template dan karakter dasar). Tulang tambahan dari outfit
/// (rambut, rok) yang tidak ada di rig pemain disalin ke bawah induk yang sesuai.
/// </summary>
public static class AvatarMeshTransplant {
    /// <summary>
    /// Nama objek penampung renderer custom di bawah rig pemain. Outfit yang menempel di tulang (topi, dll.)
    /// dipindah ke tulang rig dan ditandai <see cref="CustomAppearancePart"/>.
    /// </summary>
    public const string HolderName = "Custom Appearance";

    /// <summary>
    /// Pindahkan hasil bake ke rig lalu ambil alih memorinya (mesh/material gabungan + tekstur kecil) ke
    /// BakedAppearanceOwner di penampung. Sesudah ini avatar sementara <paramref name="baked"/> boleh dihapus.
    /// </summary>
    /// <returns>Penampung renderer custom, atau null kalau gagal (memori tetap milik avatar sementara).</returns>
    public static GameObject ApplyOwned(OutfitSystem baked, Transform targetRig) {
        GameObject holder = Apply(baked, targetRig);
        if (holder) TakeOwnership(baked, holder.AddComponent<BakedAppearanceOwner>());
        return holder;
    }

    private static void TakeOwnership(OutfitSystem baked, BakedAppearanceOwner owner) {
        baked.isStatic = true; // OutfitSystem.OnDestroy tidak lagi menghapus mesh/material gabungan
        owner.Own(baked.combinedMesh);
        SkinnedMeshRenderer body = baked.GetCharacterBody();
        if (body) {
            owner.Own(body.sharedMesh);
            foreach (Material material in body.sharedMaterials) owner.Own(material);
        }
        var shrunk = baked.GetComponent<ShrunkTextureOwner>();
        if (!shrunk) return;
        foreach (RenderTexture texture in shrunk.Textures) owner.OwnTexture(texture);
        shrunk.Textures.Clear();
    }

    private const float PoseTolerance = 0.001f;     // meter / skala
    private const float RotationTolerance = 0.5f;   // derajat

    /// Nama tulang kerangka dasar BoZo (yang digerakkan Animator). Tulang lain berasal dari outfit (rambut, kepala
    /// anime, rok) dan posisinya berbeda per outfit.
    private static HashSet<string> _skeleton;

    /// Satu kali Apply: tulang rig pemain per nama + tulang outfit yang sudah di-resolve (sumber → rig).
    private sealed class BoneMap {
        public Dictionary<string, Transform> Target;
        public HashSet<string> Skeleton;
        public readonly Dictionary<Transform, Transform> Resolved = new Dictionary<Transform, Transform>();
    }

    /// <returns>Penampung renderer custom (anak <paramref name="targetRig"/>), atau null kalau tidak ada yang dipindah.</returns>
    public static GameObject Apply(OutfitSystem baked, Transform targetRig) {
        if (!baked || !targetRig) return null;
        var bones = new BoneMap { Target = MapByName(targetRig), Skeleton = Skeleton() };
        var holder = new GameObject(HolderName);
        holder.transform.SetParent(targetRig, false);

        int moved = 0;
        foreach (Renderer renderer in baked.GetComponentsInChildren<Renderer>(false)) {
            if (!renderer.enabled) continue;
            if (renderer is SkinnedMeshRenderer skinned) {
                if (skinned.sharedMesh == null) continue;
                CopySkinned(skinned, holder.transform, bones);
                moved++;
            } else if (renderer is MeshRenderer) {
                if (MoveAttached(renderer.transform, bones)) moved++;
            }
        }
        if (moved == 0) {
            Object.Destroy(holder);
            return null;
        }
        return holder;
    }

    private static void CopySkinned(SkinnedMeshRenderer source, Transform holder, BoneMap targetBones) {
        var go = new GameObject(source.name);
        go.transform.SetParent(holder, false);
        go.layer = holder.gameObject.layer;
        SkinnedMeshRenderer copy = go.AddComponent<SkinnedMeshRenderer>();
        copy.sharedMesh = source.sharedMesh;
        copy.sharedMaterials = source.sharedMaterials;
        Transform[] bones = source.bones;
        var mapped = new Transform[bones.Length];
        for (int i = 0; i < bones.Length; i++) mapped[i] = Resolve(bones[i], targetBones);
        copy.bones = mapped;
        copy.rootBone = Resolve(source.rootBone, targetBones);
        copy.localBounds = source.localBounds;
        copy.updateWhenOffscreen = source.updateWhenOffscreen;
        copy.shadowCastingMode = source.shadowCastingMode;
        copy.receiveShadows = source.receiveShadows;
        copy.quality = source.quality;
        // Outfit yang tidak digabung (renderer sendiri) menyimpan warnanya di MaterialPropertyBlock.
        if (source.HasPropertyBlock()) {
            var block = new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            copy.SetPropertyBlock(block);
        }
        // Slider wajah = blendshape pada mesh gabungan.
        for (int i = 0; i < source.sharedMesh.blendShapeCount; i++) copy.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
    }

    /// Outfit yang menempel di satu tulang (mis. topi dengan MeshRenderer): pindahkan objeknya ke tulang yang sama.
    private static bool MoveAttached(Transform attached, BoneMap targetBones) {
        Transform parent = attached.parent;
        Transform target = parent ? Resolve(parent, targetBones) : null;
        if (!target) return false;
        attached.SetParent(target, false);
        attached.gameObject.AddComponent<CustomAppearancePart>();
        return true;
    }

    /// Tulang kerangka dasar: tulang rig pemain dengan nama yang sama (digerakkan Animator).
    /// Tulang outfit: tulang rig dengan nama sama HANYA kalau induk dan pose lokalnya sama persis. Nama tulang
    /// outfit tidak unik antar-outfit (mis. Hair_Front ada di semua rambut depan dengan posisi berbeda), dan rig
    /// template masih membawa rambut bawaannya; tanpa cek ini rambut baru menempel ke tulang rambut template
    /// dan bergeser (2026-11-25: WolfCut naik ±10 cm di rig anime_male). Selain itu disalin (bersama induknya
    /// yang hilang); salinan teratas ditandai CustomAppearancePart supaya ikut dihapus saat wujud diganti.
    private static Transform Resolve(Transform bone, BoneMap map) {
        if (!bone) return null;
        if (map.Resolved.TryGetValue(bone, out Transform done)) return done;
        bool skeleton = map.Skeleton == null || map.Skeleton.Contains(bone.name);
        map.Target.TryGetValue(bone.name, out Transform existing);
        if (existing && skeleton) return map.Resolved[bone] = existing;

        Transform parent = bone.parent ? Resolve(bone.parent, map) : null;
        if (!parent) return null;
        if (existing && existing.parent == parent && SamePose(existing, bone)) return map.Resolved[bone] = existing;

        var clone = new GameObject(bone.name).transform;
        clone.SetParent(parent, false);
        clone.localPosition = bone.localPosition;
        clone.localRotation = bone.localRotation;
        clone.localScale = bone.localScale;
        if (!parent.GetComponentInParent<CustomAppearancePart>(true)) clone.gameObject.AddComponent<CustomAppearancePart>();
        return map.Resolved[bone] = clone;
    }

    private static bool SamePose(Transform a, Transform b) =>
        (a.localPosition - b.localPosition).sqrMagnitude <= PoseTolerance * PoseTolerance &&
        (a.localScale - b.localScale).sqrMagnitude <= PoseTolerance * PoseTolerance &&
        Quaternion.Angle(a.localRotation, b.localRotation) <= RotationTolerance;

    /// null = kerangka tidak diketahui (CustomizerSettings belum ada): semua tulang dicocokkan lewat nama saja.
    private static HashSet<string> Skeleton() {
        if (_skeleton != null) return _skeleton;
        OutfitSystem characterBase = CustomizerSettings.Instance ? CustomizerSettings.Instance.characterBase : null;
        if (!characterBase) return null;
        _skeleton = new HashSet<string>();
        foreach (Transform t in characterBase.GetComponentsInChildren<Transform>(true)) _skeleton.Add(t.name);
        return _skeleton;
    }

    /// Tulang salinan dari wujud sebelumnya (CustomAppearancePart, dihapus akhir frame ini) tidak dipakai ulang.
    private static Dictionary<string, Transform> MapByName(Transform root) {
        var map = new Dictionary<string, Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            if (map.ContainsKey(t.name) || t.GetComponentInParent<CustomAppearancePart>(true)) continue;
            map.Add(t.name, t);
        }
        return map;
    }
}
