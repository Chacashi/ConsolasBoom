using System.Linq;
using HouseEscape.Infrastructure;
using HouseEscape.Networking.Avatars;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class AvatarSerializedIntegrationTests
    {
        [Test]
        public void AvatarPrefabIsServerReplicatedAndRegistered()
        {
            const string prefabPath = "Assets/HouseEscape/Networking/Prefabs/NetworkParticipantAvatar.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            var serializedNetworkObject = new SerializedObject(prefab.GetComponent<NetworkObject>());
            Assert.That(serializedNetworkObject.FindProperty("GlobalObjectIdHash").uintValue, Is.Not.Zero);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkParticipantAvatar>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkParticipantAvatar>().ViewAnchor, Is.Not.Null);
            CharacterController characterController = prefab.GetComponent<CharacterController>();
            Assert.That(characterController, Is.Not.Null, "The server moves the avatar through a character controller.");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.EqualTo(new Collider[] { characterController }),
                "The character controller is the avatar's only collider.");
            Assert.That(new SerializedObject(prefab.GetComponent<NetworkParticipantAvatar>()).FindProperty("characterController").objectReferenceValue,
                Is.SameAs(characterController));

            NetworkPrefabsList prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            var serializedPrefabs = new SerializedObject(prefabs);
            SerializedProperty list = serializedPrefabs.FindProperty("List");
            bool registered = false;
            for (int index = 0; index < list.arraySize; index++)
            {
                registered |= list.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("Prefab").objectReferenceValue == prefab;
            }

            Assert.That(registered, Is.True);
        }

        [Test]
        public void GameSceneHasExplicitSpawnerDriversAndNoPlayerPrefabOrDuplicateListeners()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            NetworkManager manager = Collect<NetworkManager>(scene).Single();
            Assert.That(manager.NetworkConfig.PlayerPrefab, Is.Null);
            Assert.That(Collect<NetworkParticipantAvatarSpawner>(scene), Has.Length.EqualTo(1));
            Assert.That(Collect<LocalParticipantAvatarDriver>(scene), Has.Length.EqualTo(4));
            Assert.That(Collect<AudioListener>(scene).Count(listener => listener.enabled), Is.EqualTo(1));

            var spawner = new SerializedObject(Collect<NetworkParticipantAvatarSpawner>(scene).Single());
            Assert.That(spawner.FindProperty("avatarPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(spawner.FindProperty("spawnPoints").arraySize, Is.EqualTo(4));
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
