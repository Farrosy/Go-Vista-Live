using UnityEngine;

/// <summary>
/// Hitung ulang pemetaan light probe saat scene yang punya probe di-load atau di-unload secara additive.
/// Unity tidak melakukannya sendiri, jadi tanpa ini karakter di venue tidak memakai probe hasil bake.
/// </summary>
public static class LightProbeTetrahedralizer {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register() {
        LightProbes.needsRetetrahedralization -= Refresh;
        LightProbes.needsRetetrahedralization += Refresh;
    }

    private static void Refresh() => LightProbes.TetrahedralizeAsync();
}
