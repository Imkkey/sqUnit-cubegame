using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using System.Linq;

public class TeamSelectionManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject teamSelectionPanel;
    [SerializeField] private TMP_Text teamCountText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private Button joinRedButton;
    [SerializeField] private Button joinBlueButton;
    [SerializeField] private Button joinRandomButton;
    private Dictionary<int, string> playerNicknames = new Dictionary<int, string>();
    private Dictionary<int, CubeController.Team> playerTeams = new Dictionary<int, CubeController.Team>();

    void Start()
    {
        if (teamSelectionPanel == null) teamSelectionPanel = gameObject;

        if (joinRedButton != null) joinRedButton.onClick.AddListener(JoinRedTeam);
        if (joinBlueButton != null) joinBlueButton.onClick.AddListener(JoinBlueTeam);
        if (joinRandomButton != null) joinRandomButton.onClick.AddListener(JoinRandomTeam);
    }

    public void ShowTeamSelection()
    {
        if (teamSelectionPanel != null)
        {
            teamSelectionPanel.SetActive(true);
            UpdatePlayerList();
        }
    }

    public override void OnJoinedRoom()
    {
        ShowTeamSelection();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => UpdatePlayerList();
    public override void OnPlayerLeftRoom(Player otherPlayer) => UpdatePlayerList();

    void UpdatePlayerList()
    {
        if (playerListContent == null || teamCountText == null)
        {
            Debug.LogError("playerListContent или teamCountText не назначены!");
            return;
        }

        if (!PhotonNetwork.InRoom) return;

        foreach (Transform child in playerListContent) Destroy(child.gameObject);

        int redCount = 0;
        int blueCount = 0;

        if (PhotonNetwork.PlayerList == null || PhotonNetwork.PlayerList.Length == 0)
        {
            teamCountText.text = "Красные: 0 | Синие: 0";
            return;
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player == null) continue;

            string nickname = player.NickName ?? "Unknown";
            CubeController.Team team = CubeController.Team.None;

            if (player.CustomProperties != null && player.CustomProperties.ContainsKey("Team"))
            {
                team = (CubeController.Team)(int)player.CustomProperties["Team"];
                if (team == CubeController.Team.TeamA) redCount++;
                else if (team == CubeController.Team.TeamB) blueCount++;
            }

            playerNicknames[player.ActorNumber] = nickname;
            playerTeams[player.ActorNumber] = team;

            if (playerEntryPrefab != null)
            {
                GameObject entry = Instantiate(playerEntryPrefab, playerListContent);
                string teamName = team == CubeController.Team.TeamA ? "[Красные]" : team == CubeController.Team.TeamB ? "[Синие]" : "[Не выбрано]";
                TMP_Text entryText = entry.GetComponentInChildren<TMP_Text>();
                if (entryText != null) entryText.text = $"{nickname} {teamName}";
                Button entryButton = entry.GetComponent<Button>();
                if (entryButton != null) entryButton.onClick.AddListener(() => JoinFriendTeam(player.ActorNumber));
            }
        }

        teamCountText.text = $"Красные: {redCount} | Синие: {blueCount}";
    }

    void JoinRedTeam() => StartCoroutine(AssignTeamWithDelay(CubeController.Team.TeamA));
    void JoinBlueTeam() => StartCoroutine(AssignTeamWithDelay(CubeController.Team.TeamB));
    void JoinRandomTeam()
    {
        int redCount = PhotonNetwork.PlayerList.Count(p => p.CustomProperties.ContainsKey("Team") && (CubeController.Team)(int)p.CustomProperties["Team"] == CubeController.Team.TeamA);
        int blueCount = PhotonNetwork.PlayerList.Count(p => p.CustomProperties.ContainsKey("Team") && (CubeController.Team)(int)p.CustomProperties["Team"] == CubeController.Team.TeamB);
        StartCoroutine(AssignTeamWithDelay(redCount <= blueCount ? CubeController.Team.TeamA : CubeController.Team.TeamB));
    }

    void JoinFriendTeam(int actorNumber)
    {
        if (playerTeams.ContainsKey(actorNumber))
        {
            StartCoroutine(AssignTeamWithDelay(playerTeams[actorNumber]));
        }
    }

    System.Collections.IEnumerator AssignTeamWithDelay(CubeController.Team team)
    {
        yield return new WaitUntil(() => PhotonNetwork.LocalPlayer != null);

        teamSelectionPanel.SetActive(false);
        CubeController cubeController = FindObjectsByType<CubeController>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.photonView.IsMine);

        if (cubeController != null)
        {
            cubeController.playerTeam = team;
            cubeController.syncedColor = team == CubeController.Team.TeamA ? Color.red : team == CubeController.Team.TeamB ? Color.blue : Color.white;

            if (cubeController.spriteRenderer != null)
            {
                if (cubeController.spriteRenderer.material != null)
                {
                    cubeController.spriteRenderer.material.SetColor("_TeamColor", cubeController.syncedColor);
                    Debug.Log($"Локально: {PhotonNetwork.NickName} в команде {team}, цвет материала: {cubeController.syncedColor}");
                }
                else
                {
                    cubeController.spriteRenderer.color = cubeController.syncedColor;
                    Debug.Log($"Локально: {PhotonNetwork.NickName} в команде {team}, цвет спрайта: {cubeController.syncedColor}");
                }
            }
            else
            {
                Debug.LogError($"SpriteRenderer null на {cubeController.gameObject.name}!");
            }

            cubeController.canMove = true;

            Hashtable teamProp = new Hashtable { { "Team", (int)team } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(teamProp);

            cubeController.photonView.RPC("ShareTeam", RpcTarget.AllBuffered, cubeController.photonView.ViewID, (int)team);
            Debug.Log($"AssignTeam RPC отправлен для {PhotonNetwork.NickName}, команда: {team}, ViewID: {cubeController.photonView.ViewID}");
        }
        else
        {
            Debug.LogError("CubeController не найден для локального игрока!");
        }
    }
}