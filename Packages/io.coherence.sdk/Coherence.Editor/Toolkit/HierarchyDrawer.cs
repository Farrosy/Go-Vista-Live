// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEditor;
    using Object = UnityEngine.Object;

    [InitializeOnLoad]
    internal static class HierarchyDrawer
    {
        private static class GUIContents
        {
            public static readonly GUIContent hasBindings = Icons.GetContent("Coherence.Auth.State");
            public static readonly GUIContent hasBindingsInChildren = Icons.GetContent("Coherence.Auth.None");

            public static readonly GUIContent orphan = Icons.GetContent("Coherence.Hierarchy.Orphan");
            public static readonly GUIContent connected = Icons.GetContent("Coherence.Connected", "Connected.");
            public static readonly GUIContent disconnected = Icons.GetContent("Coherence.Disconnected", "Disconnected.");
            public static readonly GUIContent simulator = Icons.GetContent("Coherence.ConnectionType.Simulator", "This scene is connected as a Simulator or host.");
            public static readonly GUIContent client = Icons.GetContent("Coherence.ConnectionType.Client", "Loaded through CoherenceSceneLoader with Client connection type.");
            public static readonly GUIContent loaded = Icons.GetContent("Coherence.Scene.Loaded", "CoherenceScene connection stablished.");

            public static readonly GUIContent hidden = EditorGUIUtility.TrIconContent("scenevis_hidden_hover");
            public static readonly GUIContent visible = EditorGUIUtility.TrIconContent("scenevis_visible_hover");

            public static readonly GUIContent authState = Icons.GetContent("Coherence.Auth.State", "You have State authority over this object.");
            public static readonly GUIContent authNone = Icons.GetContent("Coherence.Auth.None", "You don't have neither State nor Input authority over this object.");
            public static readonly GUIContent authBoth = Icons.GetContent("Coherence.Auth.Both", "You have both State and Input authority over this object.");
            public static readonly GUIContent authInput = Icons.GetContent("Coherence.Input", "You have Input authority over this object.");

            public static readonly GUIContent error = EditorGUIUtility.IconContent("Error");
            public static readonly GUIContent warning = EditorGUIUtility.IconContent("Warning");
        }

        private static readonly Dictionary<int, Component> hash = new();
        private static readonly System.Type sceneHierarchyWindowType;
        private static Object[] hierarchyWindows;

        static HierarchyDrawer()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
#endif
            sceneHierarchyWindowType = System.Type.GetType("UnityEditor.SceneHierarchyWindow,UnityEditor.dll");
        }

        /// <summary>
        /// Get the expanded state of an Object.
        /// </summary>
        /// <remarks>
        /// All opened scene hierarchy windows are traversed. If the Object is expanded at least on one of them, this method returns true.
        /// This method call might be slow, as it has to find all scene hierarchy windows opened everytime it's invoked, and finds the expanded IDs
        /// using SerializedObject/SerializedProperty APIs.
        /// </remarks>
        private static bool IsExpanded(int id)
        {
            hierarchyWindows = Resources.FindObjectsOfTypeAll(sceneHierarchyWindowType);
            foreach (var hw in hierarchyWindows)
            {
                using (var p = new SerializedObject(hw).FindProperty("m_SceneHierarchy.m_TreeViewState.m_ExpandedIDs"))
                {
                    for (int i = 0; i < p.arraySize; i++)
                    {
                        using (var item = p.GetArrayElementAtIndex(i))
                        {
                            if (id == item.intValue)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }

            return true;
        }

#if UNITY_6000_4_OR_NEWER
        private static void OnHierarchyGUI(EntityId entityId, Rect selectionRect)
        {
            var instanceId = entityId.GetInstanceId();
#if COHERENCE_DEBUG_EDITOR
            if (Event.current.alt)
            {
                GUI.Label(selectionRect, entityId.ToString("X"), ContentUtils.GUIStyles.greyMiniLabelRight);
            }
#endif
            OnHierarchyGUI(instanceId, selectionRect);
        }
#endif

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (CoherenceSyncBindingsWindow.Instance)
            {
                DrawConfigureTarget(instanceID, selectionRect, CoherenceSyncBindingsWindow.Instance);
            }

            if (Application.isPlaying)
            {
                DrawLeftSidePlayMode(instanceID, selectionRect);
            }

            // If we don't have at least 16px (icon size) of width to draw, skip drawing.
            if (TryGet(instanceID, out var gameObject) && (GetObjectRect(selectionRect, gameObject).width < 16))
            {
                return;
            }

            if (Application.isPlaying)
            {
                DrawScenePlayMode(instanceID, selectionRect);
            }
        }

        private static void DrawLeftSidePlayMode(int instanceID, Rect selectionRect)
        {
            // CoherenceSync

            if (TryGet(instanceID, out CoherenceSync sync))
            {
                var state = sync.EntityState;
                if (state == null)
                {
                    return;
                }

                var iconRect = new Rect(32, selectionRect.y, 16, 16);
                var icon = state.AuthorityType.Value switch
                {
                    AuthorityType.None => GUIContents.authNone,
                    AuthorityType.State => GUIContents.authState,
                    AuthorityType.Input => GUIContents.authInput,
                    AuthorityType.Full => GUIContents.authBoth,
                    _ => GUIContent.none,
                };

                EditorGUI.BeginDisabledGroup(!sync.isActiveAndEnabled);
                GUI.Label(iconRect, icon, GUIStyle.none);
                if (state.IsOrphaned)
                {
                    GUI.Label(iconRect, GUIContents.orphan, GUIStyle.none);
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        private static void DrawScenePlayMode(int instanceID, Rect selectionRect)
        {
            // CoherenceSceneLoader

            if (TryGet(instanceID, out CoherenceSceneLoader loader))
            {
                if (loader.Scene.IsValid())
                {
                    EditorGUI.BeginDisabledGroup(!loader.isActiveAndEnabled);
                    var iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                    if (GUI.Button(iconRect, GUIContents.loaded, GUIStyle.none))
                    {
                        if (CoherenceScene.map.TryGetValue(loader.Scene, out CoherenceScene l))
                        {
                            EditorGUIUtility.PingObject(l);
                        }
                        else
                        {
#if UNITY_6000_4_OR_NEWER
                            EditorGUIUtility.PingObject(EntityId.FromULong(loader.Scene.handle.GetRawData()));
#else
                            EditorGUIUtility.PingObject(loader.Scene.handle);
#endif
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }

            // CoherenceScene

            if (TryGet(instanceID, out CoherenceScene listener))
            {
                if (listener.Active)
                {
                    EditorGUI.BeginDisabledGroup(!listener.isActiveAndEnabled);
                    var iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                    var hasLoader = CoherenceSceneLoader.loaderMap.TryGetValue(listener.gameObject.scene, out loader);
                    if (GUI.Button(iconRect, GUIContents.loaded, GUIStyle.none))
                    {
                        if (hasLoader)
                        {
                            EditorGUIUtility.PingObject(loader);
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }

            // Bridge

            if (TryGet(instanceID, out CoherenceBridge bridge))
            {
                EditorGUI.BeginDisabledGroup(!bridge || !bridge.isActiveAndEnabled);
                var iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                if (!bridge.HasBakedData)
                {
                    GUI.Label(iconRect, GUIContents.error, GUIStyle.none);
                }
                else
                {
                    var connected = bridge.Client.IsConnected();
                    GUI.Label(iconRect, connected ? GUIContents.connected : GUIContents.disconnected, GUIStyle.none);
                }
                EditorGUI.EndDisabledGroup();
            }

            // Query

            if (TryGet(instanceID, out CoherenceQuery query))
            {
                if (CoherenceQueryValidator.HasAnyIssues(query))
                {
                    EditorGUI.BeginDisabledGroup(!query.isActiveAndEnabled);
                    var iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                    GUI.Label(iconRect, GUIContents.error, GUIStyle.none);
                    EditorGUI.EndDisabledGroup();
                }
            }

            // CloudLogin

            if (TryGet(instanceID, out CoherenceCloudLogin cloudLogin))
            {
                EditorGUI.BeginDisabledGroup(!cloudLogin || !cloudLogin.isActiveAndEnabled);
                var issues = cloudLogin.Validate();
                if (issues != CredentialsIssue.None || cloudLogin.Error is not null)
                {
                    var iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                    GUI.Label(iconRect, GUIContents.error, GUIStyle.none);
                }
                EditorGUI.EndDisabledGroup();
            }

            // Scene

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                var handle =
#if UNITY_6000_4_OR_NEWER
                    scene.handle.GetInstanceId();
#else
                    scene.handle;
#endif
                if (instanceID != handle)
                {
                    continue;
                }

                if (!scene.IsValid())
                {
                    continue;
                }

                var sceneRect = GetSceneRect(selectionRect, scene);
                var drawWidth = 0f;

                var visibleRect = new Rect(sceneRect.xMax - 20, sceneRect.y, 16, 16);
                var visible = SceneCullingUtils.IsVisible(scene);

                drawWidth += visibleRect.width;
                if (sceneRect.width < drawWidth)
                {
                    break;
                }
                if (GUI.Button(visibleRect, visible ? GUIContents.visible : GUIContents.hidden, GUIStyle.none))
                {
                    SceneCullingUtils.SetVisible(scene, !visible);
                }

                if (CoherenceBridgeStore.TryGetBridge(scene, out var sceneBridge) && sceneBridge.Client != null)
                {
                    var connectedRect = new Rect(visibleRect.xMin - 16, visibleRect.y, 16, 16);
                    drawWidth += connectedRect.width;
                    if (sceneRect.width < drawWidth)
                    {
                        break;
                    }

                    if (GUI.Button(connectedRect, sceneBridge.IsConnected ? GUIContents.connected : GUIContents.disconnected, GUIStyle.none))
                    {
                        EditorGUIUtility.PingObject(sceneBridge);
                    }

                    if (sceneBridge.IsConnected)
                    {
                        var connTypeRect = new Rect(connectedRect.xMin - 16, selectionRect.y, 16, 16);
                        drawWidth += connTypeRect.width;
                        if (sceneRect.width < drawWidth)
                        {
                            break;
                        }
                        GUI.Label(connTypeRect, sceneBridge.IsSimulatorOrHost ? GUIContents.simulator : GUIContents.client, GUIStyle.none);

                        var endpointLabelStyle = ContentUtils.GUIStyles.greyMiniLabelRight;
                        var epd = sceneBridge.Client.LastEndpointData;
                        var endpointString = epd.worldId != 0
                            ? epd.worldId.ToString()
                            : epd.uniqueRoomId.ToString();
                        var endpointContent = EditorGUIUtility.TrTempContent(endpointString);
                        var endpointWidth = endpointLabelStyle.CalcSize(endpointContent).x;
                        var endpointRect = new Rect(connTypeRect.xMin - endpointWidth, selectionRect.y, endpointWidth, 16);
                        drawWidth += endpointRect.width;
                        if (sceneRect.width < drawWidth)
                        {
                            break;
                        }
                        GUI.Label(endpointRect, endpointContent, ContentUtils.GUIStyles.greyMiniLabelRight);
                    }
                }

                break;
            }
        }

        private static bool HasBindingsOrComponentActions(CoherenceSync root, int instanceID)
        {
            foreach (var binding in root.Bindings)
            {
                if (binding == null)
                {
                    continue;
                }

                if (IsPartOfGameObject(binding.unityComponent, instanceID))
                {
                    return true;
                }
            }

            if (root.componentActions != null)
            {
                foreach (var componentAction in root.componentActions)
                {
                    if (componentAction == null)
                    {
                        continue;
                    }

                    if (IsPartOfGameObject(componentAction.component, instanceID))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPartOfGameObject(Component component, int instanceID)
        {
            var id =
#if UNITY_6000_4_OR_NEWER
                component ? component.gameObject.GetEntityId().GetInstanceId() : 0;
#else
                component ? component.gameObject.GetInstanceID() : 0;
#endif
            return component && id == instanceID;
        }

        private static bool IsPartOfChildGameObject(Component component, int instanceID)
        {
            if (!component || !component.gameObject)
            {
                return false;
            }

            var t = component.gameObject.transform.parent;
            while (t != null)
            {
                var id =
#if UNITY_6000_4_OR_NEWER
                    t.gameObject.GetEntityId().GetInstanceId();
#else
                    t.gameObject.GetInstanceID();
#endif
                if (id == instanceID)
                {
                    return true;
                }

                t = t.parent;
            }

            return false;
        }

        private static bool HasBindingsOrComponentActionsInChildren(CoherenceSync root, int instanceID)
        {
            foreach (var binding in root.Bindings)
            {
                if (binding == null)
                {
                    continue;
                }

                if (IsPartOfChildGameObject(binding.unityComponent, instanceID))
                {
                    return true;
                }
            }

            if (root.componentActions != null)
            {
                foreach (var componentAction in root.componentActions)
                {
                    if (componentAction == null)
                    {
                        continue;
                    }

                    if (IsPartOfChildGameObject(componentAction.component, instanceID))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void DrawConfigureTarget(int instanceID, Rect selectionRect, CoherenceSyncBindingsWindow window)
        {
            if (!window || !window.Sync)
            {
                return;
            }

            if (HasBindingsOrComponentActions(window.Sync, instanceID))
            {
                var root = window.Sync.gameObject;
                EditorGUI.BeginDisabledGroup(!root.activeInHierarchy);
                var iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                //var iconRect = new Rect(32, selectionRect.y, 16, 16);
                GUI.Label(iconRect, GUIContents.hasBindings, GUIStyle.none);
                EditorGUI.EndDisabledGroup();
            }
            else if (HasBindingsOrComponentActionsInChildren(window.Sync, instanceID) && !IsExpanded(instanceID))
            {
                var root = window.Sync.gameObject;
                EditorGUI.BeginDisabledGroup(!root.activeInHierarchy);
                var iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                //var iconRect = new Rect(32, selectionRect.y, 16, 16);
                GUI.Label(iconRect, GUIContents.hasBindingsInChildren, GUIStyle.none);
                EditorGUI.EndDisabledGroup();
            }
        }

        private static bool TryGet(int instanceID, out GameObject gameObject)
        {
            gameObject = EditorUtility.
#if UNITY_6000_4_OR_NEWER
                EntityIdToObject(EntityId.FromULong(EntityIdExtensions.GetFromInstanceId(instanceID, SceneManager.GetActiveScene()))) as GameObject;
#elif UNITY_6000_3_OR_NEWER
                EntityIdToObject(instanceID) as GameObject;
#else
                InstanceIDToObject(instanceID) as GameObject;
#endif

            return gameObject;
        }

        private static bool TryGet<T>(int instanceID, out T component) where T : Component
        {
            if (hash.TryGetValue(instanceID, out var c))
            {
                component = c as T;
                if (!component)
                {
                    _ = hash.Remove(instanceID);
                    return false;
                }

                return true;
            }
            else
            {
                var go = EditorUtility.
#if UNITY_6000_4_OR_NEWER
                    EntityIdToObject(EntityId.FromULong(EntityIdExtensions.GetFromInstanceId(instanceID, SceneManager.GetActiveScene()))) as GameObject;
#elif UNITY_6000_3_OR_NEWER
                    EntityIdToObject(instanceID) as GameObject;
#else
                    InstanceIDToObject(instanceID) as GameObject;
#endif

                if (!go)
                {
                    component = null;
                    return false;
                }

                var hasComponent = go.TryGetComponent(out component);
                if (hasComponent)
                {
                    hash.Add(instanceID, component);
                }

                return hasComponent;
            }
        }

        private static float GetSceneLabelWidth(Scene scene)
        {
            var style = SceneManager.GetActiveScene() == scene
                ? EditorStyles.boldLabel
                : EditorStyles.label;

            var name = scene.name;
            if (scene.isDirty)
            {
                name += "*";
            }

            // TODO handle loading state scenes, as done in GameObjectTreeViewGUI.OnContentGUI

            var content = EditorGUIUtility.TrTempContent(name);
            var width = style.CalcSize(content).x;

            return width;
        }

        private static Rect GetSceneRect(Rect rect, Scene scene)
        {
            const float iconWidth = 18f;
            var labelWidth = iconWidth + GetSceneLabelWidth(scene);

            if (rect.width < labelWidth)
            {
                return Rect.zero;
            }

            rect.xMin += labelWidth;

            return rect;
        }

        private static float GetObjectLabelWidth(Object obj)
        {
            var style = EditorStyles.label;
            var content = EditorGUIUtility.TrTempContent(obj.name);
            var width = style.CalcSize(content).x;
            return width;
        }

        private static Rect GetObjectRect(Rect rect, Object obj)
        {
            const float iconWidth = 18f;
            var labelWidth = iconWidth + GetObjectLabelWidth(obj);

            if (rect.width < labelWidth)
            {
                return Rect.zero;
            }

            rect.xMin += labelWidth;

            return rect;
        }
    }
}
