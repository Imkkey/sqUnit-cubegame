using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class FieldOfView : MonoBehaviourPun, IPunObservable
{
    [Header("Параметры обзора")]
    public float viewRadius = 5f;
    [Range(0, 360)] public float viewAngle = 90f;
    public int rayCount = 100;

    [Header("Слои")]
    public LayerMask obstacleMask;

    private Mesh viewMesh;
    private CubeController cubeController;
    private CubeController.Team playerTeam;
    private List<Vector3> viewPoints = new List<Vector3>();

    void Awake()
    {
        viewMesh = new Mesh();
        viewMesh.name = "View Mesh";
        GetComponent<MeshFilter>().mesh = viewMesh;

        cubeController = GetComponentInParent<CubeController>();
        if (cubeController == null)
        {
            Debug.LogError("FOV: CubeController не найден!");
            return;
        }

        StartCoroutine(WaitForTeamAssignment());
    }

    System.Collections.IEnumerator WaitForTeamAssignment()
    {
        while (cubeController.playerTeam == CubeController.Team.None)
        {
            yield return null;
        }
        playerTeam = cubeController.playerTeam;
    }

    void LateUpdate()
    {
        if (!photonView.IsMine) return;

        transform.position = cubeController.transform.position;
        transform.rotation = cubeController.transform.rotation;

        DrawFieldOfView();
        UpdateEnemyVisibility();
    }

    void DrawFieldOfView()
    {
        viewPoints.Clear();
        float stepAngleSize = viewAngle / rayCount;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = -viewAngle / 2 + stepAngleSize * i;
            Vector3 dir = DirFromAngle(angle, false);
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, viewRadius, obstacleMask);
            Vector3 point = hit.collider != null ? hit.point : transform.position + dir * viewRadius;
            viewPoints.Add(point);
        }

        int vertexCount = viewPoints.Count + 1;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[(vertexCount - 2) * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < viewPoints.Count; i++)
        {
            vertices[i + 1] = transform.InverseTransformPoint(viewPoints[i]);
        }

        for (int i = 0; i < vertexCount - 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        viewMesh.Clear();
        viewMesh.vertices = vertices;
        viewMesh.triangles = triangles;
        viewMesh.RecalculateNormals();
    }

    void UpdateEnemyVisibility()
    {
        if (playerTeam == CubeController.Team.None) return;

        // Обрабатываем игроков (CubeController)
        CubeController[] allPlayers = FindObjectsByType<CubeController>(FindObjectsSortMode.None);
        foreach (CubeController otherPlayer in allPlayers)
        {
            if (otherPlayer.photonView == null || otherPlayer == cubeController) continue;

            SpriteRenderer spriteRenderer = otherPlayer.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.material == null) continue;

            Material mat = spriteRenderer.material;
            int teamID = (int)otherPlayer.playerTeam;
            mat.SetFloat("_TeamID", teamID);
            mat.SetFloat("_ViewerTeamID", (int)playerTeam);
            mat.SetFloat("_FOVAngle", viewAngle);
            mat.SetFloat("_FOVRadius", viewRadius);
            mat.SetVector("_PlayerPos", new Vector4(transform.position.x, transform.position.y, transform.eulerAngles.z, 0));
            mat.SetFloat("_IsBullet", 0); // Для игроков

            // Проверка видимости с учётом стен
            Bounds bounds = spriteRenderer.bounds;
            Vector3 center = bounds.center;
            Vector3 directionToTarget = center - transform.position;
            float distanceToTarget = directionToTarget.magnitude;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget.normalized, viewRadius, obstacleMask);

            float wallDistance = viewRadius;
            if (hit.collider != null && Vector3.Distance(transform.position, hit.point) < distanceToTarget)
            {
                wallDistance = Vector3.Distance(transform.position, hit.point);
            }

            mat.SetFloat("_WallDistance", wallDistance);
            Debug.Log($"Расстояние до стены для игрока {otherPlayer.photonView.Owner.NickName}: {wallDistance}, угол: {viewAngle}, радиус: {viewRadius}");
        }

        // Обрабатываем пули (Bullet)
        Bullet[] allBullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        foreach (Bullet bullet in allBullets)
        {
            if (bullet.photonView == null) continue;

            SpriteRenderer bulletRenderer = bullet.GetComponent<SpriteRenderer>();
            if (bulletRenderer == null || bulletRenderer.material == null) continue;

            Material mat = bulletRenderer.material;
            // Устанавливаем параметры для шейдера
            mat.SetFloat("_TeamID", bullet.owner != null && bullet.owner.CustomProperties.ContainsKey("Team") ? (int)(CubeController.Team)bullet.owner.CustomProperties["Team"] : 0);
            mat.SetFloat("_ViewerTeamID", (int)playerTeam);
            mat.SetFloat("_FOVAngle", viewAngle);
            mat.SetFloat("_FOVRadius", viewRadius);
            mat.SetVector("_PlayerPos", new Vector4(transform.position.x, transform.position.y, transform.eulerAngles.z, 0));
            mat.SetFloat("_IsBullet", 1); // Для пуль

            // Проверка видимости с учётом стен
            Vector3 bulletPos = bullet.transform.position;
            Vector3 directionToBullet = bulletPos - transform.position;
            float distanceToBullet = directionToBullet.magnitude;
            RaycastHit2D bulletHit = Physics2D.Raycast(transform.position, directionToBullet.normalized, viewRadius, obstacleMask);

            float bulletWallDistance = viewRadius;
            if (bulletHit.collider != null && Vector3.Distance(transform.position, bulletHit.point) < distanceToBullet)
            {
                bulletWallDistance = Vector3.Distance(transform.position, bulletHit.point);
            }

            mat.SetFloat("_WallDistance", bulletWallDistance);
            Debug.Log($"Расстояние до стены для пули (ViewID: {bullet.photonView.ViewID}): {bulletWallDistance}, угол: {viewAngle}, радиус: {viewRadius}");
        }
    }

    Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal && cubeController != null)
        {
            angleInDegrees += cubeController.transform.eulerAngles.z;
        }
        return new Vector3(Mathf.Cos(angleInDegrees * Mathf.Deg2Rad), Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting && photonView.IsMine)
        {
            stream.SendNext(viewPoints.Count);
            for (int i = 0; i < viewPoints.Count; i++)
            {
                stream.SendNext(viewPoints[i]);
            }
            stream.SendNext(viewAngle);
            stream.SendNext(viewRadius);
        }
        else
        {
            int pointCount = (int)stream.ReceiveNext();
            viewPoints.Clear();
            for (int i = 0; i < pointCount; i++)
            {
                viewPoints.Add((Vector3)stream.ReceiveNext());
            }
            viewAngle = (float)stream.ReceiveNext();
            viewRadius = (float)stream.ReceiveNext();

            if (info.Sender.CustomProperties.ContainsKey("Team") && viewPoints.Count > 0)
            {
                CubeController.Team senderTeam = (CubeController.Team)(int)info.Sender.CustomProperties["Team"];
                if (senderTeam == playerTeam)
                {
                    int vertexCount = viewPoints.Count + 1;
                    Vector3[] vertices = new Vector3[vertexCount];
                    int[] triangles = new int[(vertexCount - 2) * 3];

                    vertices[0] = Vector3.zero;
                    for (int i = 0; i < viewPoints.Count; i++)
                    {
                        vertices[i + 1] = transform.InverseTransformPoint(viewPoints[i]);
                    }

                    for (int i = 0; i < vertexCount - 2; i++)
                    {
                        triangles[i * 3] = 0;
                        triangles[i * 3 + 1] = i + 1;
                        triangles[i * 3 + 2] = i + 2;
                    }

                    viewMesh.Clear();
                    viewMesh.vertices = vertices;
                    viewMesh.triangles = triangles;
                    viewMesh.RecalculateNormals();
                }
            }
        }
    }
}