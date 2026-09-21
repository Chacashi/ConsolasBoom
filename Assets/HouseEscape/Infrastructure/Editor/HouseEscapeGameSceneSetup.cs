using System;
using System.Collections.Generic;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using HouseEscape.Presentation.Participants;
using HouseEscape.Presentation.Timeline;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HouseEscape.Infrastructure.Editor
{
    public static class HouseEscapeGameSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string InputActionsPath = "Assets/HouseEscape/LocalMultiplayer/LocalParticipants.inputactions";
        private const string PrefabFolder = "Assets/HouseEscape/LocalMultiplayer/Prefabs";
        private const string ContextPrefabPath = PrefabFolder + "/LocalParticipantContext.prefab";
        private const string GeneratedRootName = "House Escape Runtime";
        private const string NetworkRootName = "Network Bootstrap";
        private const string TimelineRootName = "Match Timeline";
        private const string RegistryRootName = "Participant Registry";

        [MenuItem("House Escape/Configure Game Local Participants Scene")]
        public static void ConfigureGameScene()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException($"Required gamepad input actions are missing at {InputActionsPath}.");
            }

            EnsureFolder(PrefabFolder);
            GameObject contextPrefab = CreateOrUpdateContextPrefab(inputActions);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            RemovePreviousGeneratedRoots(scene);

            Light[] controlledLights = CollectSceneComponents<Light>(scene);
            DisablePreviousLocalPresentation(scene);

            GameObject runtimeRoot = CreateRoot(GeneratedRootName, null);
            runtimeRoot.SetActive(false);
            NetworkManager networkManager = CreateNetworkBootstrap();
            NetworkMatchTimeline timeline = CreateTimeline();
            NetworkParticipantRegistry registry = CreateParticipantRegistry();
            MatchTimelinePresentationSource presentation = CreatePresentationSource(runtimeRoot.transform, timeline);
            HouseLightRigView lightRig = CreateLightRig(runtimeRoot.transform, presentation, controlledLights);
            AudioListener audioListener = CreateSharedAudio(runtimeRoot.transform, presentation);
            CreateJoiningPresentation(runtimeRoot.transform, out Camera joiningCamera, out Canvas joiningCanvas);

            LocalParticipantContext[] contexts = new LocalParticipantContext[4];
            LocalParticipantHudView[] huds = new LocalParticipantHudView[4];
            CreateParticipantContexts(runtimeRoot.transform, contextPrefab, presentation, contexts, huds);

            LocalParticipantJoinManager joinManager = runtimeRoot.AddComponent<LocalParticipantJoinManager>();
            joinManager.Initialize(inputActions, contexts);
            LocalParticipantComposition composition = runtimeRoot.AddComponent<LocalParticipantComposition>();
            composition.Initialize(
                networkManager,
                registry,
                timeline,
                joinManager,
                contexts,
                huds,
                joiningCamera,
                joiningCanvas,
                audioListener);
            runtimeRoot.SetActive(true);

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(joinManager);
            EditorUtility.SetDirty(composition);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(lightRig);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Configured local participants and authoritative timeline in {ScenePath}.");
        }

        public static void ConfigureGameSceneBatch() => ConfigureGameScene();

        private static NetworkManager CreateNetworkBootstrap()
        {
            GameObject gameObject = CreateRoot(NetworkRootName, null);
            NetworkManager networkManager = gameObject.AddComponent<NetworkManager>();
            UnityTransport transport = gameObject.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.EnableSceneManagement = true;
            networkManager.NetworkConfig.PlayerPrefab = null;
            NetworkSessionBootstrap bootstrap = gameObject.AddComponent<NetworkSessionBootstrap>();
            bootstrap.Initialize(networkManager);
            return networkManager;
        }

        private static NetworkMatchTimeline CreateTimeline()
        {
            GameObject gameObject = CreateRoot(TimelineRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkMatchTimeline timeline = gameObject.AddComponent<NetworkMatchTimeline>();
            var serializedTimeline = new SerializedObject(timeline);
            serializedTimeline.FindProperty("startMatchOnServerSpawn").boolValue = false;
            serializedTimeline.ApplyModifiedPropertiesWithoutUndo();
            return timeline;
        }

        private static NetworkParticipantRegistry CreateParticipantRegistry()
        {
            GameObject gameObject = CreateRoot(RegistryRootName, null);
            gameObject.AddComponent<NetworkObject>();
            return gameObject.AddComponent<NetworkParticipantRegistry>();
        }

        private static MatchTimelinePresentationSource CreatePresentationSource(
            Transform parent,
            NetworkMatchTimeline timeline)
        {
            GameObject gameObject = CreateRoot("Timeline Presentation", parent);
            MatchTimelinePresentationSource presentation = gameObject.AddComponent<MatchTimelinePresentationSource>();
            presentation.Initialize(timeline);
            return presentation;
        }

        private static HouseLightRigView CreateLightRig(
            Transform parent,
            MatchTimelinePresentationSource presentation,
            Light[] controlledLights)
        {
            GameObject gameObject = CreateRoot("House Light Rig", parent);
            HouseLightRigView rig = gameObject.AddComponent<HouseLightRigView>();
            rig.Initialize(presentation, controlledLights);
            return rig;
        }

        private static AudioListener CreateSharedAudio(
            Transform parent,
            MatchTimelinePresentationSource presentation)
        {
            GameObject gameObject = CreateRoot("Shared Local Audio", parent);
            AudioListener listener = gameObject.AddComponent<AudioListener>();
            AudioSource warningAudio = gameObject.AddComponent<AudioSource>();
            warningAudio.playOnAwake = false;
            warningAudio.spatialBlend = 0f;
            gameObject.AddComponent<LightTransitionWarningView>().Initialize(presentation, null, warningAudio);
            return listener;
        }

        private static void CreateJoiningPresentation(
            Transform parent,
            out Camera joiningCamera,
            out Canvas joiningCanvas)
        {
            GameObject cameraObject = CreateRoot("Joining Camera", parent);
            joiningCamera = cameraObject.AddComponent<Camera>();
            joiningCamera.clearFlags = CameraClearFlags.SolidColor;
            joiningCamera.backgroundColor = new Color(0.015f, 0.02f, 0.025f);
            joiningCamera.cullingMask = 0;
            joiningCamera.depth = -100f;

            GameObject canvasObject = CreateUiObject("Joining HUD", parent);
            joiningCanvas = canvasObject.AddComponent<Canvas>();
            joiningCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            joiningCanvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject panel = CreateUiObject("Joining Panel", canvasObject.transform);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 220f);
            panel.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.94f);

            TMP_Text title = CreateText("Joining Title", panel.transform, new Vector2(0f, -35f), 42f, Color.white);
            title.text = "PRESS MENU TO JOIN";
            TMP_Text start = CreateText("Start Hint", panel.transform, new Vector2(0f, -110f), 28f, new Color(1f, 0.75f, 0.25f));
            start.text = "PLAYER 1: PRESS A / CROSS TO START";
        }

        private static void CreateParticipantContexts(
            Transform parent,
            GameObject prefab,
            MatchTimelinePresentationSource presentation,
            LocalParticipantContext[] contexts,
            LocalParticipantHudView[] huds)
        {
            for (byte slot = 0; slot < contexts.Length; slot++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = $"Local Participant {slot + 1}";
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = new Vector3((slot - 1.5f) * 2f, 1.6f, -6f);
                instance.SetActive(true);

                LocalParticipantContext context = instance.GetComponent<LocalParticipantContext>();
                PlayerInput playerInput = instance.GetComponent<PlayerInput>();
                Camera camera = instance.GetComponentInChildren<Camera>(true);
                Canvas canvas = instance.GetComponentInChildren<Canvas>(true);
                LocalParticipantHudView hud = instance.GetComponentInChildren<LocalParticipantHudView>(true);
                context.Initialize(slot, playerInput, camera, canvas);
                hud.Prepare(slot);
                canvas.worldCamera = camera;
                CreateTimelineHud(canvas.transform, presentation);
                contexts[slot] = context;
                huds[slot] = hud;
            }
        }

        private static GameObject CreateOrUpdateContextPrefab(InputActionAsset inputActions)
        {
            var root = new GameObject("LocalParticipantContext");
            root.SetActive(false);
            PlayerInput playerInput = root.AddComponent<PlayerInput>();
            playerInput.defaultActionMap = LocalParticipantContext.ActionMapName;
            playerInput.defaultControlScheme = LocalParticipantContext.GamepadControlScheme;
            playerInput.neverAutoSwitchControlSchemes = true;
            playerInput.actions = inputActions;
            playerInput.enabled = false;

            GameObject cameraObject = CreateRoot("Participant Camera", root.transform);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            cameraObject.tag = "Untagged";

            GameObject canvasObject = CreateUiObject("Participant HUD", root.transform);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            canvasObject.AddComponent<GraphicRaycaster>();
            canvas.enabled = false;

            TMP_Text participantLabel = CreateText("Participant Label", canvasObject.transform, new Vector2(20f, -20f), 34f, Color.white);
            RectTransform participantRect = (RectTransform)participantLabel.transform;
            participantRect.anchorMin = participantRect.anchorMax = new Vector2(0f, 1f);
            participantRect.pivot = new Vector2(0f, 1f);
            participantRect.sizeDelta = new Vector2(140f, 50f);

            GameObject reconnectPanel = CreateUiObject("Reconnect Panel", canvasObject.transform);
            RectTransform reconnectRect = (RectTransform)reconnectPanel.transform;
            reconnectRect.anchorMin = reconnectRect.anchorMax = new Vector2(0.5f, 0.5f);
            reconnectRect.pivot = new Vector2(0.5f, 0.5f);
            reconnectRect.sizeDelta = new Vector2(620f, 80f);
            Image reconnectBackground = reconnectPanel.AddComponent<Image>();
            reconnectBackground.color = new Color(0.45f, 0.03f, 0.02f, 0.9f);
            TMP_Text reconnectLabel = CreateText("Reconnect Label", reconnectPanel.transform, Vector2.zero, 32f, Color.white);
            reconnectPanel.SetActive(false);

            LocalParticipantHudView hud = canvasObject.AddComponent<LocalParticipantHudView>();
            hud.Initialize(participantLabel, reconnectLabel);
            LocalParticipantContext context = root.AddComponent<LocalParticipantContext>();
            context.Initialize(0, playerInput, camera, canvas);

            root.SetActive(true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ContextPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateTimelineHud(Transform canvas, MatchTimelinePresentationSource presentation)
        {
            GameObject panel = CreateUiObject("Clock Panel", canvas);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-20f, -20f);
            panelRect.sizeDelta = new Vector2(330f, 170f);
            panel.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.88f);

            TMP_Text time = CreateText("In-game Time", panel.transform, new Vector2(0f, -10f), 30f, Color.white);
            TMP_Text match = CreateText("Match Remaining", panel.transform, new Vector2(0f, -48f), 22f, new Color(0.75f, 0.85f, 0.9f));
            TMP_Text state = CreateText("Light State", panel.transform, new Vector2(0f, -84f), 25f, new Color(1f, 0.75f, 0.25f));
            TMP_Text phase = CreateText("Phase Remaining", panel.transform, new Vector2(0f, -122f), 22f, Color.white);
            panel.AddComponent<MatchTimelineHudView>().Initialize(presentation, time, match, state, phase);

            GameObject warning = CreateUiObject("Light Transition Warning", canvas);
            RectTransform warningRect = (RectTransform)warning.transform;
            warningRect.anchorMin = warningRect.anchorMax = new Vector2(0.5f, 0.82f);
            warningRect.pivot = new Vector2(0.5f, 0.5f);
            warningRect.sizeDelta = new Vector2(560f, 64f);
            warning.AddComponent<Image>().color = new Color(0.55f, 0.04f, 0.02f, 0.92f);
            CanvasGroup warningGroup = warning.AddComponent<CanvasGroup>();
            TMP_Text warningText = CreateText("Warning Text", warning.transform, Vector2.zero, 28f, Color.white);
            warningText.text = "LIGHTS CHANGING";
            warning.AddComponent<LightTransitionWarningView>().Initialize(presentation, warningGroup, null);
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 position, float size, Color color)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(-24f, 38f);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void DisablePreviousLocalPresentation(Scene scene)
        {
            foreach (Camera camera in CollectSceneComponents<Camera>(scene))
            {
                camera.enabled = false;
            }

            foreach (AudioListener listener in CollectSceneComponents<AudioListener>(scene))
            {
                listener.enabled = false;
            }
        }

        private static T[] CollectSceneComponents<T>(Scene scene) where T : Component
        {
            var components = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        private static void RemovePreviousGeneratedRoots(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GeneratedRootName
                    || root.name == NetworkRootName
                    || root.name == TimelineRootName
                    || root.name == RegistryRootName)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static GameObject CreateRoot(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }
    }
}
