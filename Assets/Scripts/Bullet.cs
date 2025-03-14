using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class Bullet : MonoBehaviourPun, IPunObservable
{
    private Vector2 direction;
    public float bulletSpeed = 15f;
    public Photon.Realtime.Player owner;
    private Vector3 syncedPosition;
    private Vector2 syncedDirection;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb2D;
    [SerializeField] private LayerMask obstacleMask;

    private static List<Bullet> allBullets = new List<Bullet>();

    void Awake()
    {
        if (photonView == null)
        {
            Debug.LogError("PhotonView не найден на объекте пули!");
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer не найден на пуле!");
            return;
        }

        rb2D = GetComponent<Rigidbody2D>();
        if (rb2D == null)
        {
            Debug.LogError("Rigidbody2D не найден на пуле!");
            return;
        }

        gameObject.tag = "Bullet";
        syncedPosition = transform.position;
        allBullets.Add(this);
        Debug.Log($"Bullet: Инициализирована, ViewID: {photonView.ViewID}, позиция: {transform.position}, материал: {spriteRenderer.material?.name}, шейдер: {spriteRenderer.material?.shader?.name}");
    }

    void OnEnable()
    {
        allBullets.Add(this);
        Debug.Log($"Bullet: Включена, ViewID: {photonView.ViewID}");
    }

    void OnDisable()
    {
        allBullets.Remove(this);
        Debug.Log($"Bullet: Отключена, ViewID: {photonView.ViewID}");
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        syncedDirection = direction;
        spriteRenderer.enabled = true;
        if (photonView.IsMine)
        {
            rb2D.linearVelocity = direction * bulletSpeed;
            Debug.Log($"Bullet: Установлено направление: {direction}, скорость: {bulletSpeed}, позиция: {transform.position}");
        }
    }

    public void SetOwner(Photon.Realtime.Player player)
    {
        owner = player;
        Debug.Log($"Bullet: Владелец установлен: {player.NickName}, ViewID: {photonView.ViewID}");
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            float rayDistance = bulletSpeed * Time.fixedDeltaTime;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, rayDistance, obstacleMask);
            if (hit.collider != null)
            {
                Debug.Log($"Bullet: Столкновение с {hit.collider.name} на расстоянии {hit.distance}");
                photonView.RPC("DestroyBullet", RpcTarget.All);
                return;
            }
            syncedPosition = transform.position;
        }
    }

    void Update()
    {
        if (!photonView.IsMine)
        {
            Vector3 targetPosition = syncedPosition + (Vector3)(syncedDirection * bulletSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine) return;

        CubeController cube = other.GetComponent<CubeController>();
        if (cube != null && cube.photonView != null && owner != null)
        {
            if (owner.CustomProperties.ContainsKey("Team"))
            {
                CubeController.Team shooterTeam = (CubeController.Team)(int)owner.CustomProperties["Team"];
                if (shooterTeam != cube.playerTeam)
                {
                    Debug.Log($"Bullet: Попадание в {cube.photonView.Owner.NickName}, ViewID: {photonView.ViewID}");
                    cube.photonView.RPC("TakeDamage", RpcTarget.All, 10);
                    photonView.RPC("DestroyBullet", RpcTarget.All);
                }
            }
        }
        else if (other.CompareTag("Obstacle"))
        {
            Debug.Log($"Bullet: Попадание в препятствие {other.name}, ViewID: {photonView.ViewID}");
            photonView.RPC("DestroyBullet", RpcTarget.All);
        }
    }

    [PunRPC]
    void DestroyBullet()
    {
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
            Debug.Log($"Bullet: Уничтожена через RPC, ViewID: {photonView.ViewID}");
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting && photonView.IsMine)
        {
            stream.SendNext(transform.position);
            stream.SendNext(direction);
            Debug.Log($"Bullet: Синхронизация отправлена, позиция: {transform.position}, направление: {direction}");
        }
        else
        {   
            syncedPosition = (Vector3)stream.ReceiveNext();
            syncedDirection = (Vector2)stream.ReceiveNext();
            direction = syncedDirection;
            Debug.Log($"Bullet: Синхронизация получена, позиция: {syncedPosition}, направление: {syncedDirection}");
        }
    }
}