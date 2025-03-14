using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

public class MainMenuManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject serverPanel;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private Transform serverListContent;
    [SerializeField] private GameObject serverEntryPrefab;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private GameObject createLobbyPanel;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private Toggle isPrivateToggle;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Dropdown regionDropdown;

    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    private Dictionary<string, GameObject> roomButtons = new Dictionary<string, GameObject>();

    private readonly string[] regions = { "us", "eu" };
    private string pendingRegion = "us";
    private bool pendingRoomCreation = false;
    private string pendingLobbyName;
    private RoomOptions pendingRoomOptions;

    void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "us"; // По умолчанию US
            PhotonNetwork.ConnectUsingSettings();
        }

        mainMenuPanel.SetActive(true);
        serverPanel.SetActive(false);
        settingsMenu.SetActive(false);
        createLobbyPanel.SetActive(false);

        string savedNickname = PlayerPrefs.GetString("Nickname", "");
        if (nicknameInput != null && !string.IsNullOrEmpty(savedNickname))
        {
            nicknameInput.text = savedNickname;
        }

        if (regionDropdown != null)
        {
            regionDropdown.value = 0; // US по умолчанию
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Подключено к Photon Master Server");
        PhotonNetwork.JoinLobby();

        if (pendingRoomCreation)
        {
            PhotonNetwork.CreateRoom(pendingLobbyName, pendingRoomOptions);
            pendingRoomCreation = false;
        }
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Отключен от Photon: {cause}");
        if (pendingRoomCreation)
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = pendingRegion;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        UpdateRoomList(roomList);
    }

    void UpdateRoomList(List<RoomInfo> roomList)
    {
        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList)
            {
                cachedRoomList.Remove(room.Name);
                if (roomButtons.ContainsKey(room.Name) && roomButtons[room.Name] != null)
                {
                    Destroy(roomButtons[room.Name]);
                    roomButtons.Remove(room.Name);
                }
            }
            else
            {
                cachedRoomList[room.Name] = room;
            }
        }

        foreach (var room in cachedRoomList.Values)
        {
            if (!room.IsOpen || !room.IsVisible) continue;

            if (!roomButtons.ContainsKey(room.Name))
            {
                GameObject entry = Instantiate(serverEntryPrefab, serverListContent);
                TMP_Text textComponent = entry.GetComponentInChildren<TMP_Text>();
                if (textComponent != null)
                {
                    textComponent.text = $"{room.Name} | Игроки: {room.PlayerCount}/{room.MaxPlayers} | Пинг: {PhotonNetwork.GetPing()}";
                }
                else
                {
                    Debug.LogError("TMP_Text не найден в serverEntryPrefab!");
                }
                Button buttonComponent = entry.GetComponent<Button>();
                if (buttonComponent != null)
                {
                    buttonComponent.onClick.AddListener(() => JoinRoom(room.Name));
                }
                else
                {
                    Debug.LogError("Button не найден в serverEntryPrefab!");
                }
                roomButtons[room.Name] = entry;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(serverListContent as RectTransform);
            }
            else
            {
                TMP_Text textComponent = roomButtons[room.Name].GetComponentInChildren<TMP_Text>();
                if (textComponent != null)
                {
                    textComponent.text = $"{room.Name} | Игроки: {room.PlayerCount}/{room.MaxPlayers} | Пинг: {PhotonNetwork.GetPing()}";
                }
            }
        }
    }

    public void PlayButton()
    {
        mainMenuPanel.SetActive(false);
        serverPanel.SetActive(true);
        settingsMenu.SetActive(false);
        createLobbyPanel.SetActive(false);
    }

    public void QuickJoinButton()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("Клиент еще не готов для операций. Дождитесь подключения к лобби.");
            return;
        }

        SetNickname();
        PhotonNetwork.JoinRandomRoom();
    }

    public void CreateLobbyButton()
    {
        createLobbyPanel.SetActive(true);
    }

    public void CreateButton()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("Клиент еще не готов для операций. Дождитесь подключения к лобби.");
            return;
        }

        SetNickname();
        string lobbyName = lobbyNameInput.text.Trim();
        if (string.IsNullOrEmpty(lobbyName)) lobbyName = "Room" + Random.Range(1000, 9999);
        bool isPrivate = isPrivateToggle.isOn;
        string password = passwordInput.text.Trim();

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 5,
            IsVisible = !isPrivate,
            IsOpen = true
        };

        options.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { "Password", password }
        };
        options.CustomRoomPropertiesForLobby = new string[] { "Password" };

        int selectedRegionIndex = regionDropdown != null ? regionDropdown.value : 0;
        string selectedRegion = regions[selectedRegionIndex];
        string currentRegion = PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion;

        if (currentRegion != selectedRegion)
        {
            pendingRoomCreation = true;
            pendingLobbyName = lobbyName;
            pendingRoomOptions = options;
            pendingRegion = selectedRegion;
            PhotonNetwork.Disconnect();
        }
        else
        {
            PhotonNetwork.CreateRoom(lobbyName, options);
        }
    }

    public void CancelButton()
    {
        createLobbyPanel.SetActive(false);
        serverPanel.SetActive(true);
    }

    void JoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("Клиент еще не готов для операций. Дождитесь подключения к лобби.");
            return;
        }

        SetNickname();
        if (!cachedRoomList.ContainsKey(roomName))
        {
            Debug.LogWarning($"Комната {roomName} не найдена в кэше!");
            return;
        }

        RoomInfo room = cachedRoomList[roomName];
        string password = "";
        if (room.CustomProperties.ContainsKey("Password"))
        {
            password = (string)room.CustomProperties["Password"];
            if (!string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("Требуется пароль! Необходимо реализовать UI для ввода пароля.");
                return;
            }
        }
        PhotonNetwork.JoinRoom(roomName);
    }

    void SetNickname()
    {
        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            System.Text.StringBuilder randomNickname = new System.Text.StringBuilder();
            string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string numbers = "0123456789";

            for (int i = 0; i < 3; i++)
            {
                randomNickname.Append(letters[Random.Range(0, letters.Length)]);
            }
            for (int i = 0; i < 3; i++)
            {
                randomNickname.Append(numbers[Random.Range(0, numbers.Length)]);
            }

            nickname = randomNickname.ToString();
            nicknameInput.text = nickname;
        }

        PhotonNetwork.NickName = nickname;
        PlayerPrefs.SetString("Nickname", nickname);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room: " + PhotonNetwork.CurrentRoom.Name);
        PhotonNetwork.LoadLevel("Game");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"Не удалось присоединиться к случайной комнате: {message}. Создаем новую комнату...");
        CreateButton();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"Не удалось присоединиться к комнате: {message}");
    }

    public void SettingsButton()
    {
        mainMenuPanel.SetActive(false);
        serverPanel.SetActive(false);
        settingsMenu.SetActive(true);
        createLobbyPanel.SetActive(false);
    }

    public void BackToMainMenuButton()
    {
        mainMenuPanel.SetActive(true);
        serverPanel.SetActive(false);
        settingsMenu.SetActive(false);
        createLobbyPanel.SetActive(false);
    }

    public void QuitButton()
    {
        Application.Quit();
    }
}