using UnityEngine;
using System;
using System.Collections.Generic;
using HouseEscape.Player;

namespace HouseEscape.Systems
{
    public class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            Waiting,
            Playing,
            Paused,
            Victory,
            Defeat
        }

        [Header("Game Settings")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private float victoryTimeLimit = 3600f;

        [Header("References")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private GeneratorManager generatorManager;

        private static GameManager _instance;
        private GameState _currentState = GameState.Waiting;
        private List<Transform> _activePlayers = new List<Transform>();
        private float _gameStartTime;

        public event Action<GameState> OnGameStateChanged;
        public event Action OnVictory;
        public event Action OnDefeat;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<GameManager>();
                return _instance;
            }
        }

        public GameState CurrentState => _currentState;
        public int ActivePlayersCount => _activePlayers.Count;
        public float GameTime => Time.time - _gameStartTime;
        public bool IsPlaying => _currentState == GameState.Playing;
        public bool IsPaused => _currentState == GameState.Paused;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _gameStartTime = Time.time;
        }

        public void StartGame()
        {
            if (_currentState != GameState.Waiting)
                return;

            _currentState = GameState.Playing;
            _gameStartTime = Time.time;
            OnGameStateChanged?.Invoke(_currentState);

            if (gameTimer != null)
                gameTimer.StartTimer();

            Debug.Log("[GameManager] Game started with " + _activePlayers.Count + " players");
        }

        public void PauseGame()
        {
            if (_currentState != GameState.Playing)
                return;

            _currentState = GameState.Paused;
            Time.timeScale = 0f;
            OnGameStateChanged?.Invoke(_currentState);
        }

        public void ResumeGame()
        {
            if (_currentState != GameState.Paused)
                return;

            _currentState = GameState.Playing;
            Time.timeScale = 1f;
            OnGameStateChanged?.Invoke(_currentState);
        }

        public void EndGame(bool victory)
        {
            if (_currentState != GameState.Playing && _currentState != GameState.Paused)
                return;

            if (victory)
            {
                _currentState = GameState.Victory;
                OnVictory?.Invoke();
                Debug.Log("[GameManager] VICTORY!");
            }
            else
            {
                _currentState = GameState.Defeat;
                OnDefeat?.Invoke();
                Debug.Log("[GameManager] DEFEAT!");
            }

            OnGameStateChanged?.Invoke(_currentState);
        }

        public void RegisterPlayer(Transform player)
        {
            if (!_activePlayers.Contains(player))
            {
                _activePlayers.Add(player);
                Debug.Log("[GameManager] Player registered: " + player.name);
            }
        }

        public void UnregisterPlayer(Transform player)
        {
            if (_activePlayers.Contains(player))
            {
                _activePlayers.Remove(player);
                Debug.Log("[GameManager] Player unregistered: " + player.name);
            }
        }

        public void SpawnPlayer()
        {
            if (playerPrefab == null || playerSpawnPoint == null)
            {
                Debug.LogError("[GameManager] Player prefab or spawn point not assigned");
                return;
            }

            Vector3 spawnPosition = playerSpawnPoint.position;
            Quaternion spawnRotation = playerSpawnPoint.rotation;

            GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            Transform playerTransform = newPlayer.transform;

            RegisterPlayer(playerTransform);

            var playerController = newPlayer.GetComponent<PlayerController>();
            if (playerController != null)
            {
                Debug.Log("[GameManager] Player controller found on " + newPlayer.name);
            }
        }

        public void CheckVictoryCondition()
        {
            if (_currentState != GameState.Playing)
                return;

            if (generatorManager != null && generatorManager.AllGeneratorsActivated)
            {
                EndGame(true);
            }
        }

        public void CheckDefeatCondition()
        {
            if (_currentState != GameState.Playing)
                return;

            if (gameTimer != null && gameTimer.IsTimeUp)
            {
                EndGame(false);
            }
        }

        private void Update()
        {
            if (_currentState != GameState.Playing)
                return;

            CheckVictoryCondition();
            CheckDefeatCondition();
        }

        public void ResetGame()
        {
            _currentState = GameState.Waiting;
            Time.timeScale = 1f;
            _activePlayers.Clear();
            OnGameStateChanged?.Invoke(_currentState);
            Debug.Log("[GameManager] Game reset");
        }

        public Transform GetPlayerSpawnPoint()
        {
            return playerSpawnPoint;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(playerSpawnPoint.position, 1f);
            }
        }
    }
}
