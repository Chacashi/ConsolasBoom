using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HouseEscape.Systems;
using HouseEscape.Network;
using HouseEscape.Player;
using System;

namespace HouseEscape.UI
{
    public class GameUI : MonoBehaviour
    {
        [Header("Main Menu")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button startServerButton;
        [SerializeField] private Button connectClientButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_InputField serverAddressField;

        [Header("Lobby")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private TMP_Text lobbyPlayerCountText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveLobbyButton;

        [Header("HUD")]
        [SerializeField] private GameObject hudPanel;

        [Header("Pause Menu")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitToMenuButton;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text gameOverTitleText;
        [SerializeField] private TMP_Text gameOverMessageText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button quitToMenuButton2;

        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private NetworkGameManager networkGameManager;

        private GameManager.GameState _lastState = GameManager.GameState.Waiting;

        private void Awake()
        {
            HideAllPanels();
        }

        private void Start()
        {
            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>();

            if (networkGameManager == null)
                networkGameManager = FindAnyObjectByType<NetworkGameManager>();

            if (gameManager != null)
                gameManager.OnGameStateChanged += HandleGameStateChanged;

            if (startHostButton != null)
                startHostButton.onClick.AddListener(StartHost);
            if (startServerButton != null)
                startServerButton.onClick.AddListener(StartServer);
            if (connectClientButton != null)
                connectClientButton.onClick.AddListener(ConnectClient);
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitToMenu);
            if (startGameButton != null)
                startGameButton.onClick.AddListener(StartGame);
            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.AddListener(LeaveLobby);
            if (resumeButton != null)
                resumeButton.onClick.AddListener(ResumeGame);
            if (quitToMenuButton != null)
                quitToMenuButton.onClick.AddListener(QuitToMenu);
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(PlayAgain);
            if (quitToMenuButton2 != null)
                quitToMenuButton2.onClick.AddListener(QuitToMenu);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void Update()
        {
            HandlePauseInput();
        }

        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }

        private void HandleGameStateChanged(GameManager.GameState state)
        {
            _lastState = state;

            switch (state)
            {
                case GameManager.GameState.Waiting:
                    ShowMainMenu();
                    break;
                case GameManager.GameState.Playing:
                    ShowHUD();
                    break;
                case GameManager.GameState.Paused:
                    ShowPauseMenu();
                    break;
                case GameManager.GameState.Victory:
                    ShowGameOver(true);
                    break;
                case GameManager.GameState.Defeat:
                    ShowGameOver(false);
                    break;
            }
        }

        private void ShowMainMenu()
        {
            HideAllPanels();
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(true);
        }

        private void ShowLobby()
        {
            HideAllPanels();
            if (lobbyPanel != null)
            {
                lobbyPanel.SetActive(true);
                UpdateLobbyUI();
            }
        }

        private void ShowHUD()
        {
            HideAllPanels();
            if (hudPanel != null)
                hudPanel.SetActive(true);
        }

        private void ShowPauseMenu()
        {
            if (pausePanel != null)
                pausePanel.SetActive(true);
        }

        private void ShowGameOver(bool victory)
        {
            HideAllPanels();
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                if (gameOverTitleText != null)
                    gameOverTitleText.text = victory ? "VICTORY!" : "GAME OVER";
                if (gameOverMessageText != null)
                {
                    gameOverMessageText.text = victory
                        ? "You escaped the house before dawn!"
                        : "The monster caught you... or the sun rose.";
                }
            }
        }

        private void HideAllPanels()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        private void StartHost()
        {
            if (networkGameManager != null)
            {
                networkGameManager.StartHost();
                ShowLobby();
            }
        }

        private void StartServer()
        {
            if (networkGameManager != null)
            {
                networkGameManager.StartServer();
                ShowLobby();
            }
        }

        private void ConnectClient()
        {
            if (networkGameManager != null)
            {
                networkGameManager.ConnectClient();
            }
        }

        private void StartGame()
        {
            if (gameManager != null)
            {
                gameManager.StartGame();
            }
        }

        private void LeaveLobby()
        {
            if (networkGameManager != null)
                networkGameManager.StopNetwork();

            ShowMainMenu();
        }

        private void TogglePause()
        {
            if (gameManager == null) return;

            if (gameManager.IsPlaying)
            {
                gameManager.PauseGame();
            }
            else if (gameManager.IsPaused)
            {
                gameManager.ResumeGame();
            }
        }

        private void ResumeGame()
        {
            if (gameManager != null)
                gameManager.ResumeGame();
        }

        private void PlayAgain()
        {
            if (gameManager != null)
            {
                gameManager.ResetGame();
                gameManager.StartGame();
            }
        }

        private void QuitToMenu()
        {
            if (networkGameManager != null)
                networkGameManager.StopNetwork();

            if (gameManager != null)
                gameManager.ResetGame();

            ShowMainMenu();
        }

        private void UpdateLobbyUI()
        {
            if (lobbyPlayerCountText != null && networkGameManager != null)
            {
                lobbyPlayerCountText.text = "Players: " + networkGameManager.ConnectedPlayers + "/4";
            }
        }

        private void OnApplicationQuit()
        {
            if (networkGameManager != null && networkGameManager.IsHost)
            {
                networkGameManager.StopNetwork();
            }
        }
    }
}
