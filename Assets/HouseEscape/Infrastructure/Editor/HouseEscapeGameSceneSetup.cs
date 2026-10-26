using System;
using System.Collections.Generic;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Gameplay.Flashlight;
using HouseEscape.Gameplay.Generators;
using HouseEscape.Gameplay.Hiding;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Flashlight;
using HouseEscape.Networking.Generators;
using HouseEscape.Networking.Hiding;
using HouseEscape.Networking.Items;
using HouseEscape.Networking.Match;
using HouseEscape.Networking.Monster;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Scoring;
using HouseEscape.Networking.Timeline;
using HouseEscape.Networking.Vitality;
using HouseEscape.Presentation.Flashlight;
using HouseEscape.Presentation.Generators;
using HouseEscape.Presentation.Hiding;
using HouseEscape.Presentation.Items;
using HouseEscape.Presentation.Participants;
using HouseEscape.Presentation.Timeline;
using HouseEscape.Presentation.Vitality;
using TMPro;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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
        private const string AvatarPrefabFolder = "Assets/HouseEscape/Networking/Prefabs";
        private const string AvatarPrefabPath = AvatarPrefabFolder + "/NetworkParticipantAvatar.prefab";
        private const string GeneratorPrefabPath = AvatarPrefabFolder + "/NetworkGenerator.prefab";
        private const string GeneratorActiveMaterialPath = AvatarPrefabFolder + "/GeneratorActive.mat";
        private const string GeneratorInactiveMaterialPath = AvatarPrefabFolder + "/GeneratorInactive.mat";
        private const string MonsterPrefabPath = AvatarPrefabFolder + "/NetworkMonster.prefab";
        private const string MonsterMaterialPath = AvatarPrefabFolder + "/MonsterPlaceholder.mat";
        private const string MapItemPrefabPath = AvatarPrefabFolder + "/NetworkMapItem.prefab";
        private const string MapItemMaterialPath = AvatarPrefabFolder + "/MapItemPlaceholder.mat";
        private const string NavMeshFolder = "Assets/Scenes/Game";
        private const string NavMeshDataPath = NavMeshFolder + "/MonsterArenaNavMesh.asset";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string GeneratedRootName = "House Escape Runtime";
        private const string NetworkRootName = "Network Bootstrap";
        private const string TimelineRootName = "Match Timeline";
        private const string RegistryRootName = "Participant Registry";
        private const string GeneratorRaceRootName = "Generator Race";
        private const string GeneratorsRootName = "Generators";
        private const string MonsterRootName = "Monster";
        private const string VitalityRootName = "Participant Vitality";
        private const string InventoryRootName = "Participant Inventory";
        private const string ScoreboardRootName = "Participant Scores";
        private const string RevivalRootName = "Participant Revival";
        private const string FlashlightsRootName = "Participant Flashlights";
        private const string FrontDoorRootName = "Front Door (Placeholder)";
        private const string HidingPlacesRootName = "Hiding Places";
        private const string HidingSpotsRootName = "Hiding Spots (Placeholder)";
        private const string SurvivalScoringRootName = "Survival Scoring";
        private const string HidingSpotMaterialPath = AvatarPrefabFolder + "/HidingSpotPlaceholder.mat";
        private const string ChargingStationsRootName = "Charging Stations (Placeholder)";
        private const string ChargingStationMaterialPath = AvatarPrefabFolder + "/ChargingStationPlaceholder.mat";
        private const string MapItemFieldRootName = "Map Items";
        private const string MapItemPositionsRootName = "Map Item Positions (Placeholder)";
        private const string MonsterArenaRootName = "Monster Arena (Placeholder)";

        // The house model (Assets/3D/Modelado) is a single floor with its floor at y = 0 and the
        // front door on the north wall. The setup adds mesh colliders to it, bakes the monster
        // NavMesh inside its walls and places every gameplay object in its rooms. Each position is
        // snapped to the NavMesh and must be reachable from the spawn, or the setup fails.
        private const string HouseRootName = "Modelado_TrabajoFinal3ero";
        private const string HouseNavMeshRootName = "House NavMesh";

        // Model groups without colliders: walk-through interior doors, the ceiling, flat or wall
        // decorations and small props.
        private static readonly string[] HouseGroupsWithoutColliders =
        {
            "Alfombras", "Cuadros", "Luces techo", "Objetos Interactuables", "Objetos inventario", "Puertas"
        };

        private const string HouseCeilingName = "Techo";

        // Only these groups are walkable floor; furniture, walls and the rest only obstruct, so
        // the monster never walks on sofa seats, tables or wall tops.
        private static readonly string[] HouseWalkableGroups = { "Piso y techo", "Caminos calle" };

        // NavMesh volume: the house interior between its outer walls, up to the front door.
        private static readonly Vector3 HouseInteriorCenter = new Vector3(-0.6f, 1.5f, 25.75f);
        private static readonly Vector3 HouseInteriorSize = new Vector3(84f, 4f, 62.5f);

        // Main room (dining hall) centre: the four spawns sit 1.5 m around it.
        private static readonly Vector3 SpawnCenter = new Vector3(-6f, 0f, 11f);

        private static readonly Vector3 MonsterSpawnPosition = new Vector3(10f, 0f, 30f);

        // One generator per main room of this model (it has no basement, attic or garage; the
        // library, office and storage room stand in for them). Generator 1 sits 1.5 m in front of
        // participant spawn 1 so the race can be exercised immediately after match start.
        private static readonly Vector3[] GeneratorPositions =
        {
            SpawnCenter + new Vector3(-1.5f, 0f, 0f), // Main room
            new Vector3(19f, 0f, 10f), // Kitchen
            new Vector3(22f, 0f, 40f), // Main bedroom
            new Vector3(-33f, 0f, 30f), // Library
            new Vector3(-30f, 0f, 10f), // Office
            new Vector3(34f, 0f, 33f) // Storage room
        };

        // Two valid positions beside each generator for strong healing items.
        private static readonly Vector3[] NearGeneratorItemOffsets =
        {
            new Vector3(1.5f, 0f, 1.5f),
            new Vector3(-1.5f, 0f, -1.5f),
            new Vector3(1.5f, 0f, -1.5f),
            new Vector3(-1.5f, 0f, 1.5f),
            new Vector3(2.2f, 0f, 0f),
            new Vector3(-2.2f, 0f, 0f),
            new Vector3(0f, 0f, 2.2f),
            new Vector3(0f, 0f, -2.2f)
        };

        private const int NearGeneratorItemPositionsPerGenerator = 2;

        // General item positions: a grid over the interior, keeping points on the NavMesh that
        // are reachable and spaced apart, away from the spawns.
        private const float GeneralItemGridStep = 3f;
        private const float GeneralItemMinimumSpacing = 4f;
        private const float GeneralItemSpawnClearance = 5f;
        private const int GeneralItemMaximumCount = 40;

        // GDD 3.2: three or four charging stations.
        private static readonly Vector3[] ChargingStationPositions =
        {
            new Vector3(5f, 0f, 20f), // Corridor beside the main room
            new Vector3(-19f, 0f, 33f), // West sitting room
            new Vector3(10f, 0f, 45f), // North-east hall
            new Vector3(-11f, 0f, 2.5f) // Fireplace
        };

        // GDD 3.6 quantities: wardrobes 4-5, beds 3-4, chests 2-3, small closets 2-3. Hiding places
        // use the model's furniture (path under the house root): its tall cabinets as wardrobes,
        // the bed and two large sofas as beds, two storage boxes as chests. The model has no small
        // closets, so those two are placeholder props at a floor position.
        private static readonly (HidingSpotType Type, string Furniture, Vector3 Position)[] HidingSpotLayout =
        {
            (HidingSpotType.Wardrobe, "Dormitorio/ropero", default),
            (HidingSpotType.Wardrobe, "Libreros/Librero 010", default),
            (HidingSpotType.Wardrobe, "Libreros/Librero 011", default),
            (HidingSpotType.Wardrobe, "Sala/Mueble televisor", default),
            (HidingSpotType.Bed, "Dormitorio/cama", default),
            (HidingSpotType.Bed, "Sofas/sofa grande", default),
            (HidingSpotType.Bed, "Sofas/sofa grande009", default),
            (HidingSpotType.Chest, "Almacén/Box060", default),
            (HidingSpotType.Chest, "Oficina/Box015", default),
            (HidingSpotType.SmallCloset, null, new Vector3(12f, 0f, 12f)),
            (HidingSpotType.SmallCloset, null, new Vector3(-1f, 0f, 52f))
        };

        // Furniture aim points stay at a height a standing player can look at.
        private const float FurnitureAimMaximumHeight = 1.2f;

        // NavMesh regions not connected to the spawn (furniture tops, floor outside the walls) are
        // marked not walkable and the NavMesh is baked again, at most this many times.
        private const int UnreachableNavMeshCleanupPasses = 4;
        private const int NotWalkableArea = 1;

        // The front door is the double door on the north wall at z = 57; players escape from just
        // inside it, and an invisible blocker closes the doorway until it opens.
        private static readonly Vector3 FrontDoorPosition = new Vector3(-6.3f, 0f, 55.3f);
        private static readonly Vector3 FrontDoorBlockerLocalPosition = new Vector3(0f, 1.3f, 1.76f);
        private static readonly Vector3 FrontDoorBlockerSize = new Vector3(2.8f, 2.6f, 0.4f);

        [MenuItem("House Escape/Configure Game Local Participants Scene")]        [MenuItem("House Escape/Configure Game Local Participants Scene")]
        public static void ConfigureGameScene()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException($"Required gamepad input actions are missing at {InputActionsPath}.");
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(AvatarPrefabFolder);
            GameObject contextPrefab = CreateOrUpdateContextPrefab(inputActions);
            NetworkParticipantAvatar avatarPrefab = CreateOrUpdateAvatarPrefab();
            RegisterNetworkPrefab(avatarPrefab.gameObject);
            NetworkGenerator generatorPrefab = CreateOrUpdateGeneratorPrefab();
            RegisterNetworkPrefab(generatorPrefab.gameObject);
            NetworkMonster monsterPrefab = CreateOrUpdateMonsterPrefab();
            RegisterNetworkPrefab(monsterPrefab.gameObject);
            NetworkMapItem mapItemPrefab = CreateOrUpdateMapItemPrefab();
            RegisterNetworkPrefab(mapItemPrefab.gameObject);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            RemovePreviousGeneratedRoots(scene);
            GameObject house = PrepareHouse(scene);

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

            NetworkParticipantAvatarDirectory avatarDirectory = CreateAvatarDirectory(
                runtimeRoot.transform,
                networkManager,
                avatarPrefab);
            NetworkParticipantAvatarSpawner avatarSpawner = CreateAvatarSpawner(
                runtimeRoot.transform,
                networkManager,
                registry,
                timeline,
                avatarPrefab,
                avatarDirectory);
            NetworkGenerator[] generators = CreateGenerators(generatorPrefab);
            NetworkParticipantScoreboard scoreboard = CreateScoreboard();
            NetworkGeneratorRace generatorRace = CreateGeneratorRace(registry, timeline, avatarSpawner, generators, scoreboard);
            NetworkMonster monster = CreateMonster(monsterPrefab, timeline, avatarSpawner);
            NetworkParticipantVitality vitality = CreateVitality(timeline, registry, avatarSpawner, monster);
            NetworkParticipantFlashlights flashlights = CreateFlashlights(registry, avatarSpawner, vitality);
            NetworkParticipantInventory inventory = CreateInventory(registry, avatarSpawner, vitality, flashlights);
            NetworkParticipantRevival revival = CreateRevival(registry, avatarSpawner, avatarDirectory, vitality, inventory, scoreboard);
            NetworkHidingPlaces hidingPlaces = CreateHidingPlaces(house, registry, avatarSpawner, vitality, monster, scoreboard);
            LightsOnSurvivalScoring survivalScoring = CreateSurvivalScoring(timeline, vitality, scoreboard);
            FrontDoorEscape frontDoor = CreateFrontDoor(timeline, generatorRace, avatarSpawner, vitality, scoreboard);
            NetworkMapItemField mapItemField = CreateMapItemField(
                networkManager,
                timeline,
                registry,
                avatarSpawner,
                vitality,
                inventory,
                scoreboard,
                mapItemPrefab,
                generators);

            LocalParticipantContext[] contexts = new LocalParticipantContext[4];
            LocalParticipantAvatarDriver[] drivers = new LocalParticipantAvatarDriver[4];
            LocalParticipantHudView[] huds = new LocalParticipantHudView[4];
            GeneratorHudView[] generatorHuds = new GeneratorHudView[4];
            ParticipantVitalityHudView[] vitalityHuds = new ParticipantVitalityHudView[4];
            ItemHudView[] itemHuds = new ItemHudView[4];
            RevivalHudView[] revivalHuds = new RevivalHudView[4];
            FlashlightHudView[] flashlightHuds = new FlashlightHudView[4];
            HidingHudView[] hidingHuds = new HidingHudView[4];
            CreateParticipantContexts(
                runtimeRoot.transform,
                contextPrefab,
                presentation,
                generatorRace,
                vitality,
                inventory,
                mapItemField,
                revival,
                flashlights,
                hidingPlaces,
                avatarDirectory,
                contexts,
                drivers,
                huds,
                generatorHuds,
                vitalityHuds,
                itemHuds,
                revivalHuds,
                flashlightHuds,
                hidingHuds);

            LocalParticipantJoinManager joinManager = runtimeRoot.AddComponent<LocalParticipantJoinManager>();
            joinManager.Initialize(inputActions, contexts);
            LocalParticipantComposition composition = runtimeRoot.AddComponent<LocalParticipantComposition>();
            composition.Initialize(
                networkManager,
                registry,
                timeline,
                joinManager,
                contexts,
                avatarSpawner,
                avatarDirectory,
                vitality,
                drivers,
                huds,
                generatorHuds,
                vitalityHuds,
                itemHuds,
                revivalHuds,
                flashlightHuds,
                hidingHuds,
                joiningCamera,
                joiningCanvas,
                audioListener);
            runtimeRoot.SetActive(true);

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(joinManager);
            EditorUtility.SetDirty(composition);
            EditorUtility.SetDirty(avatarSpawner);
            EditorUtility.SetDirty(avatarDirectory);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(lightRig);
            EditorUtility.SetDirty(generatorRace);
            EditorUtility.SetDirty(monster);
            EditorUtility.SetDirty(vitality);
            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(scoreboard);
            EditorUtility.SetDirty(mapItemField);
            EditorUtility.SetDirty(revival);
            EditorUtility.SetDirty(flashlights);
            EditorUtility.SetDirty(frontDoor);
            EditorUtility.SetDirty(hidingPlaces);
            EditorUtility.SetDirty(survivalScoring);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // In-scene NetworkObject hashes derive from saved scene identifiers, so regenerate
            // them for the new objects after the first save and persist them.
            RefreshSceneNetworkObjectHashes(scene);
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
            NetworkGeneratorRace generatorRace,
            NetworkParticipantVitality vitality,
            NetworkParticipantInventory inventory,
            NetworkMapItemField mapItemField,
            NetworkParticipantRevival revival,
            NetworkParticipantFlashlights flashlights,
            NetworkHidingPlaces hidingPlaces,
            NetworkParticipantAvatarDirectory avatarDirectory,
            LocalParticipantContext[] contexts,
            LocalParticipantAvatarDriver[] drivers,
            LocalParticipantHudView[] huds,
            GeneratorHudView[] generatorHuds,
            ParticipantVitalityHudView[] vitalityHuds,
            ItemHudView[] itemHuds,
            RevivalHudView[] revivalHuds,
            FlashlightHudView[] flashlightHuds,
            HidingHudView[] hidingHuds)
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
                LocalParticipantAvatarDriver driver = instance.AddComponent<LocalParticipantAvatarDriver>();
                driver.Initialize(context);
                LocalParticipantGeneratorDriver generatorDriver = instance.AddComponent<LocalParticipantGeneratorDriver>();
                generatorDriver.Initialize(context, driver, generatorRace);
                LocalParticipantRevivalDriver revivalDriver = instance.AddComponent<LocalParticipantRevivalDriver>();
                revivalDriver.Initialize(context, driver, revival);
                flashlightHuds[slot] = CreateFlashlightHud(canvas.transform, flashlights);
                LocalParticipantFlashlightDriver flashlightDriver = instance.AddComponent<LocalParticipantFlashlightDriver>();
                flashlightDriver.Initialize(context, driver, flashlights, flashlightHuds[slot]);
                hidingHuds[slot] = CreateHidingHud(canvas.transform, hidingPlaces);
                LocalParticipantHidingDriver hidingDriver = instance.AddComponent<LocalParticipantHidingDriver>();
                hidingDriver.Initialize(context, driver, hidingPlaces);
                LocalParticipantInteractionDriver interactionDriver = instance.AddComponent<LocalParticipantInteractionDriver>();
                interactionDriver.Initialize(context, driver, generatorDriver, revivalDriver, flashlightDriver, hidingDriver, mapItemField);
                hud.Prepare(slot);
                canvas.worldCamera = camera;
                CreateTimelineHud(canvas.transform, presentation);
                contexts[slot] = context;
                drivers[slot] = driver;
                huds[slot] = hud;
                generatorHuds[slot] = CreateGeneratorHud(canvas.transform, generatorRace);
                vitalityHuds[slot] = CreateVitalityHud(canvas.transform, vitality);
                LocalParticipantSpectatorDriver spectatorDriver = instance.AddComponent<LocalParticipantSpectatorDriver>();
                spectatorDriver.Initialize(context, driver, vitality, avatarDirectory, vitalityHuds[slot]);
                itemHuds[slot] = CreateItemHud(canvas.transform, inventory);
                revivalHuds[slot] = CreateRevivalHud(canvas.transform, revival);
                LocalParticipantItemDriver itemDriver = instance.AddComponent<LocalParticipantItemDriver>();
                itemDriver.Initialize(context, driver, inventory, itemHuds[slot]);
            }
        }

        private static NetworkParticipantAvatar CreateOrUpdateAvatarPrefab()
        {
            var root = new GameObject("NetworkParticipantAvatar");
            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.Interpolate = true;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Placeholder Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            visual.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f);
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            GameObject anchorObject = CreateRoot("View Anchor", root.transform);
            anchorObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            GameObject beamObject = CreateRoot("Flashlight Beam", anchorObject.transform);
            beamObject.transform.localPosition = new Vector3(0f, -0.1f, 0.1f);
            Light beam = beamObject.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = FlashlightRules.BeamRangeMetres;
            beam.spotAngle = 55f;
            beam.intensity = 6f;
            beam.color = new Color(1f, 0.95f, 0.8f);
            beam.shadows = LightShadows.None;
            beam.enabled = false;
            NetworkParticipantAvatar avatar = root.AddComponent<NetworkParticipantAvatar>();
            avatar.InitializeViewAnchor(anchorObject.transform);
            avatar.InitializeFlashlight(beam);
            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.radius = 0.35f;
            characterController.height = 1.8f;
            characterController.stepOffset = 0.35f;
            characterController.skinWidth = 0.04f;
            // Raise the capsule by the skin width so a grounded avatar rests with its root on the floor.
            characterController.center = new Vector3(0f, 0.9f + characterController.skinWidth, 0f);
            avatar.InitializeCharacterController(characterController);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AvatarPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(AvatarPrefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath);
            NetworkObject savedNetworkObject = prefab.GetComponent<NetworkObject>();
            savedNetworkObject.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(savedNetworkObject);
            PrefabUtility.SavePrefabAsset(prefab);
            return prefab.GetComponent<NetworkParticipantAvatar>();
        }

        private static NetworkGenerator CreateOrUpdateGeneratorPrefab()
        {
            Material inactiveMaterial = CreateOrUpdateMaterial(GeneratorInactiveMaterialPath, new Color(0.75f, 0.08f, 0.05f));
            Material activeMaterial = CreateOrUpdateMaterial(GeneratorActiveMaterialPath, new Color(0.1f, 0.85f, 0.25f));

            var root = new GameObject("NetworkGenerator");
            root.AddComponent<NetworkObject>();

            GameObject body = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "Placeholder Body", root.transform);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(0.9f, 1.1f, 0.7f);

            GameObject inactiveIndicator = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, "Inactive Indicator", root.transform);
            inactiveIndicator.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            inactiveIndicator.transform.localScale = Vector3.one * 0.25f;
            inactiveIndicator.GetComponent<Renderer>().sharedMaterial = inactiveMaterial;

            GameObject activeIndicator = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, "Active Indicator", root.transform);
            activeIndicator.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            activeIndicator.transform.localScale = Vector3.one * 0.25f;
            activeIndicator.GetComponent<Renderer>().sharedMaterial = activeMaterial;

            GameObject aimPoint = CreateRoot("Aim Point", root.transform);
            aimPoint.transform.localPosition = new Vector3(0f, 0.7f, 0f);

            NetworkGenerator generator = root.AddComponent<NetworkGenerator>();
            generator.Initialize(aimPoint.transform, inactiveIndicator, activeIndicator);

            PrefabUtility.SaveAsPrefabAsset(root, GeneratorPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(GeneratorPrefabPath, ImportAssetOptions.ForceUpdate);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorPrefabPath);
            NetworkObject savedNetworkObject = prefab.GetComponent<NetworkObject>();
            savedNetworkObject.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(savedNetworkObject);
            PrefabUtility.SavePrefabAsset(prefab);
            return prefab.GetComponent<NetworkGenerator>();
        }

        private static NetworkGenerator[] CreateGenerators(NetworkGenerator generatorPrefab)
        {
            GameObject root = CreateRoot(GeneratorsRootName, null);
            var generators = new NetworkGenerator[GeneratorRace.TotalGeneratorCount];
            for (int index = 0; index < generators.Length; index++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(generatorPrefab.gameObject);
                instance.name = $"Generator {index + 1}";
                instance.transform.SetParent(root.transform, false);
                instance.transform.position = SnapToHouseFloor(GeneratorPositions[index], $"generator {index + 1}");
                NetworkGenerator generator = instance.GetComponent<NetworkGenerator>();
                generator.SetGeneratorNumber(index + 1);
                EditorUtility.SetDirty(generator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(generator);
                generators[index] = generator;
            }

            return generators;
        }

        private static NetworkGeneratorRace CreateGeneratorRace(
            NetworkParticipantRegistry registry,
            NetworkMatchTimeline timeline,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkGenerator[] generators,
            NetworkParticipantScoreboard scoreboard)
        {
            GameObject gameObject = CreateRoot(GeneratorRaceRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkGeneratorRace race = gameObject.AddComponent<NetworkGeneratorRace>();
            race.Initialize(registry, timeline, avatarSpawner, generators, scoreboard);
            return race;
        }

        private static NetworkParticipantRevival CreateRevival(
            NetworkParticipantRegistry registry,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkParticipantAvatarDirectory avatarDirectory,
            NetworkParticipantVitality vitality,
            NetworkParticipantInventory inventory,
            NetworkParticipantScoreboard scoreboard)
        {
            GameObject gameObject = CreateRoot(RevivalRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkParticipantRevival revival = gameObject.AddComponent<NetworkParticipantRevival>();
            revival.Initialize(registry, avatarSpawner, avatarDirectory, vitality, inventory, scoreboard);
            return revival;
        }

        private static RevivalHudView CreateRevivalHud(Transform canvas, NetworkParticipantRevival revival)
        {
            TMP_Text text = CreateText("Revival", canvas, new Vector2(0f, -70f), 28f, new Color(0.45f, 1f, 0.55f));
            RectTransform rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 48f);
            text.gameObject.SetActive(false);

            GameObject holder = CreateUiObject("Revival Panel", canvas);
            RevivalHudView view = holder.AddComponent<RevivalHudView>();
            view.Initialize(revival, text);
            return view;
        }

        private static NetworkHidingPlaces CreateHidingPlaces(
            GameObject house,
            NetworkParticipantRegistry registry,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkParticipantVitality vitality,
            NetworkMonster monster,
            NetworkParticipantScoreboard scoreboard)
        {
            Material material = CreateOrUpdateMaterial(HidingSpotMaterialPath, new Color(0.45f, 0.3f, 0.18f));
            GameObject spotsRoot = CreateRoot(HidingSpotsRootName, null);
            var spots = new HidingSpot[HidingSpotLayout.Length];
            for (int index = 0; index < spots.Length; index++)
            {
                (HidingSpotType type, string furniture, Vector3 position) = HidingSpotLayout[index];
                GameObject spotObject = CreateRoot($"{type} {index + 1}", spotsRoot.transform);
                GameObject aimPoint = CreateRoot("Aim Point", spotObject.transform);
                if (furniture != null)
                {
                    Transform piece = house.transform.Find(furniture);
                    Renderer pieceRenderer = piece != null ? piece.GetComponent<Renderer>() : null;
                    if (pieceRenderer == null)
                    {
                        throw new InvalidOperationException($"Hiding place furniture '{furniture}' is missing from the house model.");
                    }

                    // The player stands on the nearest floor beside the furniture and aims at it.
                    Bounds bounds = pieceRenderer.bounds;
                    spotObject.name = $"{type} {index + 1} ({piece.name})";
                    spotObject.transform.position = SnapToHouseFloor(new Vector3(bounds.center.x, 0f, bounds.center.z), $"hiding place {furniture}");
                    aimPoint.transform.position = new Vector3(
                        bounds.center.x,
                        Mathf.Min(bounds.center.y, FurnitureAimMaximumHeight),
                        bounds.center.z);
                }
                else
                {
                    spotObject.transform.position = SnapToHouseFloor(position, $"hiding place {index + 1}");
                    var size = new Vector3(0.8f, 1.8f, 0.7f);
                    GameObject body = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "Placeholder Body", spotObject.transform);
                    body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
                    body.transform.localScale = size;
                    body.GetComponent<Renderer>().sharedMaterial = material;
                    aimPoint.transform.localPosition = new Vector3(0f, 1f, 0f);
                }

                HidingSpot spot = spotObject.AddComponent<HidingSpot>();
                spot.Initialize(type, aimPoint.transform);
                spots[index] = spot;
            }

            GameObject gameObject = CreateRoot(HidingPlacesRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkHidingPlaces hidingPlaces = gameObject.AddComponent<NetworkHidingPlaces>();
            hidingPlaces.Initialize(registry, avatarSpawner, vitality, monster, scoreboard, spots);
            return hidingPlaces;
        }

        private static LightsOnSurvivalScoring CreateSurvivalScoring(
            NetworkMatchTimeline timeline,
            NetworkParticipantVitality vitality,
            NetworkParticipantScoreboard scoreboard)
        {
            GameObject gameObject = CreateRoot(SurvivalScoringRootName, null);
            LightsOnSurvivalScoring scoring = gameObject.AddComponent<LightsOnSurvivalScoring>();
            scoring.Initialize(timeline, vitality, scoreboard);
            return scoring;
        }

        private static HidingHudView CreateHidingHud(Transform canvas, NetworkHidingPlaces hidingPlaces)
        {
            TMP_Text text = CreateText("Hiding", canvas, new Vector2(0f, -120f), 26f, new Color(0.75f, 0.85f, 1f));
            RectTransform rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 44f);
            text.gameObject.SetActive(false);

            GameObject holder = CreateUiObject("Hiding Panel", canvas);
            HidingHudView view = holder.AddComponent<HidingHudView>();
            view.Initialize(hidingPlaces, text);
            return view;
        }

        private static FrontDoorEscape CreateFrontDoor(
            NetworkMatchTimeline timeline,
            NetworkGeneratorRace generatorRace,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkParticipantVitality vitality,
            NetworkParticipantScoreboard scoreboard)
        {
            Material openMaterial = CreateOrUpdateMaterial(GeneratorActiveMaterialPath, new Color(0.1f, 0.85f, 0.25f));
            GameObject root = CreateRoot(FrontDoorRootName, null);
            root.transform.position = SnapToHouseFloor(FrontDoorPosition, "front door exit");

            // The model's door stays in place; this invisible blocker closes the doorway.
            GameObject closed = CreateRoot("Closed Door Blocker", root.transform);
            closed.transform.localPosition = FrontDoorBlockerLocalPosition;
            closed.AddComponent<BoxCollider>().size = FrontDoorBlockerSize;

            GameObject open = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "Open Door Light", root.transform);
            open.transform.localPosition = FrontDoorBlockerLocalPosition + new Vector3(0f, 1.5f, -0.3f);
            open.transform.localScale = new Vector3(2.4f, 0.12f, 0.12f);
            open.GetComponent<Renderer>().sharedMaterial = openMaterial;
            open.SetActive(false);

            GameObject exitPoint = CreateRoot("Exit Point", root.transform);
            FrontDoorView view = root.AddComponent<FrontDoorView>();
            view.Initialize(generatorRace, closed, open);
            FrontDoorEscape escape = root.AddComponent<FrontDoorEscape>();
            escape.Initialize(exitPoint.transform, timeline, generatorRace, avatarSpawner, vitality, scoreboard);
            EditorUtility.SetDirty(view);
            return escape;
        }

        private static NetworkParticipantScoreboard CreateScoreboard()
        {
            GameObject gameObject = CreateRoot(ScoreboardRootName, null);
            gameObject.AddComponent<NetworkObject>();
            return gameObject.AddComponent<NetworkParticipantScoreboard>();
        }

        private static NetworkMapItem CreateOrUpdateMapItemPrefab()
        {
            Material material = CreateOrUpdateMaterial(MapItemMaterialPath, Color.white);

            var root = new GameObject("NetworkMapItem");
            root.AddComponent<NetworkObject>();

            GameObject visual = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, "Placeholder Visual", root.transform);
            visual.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            visual.transform.localScale = Vector3.one * 0.3f;
            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            GameObject aimPoint = CreateRoot("Aim Point", root.transform);
            aimPoint.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            NetworkMapItem item = root.AddComponent<NetworkMapItem>();
            item.Initialize(aimPoint.transform, renderer);

            PrefabUtility.SaveAsPrefabAsset(root, MapItemPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(MapItemPrefabPath, ImportAssetOptions.ForceUpdate);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapItemPrefabPath);
            NetworkObject savedNetworkObject = prefab.GetComponent<NetworkObject>();
            savedNetworkObject.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(savedNetworkObject);
            PrefabUtility.SavePrefabAsset(prefab);
            return prefab.GetComponent<NetworkMapItem>();
        }

        private static NetworkMapItemField CreateMapItemField(
            NetworkManager networkManager,
            NetworkMatchTimeline timeline,
            NetworkParticipantRegistry registry,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkParticipantVitality vitality,
            NetworkParticipantInventory inventory,
            NetworkParticipantScoreboard scoreboard,
            NetworkMapItem mapItemPrefab,
            NetworkGenerator[] generators)
        {
            GameObject positionsRoot = CreateRoot(MapItemPositionsRootName, null);
            var nearPositions = new List<Vector3>();
            for (int generator = 0; generator < generators.Length; generator++)
            {
                int found = 0;
                Vector3 origin = generators[generator].transform.position;
                for (int offset = 0; offset < NearGeneratorItemOffsets.Length && found < NearGeneratorItemPositionsPerGenerator; offset++)
                {
                    if (TrySnapReachable(origin + NearGeneratorItemOffsets[offset], 0.5f, out Vector3 position))
                    {
                        nearPositions.Add(position);
                        found++;
                    }
                }

                if (found < NearGeneratorItemPositionsPerGenerator)
                {
                    throw new InvalidOperationException($"Generator {generator + 1} has no free floor beside it for items.");
                }
            }

            var generalPositions = new List<Vector3>();
            Vector3 min = HouseInteriorCenter - HouseInteriorSize * 0.5f;
            Vector3 max = HouseInteriorCenter + HouseInteriorSize * 0.5f;
            for (float z = min.z + 1f; z < max.z - 1f && generalPositions.Count < GeneralItemMaximumCount; z += GeneralItemGridStep)
            {
                for (float x = min.x + 1f; x < max.x - 1f && generalPositions.Count < GeneralItemMaximumCount; x += GeneralItemGridStep)
                {
                    if (!TrySnapReachable(new Vector3(x, 0f, z), 0.4f, out Vector3 position)
                        || Flat(position - SpawnCenter).magnitude < GeneralItemSpawnClearance
                        || IsNear(position, nearPositions, GeneralItemMinimumSpacing)
                        || IsNear(position, generalPositions, GeneralItemMinimumSpacing))
                    {
                        continue;
                    }

                    generalPositions.Add(position);
                }
            }

            var near = new Transform[nearPositions.Count];
            for (int index = 0; index < near.Length; index++)
            {
                GameObject point = CreateRoot($"Near Generator {index / NearGeneratorItemPositionsPerGenerator + 1} Item {index % NearGeneratorItemPositionsPerGenerator + 1}", positionsRoot.transform);
                point.transform.position = nearPositions[index];
                near[index] = point.transform;
            }

            var general = new Transform[generalPositions.Count];
            for (int index = 0; index < general.Length; index++)
            {
                GameObject point = CreateRoot($"Item Position {index + 1}", positionsRoot.transform);
                point.transform.position = generalPositions[index];
                general[index] = point.transform;
            }

            GameObject gameObject = CreateRoot(MapItemFieldRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkMapItemField field = gameObject.AddComponent<NetworkMapItemField>();
            field.Initialize(
                networkManager,
                timeline,
                registry,
                avatarSpawner,
                vitality,
                inventory,
                scoreboard,
                mapItemPrefab,
                near,
                general);
            return field;
        }

        private static NetworkMonster CreateOrUpdateMonsterPrefab()
        {
            Material material = CreateOrUpdateMaterial(MonsterMaterialPath, new Color(0.12f, 0.02f, 0.03f));

            var root = new GameObject("NetworkMonster");
            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            networkTransform.Interpolate = true;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.2f;
            agent.baseOffset = 0f;
            agent.acceleration = 20f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = NetworkMonster.StoppingDistanceMetres;
            agent.enabled = false;

            GameObject body = CreatePrimitiveWithoutCollider(PrimitiveType.Capsule, "Placeholder Visual", root.transform);
            body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1.1f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = material;

            NetworkMonster monster = root.AddComponent<NetworkMonster>();
            monster.InitializeAgent(agent);

            PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(MonsterPrefabPath, ImportAssetOptions.ForceUpdate);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
            NetworkObject savedNetworkObject = prefab.GetComponent<NetworkObject>();
            savedNetworkObject.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(savedNetworkObject);
            PrefabUtility.SavePrefabAsset(prefab);
            return prefab.GetComponent<NetworkMonster>();
        }

        /// <summary>
        /// Adds mesh colliders to the house model (except walk-through doors, the ceiling and
        /// decorations) and bakes the monster NavMesh over the interior from physics colliders.
        /// </summary>
        private static GameObject PrepareHouse(Scene scene)
        {
            GameObject house = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == HouseRootName)
                {
                    house = root;
                }
            }

            if (house == null)
            {
                throw new InvalidOperationException($"The house model '{HouseRootName}' must be in {ScenePath}.");
            }

            foreach (Transform group in house.transform)
            {
                if (Array.IndexOf(HouseGroupsWithoutColliders, group.name) >= 0)
                {
                    continue;
                }

                if (Array.IndexOf(HouseWalkableGroups, group.name) < 0)
                {
                    NavMeshModifier modifier = group.GetComponent<NavMeshModifier>();
                    if (modifier == null)
                    {
                        modifier = group.gameObject.AddComponent<NavMeshModifier>();
                    }

                    modifier.overrideArea = true;
                    modifier.area = NotWalkableArea;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
                }

                foreach (MeshFilter meshFilter in group.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter.sharedMesh == null
                        || meshFilter.name == HouseCeilingName
                        || meshFilter.GetComponent<Collider>() != null)
                    {
                        continue;
                    }

                    MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = meshFilter.sharedMesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
            }

            GameObject navMeshRoot = CreateRoot(HouseNavMeshRootName, null);
            NavMeshSurface surface = navMeshRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = HouseInteriorCenter;
            surface.size = HouseInteriorSize;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                throw new InvalidOperationException("Baking the house NavMesh produced no data.");
            }

            RemoveUnreachableNavMesh(surface);

            EnsureFolder(NavMeshFolder);
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath) != null)
            {
                AssetDatabase.DeleteAsset(NavMeshDataPath);
            }

            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
            EditorUtility.SetDirty(surface);
            EditorUtility.SetDirty(house);

            if (!NavMesh.SamplePosition(SpawnCenter, out _, 1f, NavMesh.AllAreas))
            {
                throw new InvalidOperationException("The main room spawn is not on the house NavMesh.");
            }

            return house;
        }

        /// <summary>
        /// Marks every NavMesh region that is not connected to the spawn (tops of furniture and
        /// walls, floor strips outside the outer walls) as not walkable and bakes again. A region
        /// whose bounds hold no reachable NavMesh is covered by one box; otherwise each of its
        /// triangles gets its own box so the reachable floor is never touched.
        /// </summary>
        private static void RemoveUnreachableNavMesh(NavMeshSurface surface)
        {
            if (!NavMesh.SamplePosition(SpawnCenter, out NavMeshHit spawn, 1f, NavMesh.AllAreas))
            {
                throw new InvalidOperationException("The main room spawn is not on the house NavMesh.");
            }

            GameObject blockers = CreateRoot("Unreachable Regions", surface.transform);
            for (int pass = 0; pass < UnreachableNavMeshCleanupPasses; pass++)
            {
                NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
                int[] component = LabelNavMeshComponents(triangulation);
                int reachable = component[NearestVertex(triangulation.vertices, spawn.position)];
                var reachableVertices = new List<Vector3>();
                var regions = new Dictionary<int, List<int>>();
                for (int triangle = 0; triangle < triangulation.indices.Length / 3; triangle++)
                {
                    int label = component[triangulation.indices[triangle * 3]];
                    if (label == reachable)
                    {
                        for (int corner = 0; corner < 3; corner++)
                        {
                            reachableVertices.Add(triangulation.vertices[triangulation.indices[triangle * 3 + corner]]);
                        }

                        continue;
                    }

                    if (!regions.TryGetValue(label, out List<int> triangles))
                    {
                        triangles = new List<int>();
                        regions.Add(label, triangles);
                    }

                    triangles.Add(triangle);
                }

                if (regions.Count == 0)
                {
                    return;
                }

                foreach (List<int> triangles in regions.Values)
                {
                    Bounds region = TriangleBounds(triangulation, triangles[0]);
                    for (int index = 1; index < triangles.Count; index++)
                    {
                        region.Encapsulate(TriangleBounds(triangulation, triangles[index]));
                    }

                    if (!ContainsAny(region, reachableVertices))
                    {
                        AddNotWalkableBox(blockers.transform, region);
                        continue;
                    }

                    for (int index = 0; index < triangles.Count; index++)
                    {
                        AddNotWalkableBox(blockers.transform, TriangleBounds(triangulation, triangles[index]));
                    }
                }

                surface.BuildNavMesh();
            }

            throw new InvalidOperationException("Unreachable NavMesh regions remain after cleanup.");
        }

        /// <summary>Connected components of the NavMesh triangles, joining vertices at the same position.</summary>
        private static int[] LabelNavMeshComponents(NavMeshTriangulation triangulation)
        {
            int[] parent = new int[triangulation.vertices.Length];
            for (int index = 0; index < parent.Length; index++)
            {
                parent[index] = index;
            }

            int Find(int vertex)
            {
                while (parent[vertex] != vertex)
                {
                    parent[vertex] = parent[parent[vertex]];
                    vertex = parent[vertex];
                }

                return vertex;
            }

            void Union(int first, int second)
            {
                parent[Find(first)] = Find(second);
            }

            for (int index = 0; index < triangulation.indices.Length; index += 3)
            {
                Union(triangulation.indices[index], triangulation.indices[index + 1]);
                Union(triangulation.indices[index], triangulation.indices[index + 2]);
            }

            var byPosition = new Dictionary<Vector3Int, int>();
            for (int index = 0; index < triangulation.vertices.Length; index++)
            {
                Vector3 vertex = triangulation.vertices[index];
                var key = new Vector3Int(Mathf.RoundToInt(vertex.x * 100f), Mathf.RoundToInt(vertex.y * 100f), Mathf.RoundToInt(vertex.z * 100f));
                if (byPosition.TryGetValue(key, out int existing))
                {
                    Union(index, existing);
                }
                else
                {
                    byPosition.Add(key, index);
                }
            }

            int[] labels = new int[parent.Length];
            for (int index = 0; index < labels.Length; index++)
            {
                labels[index] = Find(index);
            }

            return labels;
        }

        private static int NearestVertex(Vector3[] vertices, Vector3 position)
        {
            int nearest = 0;
            float best = float.PositiveInfinity;
            for (int index = 0; index < vertices.Length; index++)
            {
                float distance = (vertices[index] - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = index;
                }
            }

            return nearest;
        }

        private static Bounds TriangleBounds(NavMeshTriangulation triangulation, int triangle)
        {
            var bounds = new Bounds(triangulation.vertices[triangulation.indices[triangle * 3]], Vector3.zero);
            bounds.Encapsulate(triangulation.vertices[triangulation.indices[triangle * 3 + 1]]);
            bounds.Encapsulate(triangulation.vertices[triangulation.indices[triangle * 3 + 2]]);
            return bounds;
        }

        private static bool ContainsAny(Bounds region, List<Vector3> points)
        {
            Bounds expanded = region;
            expanded.Expand(new Vector3(0.2f, 1f, 0.2f));
            for (int index = 0; index < points.Count; index++)
            {
                if (expanded.Contains(points[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddNotWalkableBox(Transform parent, Bounds region)
        {
            GameObject blocker = CreateRoot("Not Walkable", parent);
            blocker.transform.position = region.center;
            NavMeshModifierVolume volume = blocker.AddComponent<NavMeshModifierVolume>();
            volume.center = Vector3.zero;
            volume.size = new Vector3(region.size.x + 0.2f, region.size.y + 0.6f, region.size.z + 0.2f);
            volume.area = NotWalkableArea;
        }

        /// <summary>The closest walkable floor to <paramref name="position"/>, reachable from the spawn.</summary>
        private static Vector3 SnapToHouseFloor(Vector3 position, string label)
        {
            if (!TrySnapReachable(position, 0f, out Vector3 snapped, 4f))
            {
                throw new InvalidOperationException($"The {label} at {position} is not on reachable house floor.");
            }

            return snapped;
        }

        private static bool TrySnapReachable(Vector3 position, float minimumEdgeDistance, out Vector3 snapped, float searchRadius = 1f)
        {
            snapped = position;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                return false;
            }

            if (minimumEdgeDistance > 0f
                && (!NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance < minimumEdgeDistance))
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(SpawnCenter, out NavMeshHit spawn, 1f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(spawn.position, hit.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            snapped = new Vector3(hit.position.x, 0f, hit.position.z);
            return true;
        }

        private static bool IsNear(Vector3 position, List<Vector3> others, float distance)
        {
            for (int index = 0; index < others.Count; index++)
            {
                if (Flat(others[index] - position).magnitude < distance)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 Flat(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private static NetworkMonster CreateMonster(
            NetworkMonster monsterPrefab,
            NetworkMatchTimeline timeline,
            NetworkParticipantAvatarSpawner avatarSpawner)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(monsterPrefab.gameObject);
            instance.name = MonsterRootName;
            instance.transform.position = SnapToHouseFloor(MonsterSpawnPosition, "monster spawn");
            NetworkMonster monster = instance.GetComponent<NetworkMonster>();
            monster.Initialize(timeline, avatarSpawner, instance.GetComponent<NavMeshAgent>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(monster);
            return monster;
        }

        private static NetworkParticipantVitality CreateVitality(
            NetworkMatchTimeline timeline,
            NetworkParticipantRegistry registry,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkMonster monster)
        {
            GameObject gameObject = CreateRoot(VitalityRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkParticipantVitality vitality = gameObject.AddComponent<NetworkParticipantVitality>();
            vitality.Initialize(timeline, registry, avatarSpawner, monster);
            return vitality;
        }

        private static NetworkParticipantInventory CreateInventory(
            NetworkParticipantRegistry registry,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkParticipantVitality vitality,
            NetworkParticipantFlashlights flashlights)
        {
            GameObject gameObject = CreateRoot(InventoryRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkParticipantInventory inventory = gameObject.AddComponent<NetworkParticipantInventory>();
            inventory.Initialize(registry, avatarSpawner, vitality, flashlights);
            return inventory;
        }

        private static NetworkParticipantFlashlights CreateFlashlights(
            NetworkParticipantRegistry registry,
            NetworkParticipantAvatarSpawner avatarSpawner,
            NetworkParticipantVitality vitality)
        {
            Material material = CreateOrUpdateMaterial(ChargingStationMaterialPath, new Color(0.2f, 0.6f, 0.95f));
            GameObject stationsRoot = CreateRoot(ChargingStationsRootName, null);
            var stations = new ChargingStation[ChargingStationPositions.Length];
            for (int index = 0; index < stations.Length; index++)
            {
                GameObject stationObject = CreateRoot($"Charging Station {index + 1}", stationsRoot.transform);
                stationObject.transform.position = SnapToHouseFloor(ChargingStationPositions[index], $"charging station {index + 1}");
                GameObject body = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "Placeholder Body", stationObject.transform);
                body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                body.transform.localScale = new Vector3(0.5f, 1.2f, 0.3f);
                body.GetComponent<Renderer>().sharedMaterial = material;
                GameObject aimPoint = CreateRoot("Aim Point", stationObject.transform);
                aimPoint.transform.localPosition = new Vector3(0f, 1f, 0f);
                ChargingStation station = stationObject.AddComponent<ChargingStation>();
                station.Initialize(aimPoint.transform);
                stations[index] = station;
            }

            GameObject gameObject = CreateRoot(FlashlightsRootName, null);
            gameObject.AddComponent<NetworkObject>();
            NetworkParticipantFlashlights flashlights = gameObject.AddComponent<NetworkParticipantFlashlights>();
            flashlights.Initialize(registry, avatarSpawner, vitality, stations);
            return flashlights;
        }

        private static FlashlightHudView CreateFlashlightHud(Transform canvas, NetworkParticipantFlashlights flashlights)
        {
            GameObject panel = CreateUiObject("Flashlight Panel", canvas);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-20f, 64f);
            panelRect.sizeDelta = new Vector2(420f, 44f);

            TMP_Text battery = CreateText("Flashlight Battery", panel.transform, new Vector2(0f, -8f), 24f, new Color(1f, 0.92f, 0.5f));
            battery.alignment = TextAlignmentOptions.Right;
            battery.gameObject.SetActive(false);

            FlashlightHudView view = panel.AddComponent<FlashlightHudView>();
            view.Initialize(flashlights, battery);
            return view;
        }

        private static ItemHudView CreateItemHud(Transform canvas, NetworkParticipantInventory inventory)
        {
            GameObject panel = CreateUiObject("Item Panel", canvas);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-20f, 20f);
            panelRect.sizeDelta = new Vector2(420f, 48f);

            TMP_Text selection = CreateText("Selected Item", panel.transform, new Vector2(0f, -8f), 26f, Color.white);
            selection.alignment = TextAlignmentOptions.Right;
            selection.gameObject.SetActive(false);

            ItemHudView view = panel.AddComponent<ItemHudView>();
            view.Initialize(inventory, selection);
            return view;
        }

        private static ParticipantVitalityHudView CreateVitalityHud(Transform canvas, NetworkParticipantVitality vitality)
        {
            GameObject panel = CreateUiObject("Vitality Panel", canvas);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20f, -76f);
            panelRect.sizeDelta = new Vector2(420f, 130f);

            TMP_Text health = CreateText("Health", panel.transform, new Vector2(0f, -4f), 26f, Color.white);
            health.alignment = TextAlignmentOptions.Left;
            TMP_Text energy = CreateText("Energy", panel.transform, new Vector2(0f, -44f), 24f, new Color(0.55f, 0.85f, 1f));
            energy.alignment = TextAlignmentOptions.Left;
            TMP_Text status = CreateText("Condition", panel.transform, new Vector2(0f, -84f), 24f, new Color(1f, 0.45f, 0.35f));
            status.alignment = TextAlignmentOptions.Left;
            status.gameObject.SetActive(false);

            TMP_Text outcome = CreateText("Match Outcome", canvas, Vector2.zero, 40f, new Color(1f, 0.3f, 0.25f));
            RectTransform outcomeRect = (RectTransform)outcome.transform;
            outcomeRect.anchorMin = outcomeRect.anchorMax = new Vector2(0.5f, 0.5f);
            outcomeRect.pivot = new Vector2(0.5f, 0.5f);
            outcomeRect.sizeDelta = new Vector2(820f, 64f);
            outcome.gameObject.SetActive(false);

            ParticipantVitalityHudView view = panel.AddComponent<ParticipantVitalityHudView>();
            view.Initialize(vitality, health, energy, status, outcome);
            return view;
        }

        private static GeneratorHudView CreateGeneratorHud(Transform canvas, NetworkGeneratorRace generatorRace)
        {
            GameObject panel = CreateUiObject("Generator Panel", canvas);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(20f, 20f);
            panelRect.sizeDelta = new Vector2(420f, 96f);
            panel.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.88f);

            TMP_Text count = CreateText("Generator Count", panel.transform, new Vector2(0f, -8f), 26f, Color.white);
            count.text = GeneratorHudText.FormatProgress(0);
            TMP_Text attempt = CreateText("Generator Attempt", panel.transform, new Vector2(0f, -50f), 22f, new Color(1f, 0.75f, 0.25f));
            attempt.gameObject.SetActive(false);
            GeneratorHudView view = panel.AddComponent<GeneratorHudView>();
            view.Initialize(generatorRace, count, attempt);
            return view;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException("The URP Lit shader is required for generator placeholder materials.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrimitiveWithoutCollider(PrimitiveType type, string name, Transform parent)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return primitive;
        }

        private static void RefreshSceneNetworkObjectHashes(Scene scene)
        {
            // SendMessage on scene objects in edit mode asserts (ShouldRunBehaviour), so invoke
            // NGO's internal validation hook directly.
            System.Reflection.MethodInfo validate = typeof(NetworkObject).GetMethod(
                "OnValidate",
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public);
            if (validate == null)
            {
                throw new InvalidOperationException("NetworkObject.OnValidate is unavailable; scene hashes cannot be refreshed.");
            }

            foreach (NetworkObject networkObject in CollectSceneComponents<NetworkObject>(scene))
            {
                validate.Invoke(networkObject, null);
                EditorUtility.SetDirty(networkObject);
            }
        }

        private static NetworkParticipantAvatarSpawner CreateAvatarSpawner(
            Transform parent,
            NetworkManager networkManager,
            NetworkParticipantRegistry registry,
            NetworkMatchTimeline timeline,
            NetworkParticipantAvatar avatarPrefab,
            NetworkParticipantAvatarDirectory avatarDirectory)
        {
            GameObject gameObject = CreateRoot("Participant Avatar Spawner", parent);
            var spawnPoints = new Transform[4];
            Vector3[] positions =
            {
                SpawnCenter + new Vector3(-1.5f, 0f, -1.5f),
                SpawnCenter + new Vector3(1.5f, 0f, -1.5f),
                SpawnCenter + new Vector3(-1.5f, 0f, 1.5f),
                SpawnCenter + new Vector3(1.5f, 0f, 1.5f)
            };
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                GameObject spawnPoint = CreateRoot($"Participant Spawn {index + 1}", gameObject.transform);
                spawnPoint.transform.position = positions[index];
                spawnPoints[index] = spawnPoint.transform;
            }

            NetworkParticipantAvatarSpawner spawner = gameObject.AddComponent<NetworkParticipantAvatarSpawner>();
            spawner.Initialize(networkManager, registry, timeline, avatarPrefab, spawnPoints, avatarDirectory);
            return spawner;
        }

        private static NetworkParticipantAvatarDirectory CreateAvatarDirectory(
            Transform parent,
            NetworkManager networkManager,
            NetworkParticipantAvatar avatarPrefab)
        {
            GameObject gameObject = CreateRoot("Participant Avatar Directory", parent);
            NetworkParticipantAvatarDirectory directory = gameObject.AddComponent<NetworkParticipantAvatarDirectory>();
            directory.Initialize(networkManager, avatarPrefab);
            return directory;
        }

        private static void RegisterNetworkPrefab(GameObject avatarPrefab)
        {
            NetworkPrefabsList prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (prefabs == null)
            {
                throw new InvalidOperationException($"Required network prefab list is missing at {NetworkPrefabsPath}.");
            }

            if (prefabs.Contains(avatarPrefab))
            {
                return;
            }

            prefabs.Add(new NetworkPrefab
            {
                Override = NetworkPrefabOverride.None,
                Prefab = avatarPrefab
            });
            EditorUtility.SetDirty(prefabs);
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
            camera.fieldOfView = EnergyCameraEffects.NormalFieldOfView;
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
                    || root.name == RegistryRootName
                    || root.name == GeneratorRaceRootName
                    || root.name == GeneratorsRootName
                    || root.name == MonsterRootName
                    || root.name == VitalityRootName
                    || root.name == InventoryRootName
                    || root.name == ScoreboardRootName
                    || root.name == RevivalRootName
                    || root.name == FlashlightsRootName
                    || root.name == FrontDoorRootName
                    || root.name == HidingPlacesRootName
                    || root.name == HidingSpotsRootName
                    || root.name == SurvivalScoringRootName
                    || root.name == ChargingStationsRootName
                    || root.name == MapItemFieldRootName
                    || root.name == MapItemPositionsRootName
                    || root.name == MonsterArenaRootName
                    || root.name == HouseNavMeshRootName)
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
