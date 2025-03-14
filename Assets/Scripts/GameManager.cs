using UnityEngine;
using Photon.Pun;
using Photon.Realtime; // Добавлено для DisconnectCause

public class GameManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject playerPrefab;

    void Start()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
    }

    public override void OnJoinedRoom()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom) return;

        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned!");
            return;
        }

        Vector3 spawnPosition = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0);
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
    }

    public override void OnLeftRoom()
    {
        // Уничтожаем объект игрока для всех клиентов
        if (PhotonNetwork.IsConnected && photonView != null && photonView.IsMine)
        {
            PhotonView[] views = FindObjectsOfType<PhotonView>();
            foreach (var view in views)
            {
                if (view.IsMine)
                {
                    PhotonNetwork.Destroy(view.gameObject);
                    Debug.Log($"Уничтожен объект с PhotonView {view.ViewID} для {PhotonNetwork.NickName}");
                }
            }
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        // Дополнительная очистка при отключении
        OnLeftRoom();
        Debug.Log($"Отключение по причине: {cause}");
    }
}