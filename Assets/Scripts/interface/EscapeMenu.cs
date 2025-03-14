using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq; // Добавлено для использования FirstOrDefault

public class EscapeMenu : MonoBehaviourPunCallbacks // Наследуем PhotonPunCallbacks для обработки событий Photon
{
    [SerializeField] private GameObject escapeMenuPanel; // Панель меню паузы
    private bool isPaused = false;

    void Start()
    {
        if (escapeMenuPanel == null)
        {
            Debug.LogError("EscapeMenu: Панель меню паузы не назначена!");
            return;
        }
        escapeMenuPanel.SetActive(false); // Скрываем панель при старте
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause(); // Открываем/закрываем меню по Esc
        }
    }

    void TogglePause()
    {
        isPaused = !isPaused;
        escapeMenuPanel.SetActive(isPaused);

        // В мультиплеере лучше избегать Time.timeScale, используем локальную паузу
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // Для мультиплеера отключаем только локальное управление
            CubeController localPlayer = FindObjectsByType<CubeController>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.photonView.IsMine);
            if (localPlayer != null)
            {
                localPlayer.canMove = !isPaused;
            }
            else
            {
                Debug.LogWarning("EscapeMenu: Локальный CubeController не найден!");
            }
        }
        else
        {
            // Для одиночной игры используем Time.timeScale
            Time.timeScale = isPaused ? 0f : 1f;
        }
    }

    public void ResumeButton()
    {
        TogglePause(); // Закрываем меню и продолжаем игру
    }

    public void ChangeTeamButton()
    {
        TogglePause(); // Закрываем меню паузы
        TeamSelectionManager teamSelection = FindObjectsByType<TeamSelectionManager>(FindObjectsSortMode.None)
            .FirstOrDefault();
        if (teamSelection != null)
        {
            teamSelection.ShowTeamSelection(); // Открываем панель выбора команды
        }
        else
        {
            Debug.LogWarning("EscapeMenu: TeamSelectionManager не найден в сцене!");
        }
    }

    public void BackToLobbyButton()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // Сбрасываем timescale перед выходом, если он использовался
            if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
            }
            PhotonNetwork.LeaveRoom(); // Покидаем комнату Photon
            StartCoroutine(WaitForRoomExit()); // Ждём завершения выхода
        }
        else
        {
            SceneManager.LoadScene("MainMenu"); // Если не в комнате, сразу загружаем MainMenu
        }
    }

    private IEnumerator WaitForRoomExit()
    {
        yield return new WaitUntil(() => !PhotonNetwork.InRoom); // Ждём, пока выход не завершится
        SceneManager.LoadScene("MainMenu"); // Загружаем сцену MainMenu
    }

    public void QuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Для остановки в редакторе
#else
        Application.Quit(); // Закрываем приложение
#endif
    }

    // Переопределяем callback Photon для обработки выхода из комнаты
    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        Debug.Log("EscapeMenu: Игрок покинул комнату Photon.");
    }
}