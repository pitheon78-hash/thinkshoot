using UnityEngine;

/// <summary>
/// [30m 장거리 저격 스나이퍼 모드 지원]
/// 60m x 40m 규격의 대형 맵을 자동 생성하는 환경 빌더
/// </summary>
public class ArenaEnvironmentBuilder : MonoBehaviour
{
    private void Awake()
    {
        BuildGridFloor();
        BuildPerimeterWalls();
    }

    private Shader GetSafeShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s != null) return s;
        s = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (s != null) return s;
        s = Shader.Find("Standard");
        if (s != null) return s;
        return Shader.Find("Diffuse");
    }

    private void BuildGridFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Arena_Floor";
        floor.transform.position = new Vector3(0, -0.5f, 0);
        
        // ★ 수정됨: 바닥 크기를 좌우 40m, 앞뒤 60m로 대폭 확장!
        floor.transform.localScale = new Vector3(40f, 1f, 60f); 

        int res = 64;
        Texture2D gridTex = new Texture2D(res, res);
        gridTex.filterMode = FilterMode.Point;
        gridTex.wrapMode = TextureWrapMode.Repeat;

        Color darkTile = new Color(0.12f, 0.13f, 0.16f);
        Color gridLine = new Color(0.25f, 0.28f, 0.35f);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                bool isBorder = (x == 0 || y == 0 || x == res - 1 || y == res - 1);
                gridTex.SetPixel(x, y, isBorder ? gridLine : darkTile);
            }
        }
        gridTex.Apply();

        Material floorMat = new Material(GetSafeShader());
        floorMat.mainTexture = gridTex;
        
        // ★ 수정됨: 맵이 넓어진 만큼 타일 무늬도 찌그러지지 않게 2배로 조밀하게 변경
        floorMat.mainTextureScale = new Vector2(20f, 30f); 

        if (floorMat.HasProperty("_Glossiness")) floorMat.SetFloat("_Glossiness", 0.35f);
        if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.35f);

        floor.GetComponent<Renderer>().material = floorMat;
    }

    private void BuildPerimeterWalls()
    {
        GameObject wallGroup = new GameObject("Arena_Boundaries");

        Material wallMat = new Material(GetSafeShader());
        wallMat.color = new Color(0.18f, 0.20f, 0.24f);

        if (wallMat.HasProperty("_Glossiness")) wallMat.SetFloat("_Glossiness", 0.1f);
        if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.1f);

        // ★ 수정됨: 4방향 벽을 각각 30m, 20m 밖으로 밀어내고, 길이도 60m, 40m로 연장!
        CreateWall(wallGroup.transform, new Vector3(0, 2f, 30f), new Vector3(40f, 4f, 0.8f), wallMat);   // 북벽
        CreateWall(wallGroup.transform, new Vector3(0, 2f, -30f), new Vector3(40f, 4f, 0.8f), wallMat);  // 남벽
        CreateWall(wallGroup.transform, new Vector3(-20f, 2f, 0), new Vector3(0.8f, 4f, 60f), wallMat);  // 서벽
        CreateWall(wallGroup.transform, new Vector3(20f, 2f, 0), new Vector3(0.8f, 4f, 60f), wallMat);   // 동벽
    }

    private void CreateWall(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "BoundaryWall";
        w.transform.SetParent(parent);
        w.transform.position = pos;
        w.transform.localScale = scale;
        w.GetComponent<Renderer>().material = mat;

        w.GetComponent<Renderer>().enabled = false;
    }
}