using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using TMPro;

public class CubeController : MonoBehaviourPun, IPunObservable
{
    public float speed = 5f;
    public GameObject bulletPrefab;
    public float fireRate = 0.5f;
    private float nextFireTime;

    public enum Team { None = 0, TeamA = 1, TeamB = 2 }
    public Team playerTeam = Team.None;
    private Dictionary<int, Team> playerTeams = new Dictionary<int, Team>();

    public SpriteRenderer spriteRenderer;
    private Rigidbody2D rb2D;
    public Color syncedColor = Color.white;
    public int health = 100;
    private Vector3 spawnPosition;
    private bool isInitialized = false;

    private Vector3 syncedPosition;
    private float syncedRotation;
    private float lastSyncTime;
    private Vector3 lastSyncedPosition;
    private float lastSyncedRotation;
    public static float mouseSensitivity = 1f;
    public bool canMove = false;
    [SerializeField] private TMP_Text nicknameText;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer не найден!");
            return;
        }

        rb2D = GetComponent<Rigidbody2D>();
        if (rb2D == null)
        {
            Debug.LogError("Rigidbody2D не найден!");
            return;
        }

        if (photonView == null)
        {
            Debug.LogError("PhotonView не найден!");
            return;
        }

        nicknameText = GetComponentInChildren<TMP_Text>();
        if (nicknameText == null)
        {
            Debug.LogError("NicknameText не найден!");
            return;
        }

        isInitialized = true;
    }

    void Start()
    {
        if (!isInitialized) return;

        spawnPosition = transform.position;

        if (photonView.IsMine)
        {
            PhotonNetwork.SendRate = 60;
            PhotonNetwork.SerializationRate = 30;
            nicknameText.text = PhotonNetwork.NickName;
            ApplyTeamColor();
            Debug.Log($"Игрок {PhotonNetwork.NickName} инициализирован, команда: {playerTeam}, цвет: {syncedColor}");
        }
        else
        {
            spriteRenderer.enabled = false;
            rb2D.bodyType = RigidbodyType2D.Kinematic;
            syncedPosition = transform.position;
            lastSyncedPosition = transform.position;
            syncedRotation = transform.eulerAngles.z;
            lastSyncedRotation = transform.eulerAngles.z;
            nicknameText.text = photonView.Owner.NickName;
            canMove = false;
            ApplyTeamColor();
        }
    }

    void FixedUpdate()
    {
        if (!isInitialized || !photonView.IsMine || !canMove) return;

        if (Camera.main == null) return;

        float moveHorizontal = Input.GetAxisRaw("Horizontal") * mouseSensitivity;
        float moveVertical = Input.GetAxisRaw("Vertical") * mouseSensitivity;
        Vector2 movement = new Vector2(moveHorizontal, moveVertical).normalized * speed;

        rb2D.MovePosition(rb2D.position + movement * Time.fixedDeltaTime);

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePosition - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            if (bulletPrefab != null)
            {
                Shoot(direction);
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    void Update()
    {
        if (!photonView.IsMine && isInitialized)
        {
            float timeSinceLastSync = Time.time - lastSyncTime;
            if (timeSinceLastSync > 0)
            {
                float t = Mathf.Clamp01(timeSinceLastSync / (1f / PhotonNetwork.SerializationRate));
                transform.position = Vector3.Lerp(transform.position, syncedPosition, t);
                float currentRotation = Mathf.LerpAngle(transform.eulerAngles.z, syncedRotation, t);
                transform.rotation = Quaternion.Euler(0, 0, currentRotation);
                ApplyTeamColor();
            }
        }
    }

    void Shoot(Vector2 direction)
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("CubeController: bulletPrefab не назначен!");
            return;
        }

        Vector3 spawnPos = transform.position + (Vector3)direction.normalized * 0.5f;
        Debug.Log($"Попытка инстанцировать пулю на позиции: {spawnPos}, префаб: {bulletPrefab.name}");
        GameObject bullet = PhotonNetwork.Instantiate(bulletPrefab.name, spawnPos, Quaternion.identity);
        if (bullet != null)
        {
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.SetDirection(direction);
                bulletScript.SetOwner(photonView.Owner);
                Debug.Log($"Пуля успешно создана, ViewID: {bullet.GetComponent<PhotonView>().ViewID}");
            }
            else
            {
                Debug.LogError("CubeController: На пуле отсутствует компонент Bullet!");
            }
        }
        else
        {
            Debug.LogError("CubeController: Не удалось инстанцировать пулю через PhotonNetwork.Instantiate!");
        }
    }

    [PunRPC]
    void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            health = 100;
            rb2D.position = spawnPosition;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine) return;

        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet != null && bullet.owner != null && bullet.owner.CustomProperties.ContainsKey("Team"))
        {
            Team shooterTeam = (Team)(int)bullet.owner.CustomProperties["Team"];
            if (shooterTeam != playerTeam)
            {
                photonView.RPC("TakeDamage", RpcTarget.All, 10);
                bullet.GetComponent<PhotonView>()?.RPC("DestroyBullet", RpcTarget.All);
            }
        }
    }

    [PunRPC]
    public void ShareTeam(int viewID, int teamInt)
    {
        playerTeams[viewID] = (Team)teamInt;
        if (photonView.ViewID == viewID)
        {
            playerTeam = (Team)teamInt;
            syncedColor = playerTeam == Team.TeamA ? Color.red : playerTeam == Team.TeamB ? Color.blue : Color.white;
            ApplyTeamColor();
            canMove = true;
            rb2D.bodyType = RigidbodyType2D.Dynamic;
            Debug.Log($"ShareTeam RPC: {photonView.Owner.NickName} в команде {playerTeam}, цвет: {syncedColor}");
        }
    }

    private void ApplyTeamColor()
    {
        if (spriteRenderer != null && spriteRenderer.material != null)
        {
            spriteRenderer.material.SetColor("_TeamColor", syncedColor);
            spriteRenderer.enabled = true;
            Debug.Log($"{(photonView.IsMine ? "Локальный" : "Удалённый")} игрок {photonView.Owner.NickName}: цвет материала установлен {syncedColor}");
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting && photonView.IsMine)
        {
            stream.SendNext(rb2D.position);
            stream.SendNext(transform.eulerAngles.z);
            stream.SendNext(syncedColor.r);
            stream.SendNext(syncedColor.g);
            stream.SendNext(syncedColor.b);
            stream.SendNext(syncedColor.a);
        }
        else
        {
            Vector2 receivedPosition2D = (Vector2)stream.ReceiveNext();
            syncedPosition = new Vector3(receivedPosition2D.x, receivedPosition2D.y, 0);
            syncedRotation = (float)stream.ReceiveNext();
            syncedColor.r = (float)stream.ReceiveNext();
            syncedColor.g = (float)stream.ReceiveNext();
            syncedColor.b = (float)stream.ReceiveNext();
            syncedColor.a = (float)stream.ReceiveNext();

            lastSyncedPosition = syncedPosition;
            lastSyncedRotation = syncedRotation;
            lastSyncTime = Time.time;
            ApplyTeamColor();
        }
    }
}