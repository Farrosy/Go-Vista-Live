using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Bake kurva KnockdownGroundClearance untuk semua karakter pemain di CharacterDatabase.
/// Per karakter, tiap state rebahan (tag "Knockdown" + state "Death") disampel per frame. Titik terendah
/// mesh dibandingkan dengan lantai (titik terendah saat Idle), dan kekurangannya jadi kurva angkat pinggul.
/// Jalankan lagi kalau model karakter atau klip rebahan diganti.
/// </summary>
public static class KnockdownGroundClearanceBaker {
    private const int SamplesPerClip = 48;
    private const float Margin = 0.01f;          // sedikit di atas lantai supaya tidak z-fighting
    private const string DeathState = "Death";
    private const string IdleClip = "Idle";

    [MenuItem("Go Vista/Character/Bake Knockdown Ground Clearance")]
    public static void BakeAll() {
        CharacterDatabase db = CharacterDatabase.Instance;
        if (!db) { Debug.LogError("[KnockdownBaker] CharacterDatabase tidak ditemukan."); return; }
        for (int i = 0; i < db.Count; i++) {
            GameObject prefab = db[i].avatarPrefab;
            if (prefab) Debug.Log("[KnockdownBaker] " + BakePrefab(AssetDatabase.GetAssetPath(prefab)));
        }
    }

    public static string BakePrefab(string prefabPath) {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            var controller = animator ? animator.runtimeAnimatorController as AnimatorController : null;
            if (!controller) return prefabPath + ": tidak ada AnimatorController, dilewati.";

            var clearance = animator.GetComponent<KnockdownGroundClearance>();
            if (!clearance) clearance = animator.gameObject.AddComponent<KnockdownGroundClearance>();
            clearance.States.Clear();

            // Salinan untuk disampel, di preview scene supaya scene yang sedang terbuka tidak jadi "dirty".
            var previewScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var sampler = Object.Instantiate(animator.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sampler, previewScene);
            try {
                var sampleAnimator = sampler.GetComponent<Animator>();
                var mesh = sampler.GetComponentInChildren<SkinnedMeshRenderer>();
                float floor = LowestPoint(sampleAnimator, mesh, FindClip(controller, IdleClip), 0f);
                float scale = sampler.transform.lossyScale.y;
                var report = new List<string>();
                foreach (AnimatorState state in KnockdownStates(controller)) {
                    var clip = state.motion as AnimationClip;
                    if (!clip) continue;
                    AnimationCurve curve = BakeCurve(sampleAnimator, mesh, clip, floor, scale, out float maxLift);
                    clearance.States.Add(new KnockdownGroundClearance.StateLift { stateName = state.name, lift = curve });
                    report.Add($"{state.name} maks {maxLift:F2} m");
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return $"{System.IO.Path.GetFileNameWithoutExtension(prefabPath)}: " + string.Join(", ", report);
            } finally {
                Object.DestroyImmediate(sampler);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
            }
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static IEnumerable<AnimatorState> KnockdownStates(AnimatorController controller) {
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states) {
            AnimatorState s = child.state;
            if (s.tag == PlayerController.KnockdownTag || s.name == DeathState) yield return s;
        }
    }

    private static AnimationCurve BakeCurve(Animator animator, SkinnedMeshRenderer mesh, AnimationClip clip,
                                            float floor, float scale, out float maxLift) {
        var raw = new float[SamplesPerClip + 1];
        for (int i = 0; i <= SamplesPerClip; i++) {
            float lowest = LowestPoint(animator, mesh, clip, clip.length * i / SamplesPerClip);
            raw[i] = Mathf.Max(0f, floor - lowest);
        }
        var curve = new AnimationCurve();
        maxLift = 0f;
        for (int i = 0; i <= SamplesPerClip; i++) {
            // Ambil maksimum dengan tetangga supaya di antara dua sampel badan tidak sempat tembus.
            float lift = Mathf.Max(raw[i], raw[Mathf.Max(0, i - 1)], raw[Mathf.Min(SamplesPerClip, i + 1)]);
            if (lift > 0f) lift += Margin;
            maxLift = Mathf.Max(maxLift, lift);
            curve.AddKey(new Keyframe((float)i / SamplesPerClip, lift / scale));
        }
        for (int i = 0; i < curve.length; i++) {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    private static float LowestPoint(Animator animator, SkinnedMeshRenderer mesh, AnimationClip clip, float time) {
        AnimationMode.StartAnimationMode();
        try {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
            AnimationMode.EndSampling();
            var baked = new Mesh();
            mesh.BakeMesh(baked, false);
            float baseY = mesh.transform.position.y, lowest = float.MaxValue;
            foreach (Vector3 v in baked.vertices) lowest = Mathf.Min(lowest, baseY + v.y);
            Object.DestroyImmediate(baked);
            return lowest;
        } finally {
            AnimationMode.StopAnimationMode();
        }
    }

    private static AnimationClip FindClip(AnimatorController controller, string name) {
        foreach (AnimationClip clip in controller.animationClips)
            if (clip.name == name) return clip;
        throw new System.InvalidOperationException($"Klip '{name}' tidak ada di {controller.name}.");
    }
}
