using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bekas ban di jalan, satu mesh untuk semua mobil di satu scene (dibuat otomatis saat bekas pertama).
/// Pengganti Skidmarks AllStar: buffer melingkar yang dialokasikan sekali (Skidmarks AllStar membuat array mesh
/// baru tiap frame), dan shader URP (shader AllStar fixed-function, tidak tampil di URP).
/// Material: Resources/Vehicles/Skidmark (shader GoVista/Skidmark, alpha dari warna vertex).
/// Hanya visual per client: tidak disinkronkan, tiap client menggambar dari gerak mobil yang ia lihat.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class CarSkidmarks : MonoBehaviour {
    private const string MaterialPath = "Vehicles/Skidmark";
    private const int MaxMarks = 1024;
    private const float MarkWidth = 0.25f;
    private const float GroundOffset = 0.02f;

    private static CarSkidmarks _instance;

    private struct Mark {
        public Vector3 position, normal, left, right;
        public float intensity;
    }

    private readonly Mark[] marks = new Mark[MaxMarks];
    private readonly Vector3[] vertices = new Vector3[MaxMarks * 4];
    private readonly Vector3[] normals = new Vector3[MaxMarks * 4];
    private readonly Color[] colors = new Color[MaxMarks * 4];
    private readonly Vector2[] uvs = new Vector2[MaxMarks * 4];
    private Mesh mesh;
    private int count;   // total bekas yang pernah dibuat; slot = count % MaxMarks
    private bool dirty;

    /// <summary>
    /// Tambah satu titik bekas ban. last = index dari pemanggilan sebelumnya untuk roda yang sama (-1 = awal garis).
    /// Kembalian = index titik ini.
    /// </summary>
    public static int Add(Vector3 position, Vector3 normal, float intensity, int last, Scene scene) {
        if (!_instance && !Create(scene)) return -1;
        return _instance.AddMark(position, normal, intensity, last);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private static bool Create(Scene scene) {
        var material = Resources.Load<Material>(MaterialPath);
        if (!material) return false;
        var go = new GameObject("CarSkidmarks");
        if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(go, scene);
        _instance = go.AddComponent<CarSkidmarks>();   // MeshFilter + MeshRenderer ikut dibuat (RequireComponent)
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return true;
    }

    void Awake() {
        mesh = new Mesh { name = "CarSkidmarks" };
        mesh.MarkDynamic();
        var triangles = new int[MaxMarks * 6];
        for (int i = 0; i < MaxMarks; i++) {
            int v = i * 4, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 2; triangles[t + 4] = v + 3; triangles[t + 5] = v + 1;
            uvs[v] = new Vector2(0f, 0f); uvs[v + 1] = new Vector2(1f, 0f);
            uvs[v + 2] = new Vector2(0f, 1f); uvs[v + 3] = new Vector2(1f, 1f);
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // Bekas ban bisa di mana saja di map: jangan sampai seluruh mesh di-cull karena bounds kecil.
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    void OnDestroy() {
        if (_instance == this) _instance = null;
        if (mesh) Destroy(mesh);
    }

    private int AddMark(Vector3 position, Vector3 normal, float intensity, int last) {
        int index = count++;
        int slot = index % MaxMarks;
        ref Mark curr = ref marks[slot];
        curr.position = position + normal * GroundOffset;
        curr.normal = normal;
        curr.intensity = Mathf.Clamp01(intensity);

        int v = slot * 4;
        bool linked = last >= 0 && index - last < MaxMarks;   // titik sebelumnya belum tertimpa
        if (linked) {
            ref Mark prev = ref marks[last % MaxMarks];
            Vector3 side = Vector3.Cross(curr.position - prev.position, normal).normalized * (MarkWidth * 0.5f);
            curr.left = curr.position + side;
            curr.right = curr.position - side;
            if (prev.left == prev.right) {   // titik awal garis: arahnya baru diketahui sekarang
                prev.left = prev.position + side;
                prev.right = prev.position - side;
            }
            vertices[v] = prev.left; vertices[v + 1] = prev.right;
            vertices[v + 2] = curr.left; vertices[v + 3] = curr.right;
            normals[v] = normals[v + 1] = prev.normal;
            normals[v + 2] = normals[v + 3] = curr.normal;
            colors[v] = colors[v + 1] = new Color(0f, 0f, 0f, prev.intensity);
            colors[v + 2] = colors[v + 3] = new Color(0f, 0f, 0f, curr.intensity);
        } else {
            curr.left = curr.right = curr.position;
            for (int k = 0; k < 4; k++) { vertices[v + k] = curr.position; colors[v + k] = Color.clear; }
        }
        dirty = true;
        return index;
    }

    void LateUpdate() {
        if (!dirty) return;
        dirty = false;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.colors = colors;
    }
}
