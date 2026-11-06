#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Bozo.ModularCharacters
{
    /// Go Vista addition: turns a BoZo character into prefabs that can be dropped into WorldScene.
    ///
    /// For each character it writes to Assets/Characters/NPC/&lt;Name&gt;/:
    ///   &lt;Name&gt;.prefab      baked static model (one mesh + baked textures, humanoid NPC animator)
    ///   NPC_&lt;Name&gt;.prefab  ready NPC: Interactable, DialogueTrigger, NpcController, NavMeshAgent,
    ///                        NpcNavigation, NpcObstacleAvoidance, NpcGroundSnap + the model nested inside.
    ///
    /// Saving again with the same name overwrites the model. An existing NPC_&lt;Name&gt; prefab is kept
    /// (so dialogue/navigation settings on it survive) and picks up the new model automatically.
    ///
    /// Editor only: prefabs can't be created in a built game.
    public static class BoZo_NpcPrefabExporter
    {
        public const string NpcRootFolder = "Assets/Characters/NPC";

        const string BasePrefabName = "BSMC_CharacterBase";
        const string IdleClipName = "BMAC_F_Idle";
        const string WalkClipName = "BMAC_F_Walk";
        const string ControllerPath = "Assets/Characters/Animation/AnimationController/NpcHumanoidController.controller";
        const string TalkActionPath = "Assets/NPCAction/Talk.asset";
        const string PromptPrefabPath = "Assets/MobilePrefabs/NewInteractPrompt.prefab";

        const float TimeoutSeconds = 90f;
        const float SettleSeconds = 1f;
        static readonly Vector3 HiddenOffset = new Vector3(0f, -500f, 0f);

        public static bool IsExporting { get; private set; }

        public static string SanitizeName(string name)
        {
            name = (name ?? "").Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrEmpty(name) ? "NewCharacter" : name;
        }

        public static string FolderFor(string characterName) => $"{NpcRootFolder}/{SanitizeName(characterName)}";

        /// Play Mode only. Bakes <paramref name="data"/> on a hidden temporary copy of BSMC_CharacterBase,
        /// so the character currently being edited in the Character Creator is left untouched.
        public static IEnumerator BakeAndExport(CharacterData data, string characterName)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BoZo NPC Export] Bake hanya bisa di Play Mode.");
                yield break;
            }
            if (IsExporting)
            {
                Debug.LogWarning("[BoZo NPC Export] Masih ada export yang berjalan, tunggu sampai selesai.");
                yield break;
            }

            var basePrefab = FindAsset<GameObject>(BasePrefabName, "Prefab");
            if (!basePrefab) { Debug.LogError($"[BoZo NPC Export] {BasePrefabName}.prefab tidak ditemukan."); yield break; }

            IsExporting = true;
            string name = SanitizeName(characterName);
            string folder = FolderFor(name);
            GameObject temp = null;

            try
            {
                // Instantiate under an inactive parent so Awake doesn't auto-load the prefab's default character.
                var holder = new GameObject("__BoZoNpcExportHolder");
                holder.SetActive(false);
                temp = Object.Instantiate(basePrefab, HiddenOffset, Quaternion.identity, holder.transform);
                temp.name = name;

                var os = temp.GetComponent<OutfitSystem>();
                os.characterData = null;
                os.loadMode = OutfitSystem.LoadMode.Manual;
                os.prefabName = name;
                foreach (var cam in temp.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
                foreach (var listener in temp.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;

                temp.transform.SetParent(null, true);
                Object.Destroy(holder);

                os.LoadCharacter(data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Cleanup(temp);
                yield break;
            }

            // Wait for BoZo to finish loading outfits, merging and updating textures.
            var outfit = temp.GetComponent<OutfitSystem>();
            float start = Time.realtimeSinceStartup;
            bool sawBusy = false;
            while (true)
            {
                if (!outfit) { Debug.LogError("[BoZo NPC Export] Karakter sementara hilang sebelum selesai."); Cleanup(temp); yield break; }
                if (Time.realtimeSinceStartup - start > TimeoutSeconds) { Debug.LogError("[BoZo NPC Export] Timeout menunggu load/merge."); Cleanup(temp); yield break; }

                bool busy = outfit.isloading || outfit.isMerging || outfit.isUpdatingTexture;
                if (busy) sawBusy = true;
                else if (sawBusy) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(SettleSeconds);
            while (outfit.isloading || outfit.isMerging || outfit.isUpdatingTexture) yield return null;

            // Back to the origin (keeping any heel height BoZo added) so the prefab root isn't saved 500 m down.
            temp.transform.position -= HiddenOffset;

            var task = outfit.SaveCharacterToPrefabAsync(folder);
            while (!task.IsCompleted) yield return null;

            GameObject model = null;
            if (task.IsFaulted) Debug.LogException(task.Exception);
            else model = task.Result;

            Cleanup(temp);
            if (!model) yield break;

            try
            {
                ApplyNpcAnimator(model);
                var npc = BuildNpcPrefab(model, name, $"{folder}/NPC_{name}.prefab", overwriteExisting: false);
                if (npc)
                {
                    Debug.Log($"[BoZo NPC Export] Selesai: {folder}/NPC_{name}.prefab. Drag ke WorldScene, isi Dialogue di DialogueTrigger.", npc);
                    EditorGUIUtility.PingObject(npc);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Cleanup(GameObject temp)
        {
            if (temp) Object.Destroy(temp);
            IsExporting = false;
        }

        /// Puts the humanoid NPC controller on the baked model prefab itself, so it survives re-bakes
        /// and the NPC prefab needs no override on the nested model.
        public static void ApplyNpcAnimator(GameObject modelPrefab)
        {
            string path = AssetDatabase.GetAssetPath(modelPrefab);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = contents.GetComponentInChildren<Animator>();
                if (!animator) { Debug.LogWarning("[BoZo NPC Export] Animator tidak ditemukan di " + path); return; }
                animator.runtimeAnimatorController = GetOrCreateController();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// Builds NPC_&lt;Name&gt; around a baked model. With overwriteExisting = false an existing NPC prefab is
        /// left alone (it already nests the model prefab, so it shows the new look).
        public static GameObject BuildNpcPrefab(GameObject bakedModel, string displayName, string npcPrefabPath, bool overwriteExisting)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(npcPrefabPath);
            if (existing && !overwriteExisting)
            {
                Debug.Log($"[BoZo NPC Export] {npcPrefabPath} sudah ada: model diperbarui, setting NPC (dialog, navigasi) tetap.", existing);
                return existing;
            }

            // Build under an inactive parent: in Play Mode this keeps Awake/OnEnable of the NPC scripts
            // (and NavMeshAgent registration) from running on this temporary object.
            var holder = new GameObject("__BoZoNpcBuildHolder");
            holder.SetActive(false);
            try
            {
                var root = new GameObject("NPC_" + displayName);
                root.transform.SetParent(holder.transform, false);

                // Interaction zone (same values as NPC_Kota). Must exist before Interactable (RequireComponent).
                var zone = root.AddComponent<CapsuleCollider>();
                zone.isTrigger = true;
                zone.radius = 1.39f;
                zone.height = 2f;
                zone.center = new Vector3(0f, 0.75f, 0f);

                var interactable = root.AddComponent<Interactable>();
                var trigger = root.AddComponent<DialogueTrigger>();
                root.AddComponent<NpcController>();

                interactable.displayName = displayName;
                interactable.action = AssetDatabase.LoadAssetAtPath<InteractionAction>(TalkActionPath);
                // AddComponent doesn't initialize serialized UnityEvent fields until the object is serialized.
                if (interactable.onInteract == null) interactable.onInteract = new UnityEvent();
                UnityEventTools.AddVoidPersistentListener(interactable.onInteract, trigger.StartDialogue);

                var so = new SerializedObject(interactable);
                so.FindProperty("promptPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InteractPromptWorld>(PromptPrefabPath);
                so.FindProperty("promptOffset").vector3Value = new Vector3(0f, 2.1f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();

                // Nested model prefab: re-baking the model updates every NPC that uses it.
                var model = (GameObject)PrefabUtility.InstantiatePrefab(bakedModel, root.transform);
                model.name = "Model";
                // Keep the prefab's own Y: BoZo raises the root for heeled shoes.
                model.transform.localPosition = new Vector3(0f, bakedModel.transform.localPosition.y, 0f);
                model.transform.localRotation = Quaternion.identity;

                var body = new GameObject("BodyCollider");
                body.transform.SetParent(root.transform, false);
                var bodyCol = body.AddComponent<CapsuleCollider>();
                bodyCol.radius = 0.3f;
                bodyCol.height = 1.7f;
                bodyCol.center = new Vector3(0f, 0.85f, 0f);

                AddNavigation(root, useUndo: false);

                EnsureFolder(Path.GetDirectoryName(npcPrefabPath).Replace('\\', '/'));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, npcPrefabPath, out bool ok);
                if (!ok) { Debug.LogError("[BoZo NPC Export] Gagal menyimpan " + npcPrefabPath); return null; }
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        /// NavMeshAgent sized for a BoZo character + wander/patrol + obstacle avoidance + ground snap.
        /// Existing components are kept. Also swaps a Character Creator animator for the NPC one.
        public static void AddNavigation(GameObject npc, bool useUndo)
        {
            T Add<T>() where T : Component => useUndo ? Undo.AddComponent<T>(npc) : npc.AddComponent<T>();

            if (!npc.GetComponent<NavMeshAgent>())
            {
                var agent = Add<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 1.7f;
                agent.speed = 1.5f;
                agent.angularSpeed = 360f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.2f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            }
            if (!npc.GetComponent<NpcNavigation>()) Add<NpcNavigation>();
            if (!npc.GetComponent<NpcObstacleAvoidance>()) Add<NpcObstacleAvoidance>();
            if (!npc.GetComponent<NpcGroundSnap>()) Add<NpcGroundSnap>();

            var animator = npc.GetComponentInChildren<Animator>();
            if (animator && !HasParameter(animator.runtimeAnimatorController as AnimatorController, "Speed"))
            {
                if (useUndo) Undo.RecordObject(animator, "Set NPC Animator");
                animator.runtimeAnimatorController = GetOrCreateController();
                animator.applyRootMotion = false;
            }
        }

        static bool HasParameter(AnimatorController ctrl, string name)
        {
            if (!ctrl) return false;
            foreach (var p in ctrl.parameters)
                if (p.name == name) return true;
            return false;
        }

        /// Humanoid NPC controller driven by NpcNavigation (Speed) and NpcController (IsTalking).
        public static AnimatorController GetOrCreateController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing) return existing;

            var idle = FindAsset<AnimationClip>(IdleClipName, "AnimationClip");
            var walk = FindAsset<AnimationClip>(WalkClipName, "AnimationClip");

            EnsureFolder(Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var idleState = sm.AddState("Idle", new Vector3(300, 0));
            idleState.motion = idle;
            var walkState = sm.AddState("Walk", new Vector3(300, 120));
            walkState.motion = walk;
            // No talk clip in BoZo yet: reuse idle so a real talk animation can be dropped in later.
            var talkState = sm.AddState("Talk", new Vector3(560, 0));
            talkState.motion = idle;
            sm.defaultState = idleState;

            void Transition(AnimatorState from, AnimatorState to, float duration, AnimatorConditionMode mode, float threshold, string param)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = false;
                t.duration = duration;
                t.AddCondition(mode, threshold, param);
            }
            Transition(idleState, walkState, 0.15f, AnimatorConditionMode.Greater, 0.1f, "Speed");
            Transition(walkState, idleState, 0.15f, AnimatorConditionMode.Less, 0.1f, "Speed");
            Transition(idleState, talkState, 0.2f, AnimatorConditionMode.If, 0f, "IsTalking");
            Transition(walkState, talkState, 0.2f, AnimatorConditionMode.If, 0f, "IsTalking");
            Transition(talkState, idleState, 0.2f, AnimatorConditionMode.IfNot, 0f, "IsTalking");

            AssetDatabase.SaveAssets();
            return ctrl;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T FindAsset<T>(string assetName, string typeName) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"{assetName} t:{typeName}"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == assetName) return AssetDatabase.LoadAssetAtPath<T>(p);
            }
            return null;
        }
    }
}
#endif
