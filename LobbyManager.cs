using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Fusion;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

/// <summary>
/// [Lobby Manager] GameMode.AutoHostOrClient 자동 대전 매칭과 GameMode.Single 오프라인 러너를 완벽 가동하여
/// 혼자하기 이동 마비 버그를 해결하고, 4번째 [게임 종료] 버튼과 안전 확인 팝업을 제공하는 마스터 로비 매니저입니다.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("네트워크 씬 설정")]
    [SerializeField] private string _gameSceneName = "GameScene";

    private NetworkRunner _runner;
    private Text _statusText;
    private GameObject _quitPopupObj;

    public static bool IsSinglePlayMode = false;
    public static PlayerController.Role SinglePlayRole = PlayerController.Role.Hunter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        EnsureEventSystem();
        CreateAutoLobbyUI();
    }

    private void Update()
    {
        // 안드로이드 뒤로가기 키 또는 PC ESC 키 입력 시 종료 팝업 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleQuitPopup();
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }
    }

    private void CreateAutoLobbyUI()
    {
        GameObject canvasObj = new GameObject("LobbyCanvas_Auto");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject textObj = new GameObject("StatusText");
        textObj.transform.SetParent(canvasObj.transform, false);
        _statusText = textObj.AddComponent<Text>();
        _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusText.fontSize = 38;
        _statusText.fontStyle = FontStyle.Bold;
        _statusText.alignment = TextAnchor.MiddleCenter;
        _statusText.color = Color.white;
        _statusText.text = "SHOOT & HIDE : 1:1 ARENA\n원하시는 게임 모드를 선택하십시오";

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchoredPosition = new Vector2(0, 260);
        textRect.sizeDelta = new Vector2(1200, 140);

        // 1. [1:1 온라인 대전 매칭찾기 버튼]
        CreateButton(canvasObj.transform, "Btn_Matchmaking", "⚔ 1:1 온라인 대전 [매칭찾기] ⚔", 
            new Vector2(0, 100), new Vector2(680, 105), new Color(0.18f, 0.75f, 0.35f), () =>
        {
            StartOnlineMatchmaking();
        });

        // 2. [혼자하기] 헌터 사격 연습
        CreateButton(canvasObj.transform, "Btn_Hunter", "★ [혼자하기] HUNTER 사격 연습 ★", 
            new Vector2(0, -15), new Vector2(620, 85), new Color(0.85f, 0.25f, 0.2f), () =>
        {
            StartSinglePlay(PlayerController.Role.Hunter);
        });

        // 3. [혼자하기] 히더 은신 연습
        CreateButton(canvasObj.transform, "Btn_Hider", "★ [혼자하기] HIDER 은신 연습 ★", 
            new Vector2(0, -115), new Vector2(620, 85), new Color(0.2f, 0.55f, 0.9f), () =>
        {
            StartSinglePlay(PlayerController.Role.Hider);
        });

        // 4. ★ [신규]: 게임 종료 (EXIT) 버튼 추가!
        CreateButton(canvasObj.transform, "Btn_Quit", "✕  게임 종료 (EXIT)", 
            new Vector2(0, -215), new Vector2(620, 75), new Color(0.25f, 0.25f, 0.28f), () =>
        {
            ToggleQuitPopup();
        });

        // 종료 확인 팝업 오브젝트 사전 생성
        CreateQuitPopup(canvasObj.transform);
    }

    private void CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color btnColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;
        img.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 28;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.sizeDelta = size;
    }

    private void CreateQuitPopup(Transform parent)
    {
        _quitPopupObj = new GameObject("QuitConfirmPopup");
        _quitPopupObj.transform.SetParent(parent, false);

        Image bg = _quitPopupObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);

        RectTransform popRect = _quitPopupObj.GetComponent<RectTransform>();
        popRect.anchoredPosition = Vector2.zero;
        popRect.sizeDelta = new Vector2(650, 320);

        // 팝업 텍스트
        GameObject msgObj = new GameObject("MessageText");
        msgObj.transform.SetParent(_quitPopupObj.transform, false);
        Text msgTxt = msgObj.AddComponent<Text>();
        msgTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        msgTxt.fontSize = 32;
        msgTxt.fontStyle = FontStyle.Bold;
        msgTxt.alignment = TextAnchor.MiddleCenter;
        msgTxt.color = Color.white;
        msgTxt.text = "게임을 완전히 종료하시겠습니까?";
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchoredPosition = new Vector2(0, 50);
        msgRect.sizeDelta = new Vector2(600, 80);

        // 확인 버튼
        CreateButton(_quitPopupObj.transform, "Btn_ConfirmQuit", "종료 (확인)", new Vector2(-140, -60), new Vector2(240, 70), new Color(0.85f, 0.2f, 0.2f), () =>
        {
            ExecuteQuitGame();
        });

        // 취소 버튼
        CreateButton(_quitPopupObj.transform, "Btn_CancelQuit", "취소", new Vector2(140, -60), new Vector2(240, 70), new Color(0.45f, 0.45f, 0.45f), () =>
        {
            ToggleQuitPopup();
        });

        _quitPopupObj.SetActive(false);
    }

    private void ToggleQuitPopup()
    {
        if (_quitPopupObj != null)
        {
            _quitPopupObj.SetActive(!_quitPopupObj.activeSelf);
        }
    }

    public void ExecuteQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public async void StartOnlineMatchmaking()
    {
        IsSinglePlayMode = false;

        if (_statusText != null)
        {
            _statusText.text = "<color=yellow>[매칭 중] 비어있는 1:1 대전 룸을 검색/생성 중입니다...</color>";
        }

        await StartFusionSession(GameMode.AutoHostOrClient, string.Empty, 2);
    }

    public async void StartSinglePlay(PlayerController.Role chosenRole)
    {
        IsSinglePlayMode = true;
        SinglePlayRole = chosenRole;

        if (_statusText != null)
        {
            _statusText.text = $"<color=cyan>[{chosenRole}] 단독 연습 모드로 진입 중...</color>";
        }

        // 혼자하기 모드도 GameMode.Single 러너를 띄워 NetworkObject 권한을 온전히 획득!
        await StartFusionSession(GameMode.Single, "SingleSandboxRoom", 1);
    }

    private async Task StartFusionSession(GameMode mode, string roomName, int playerCount)
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

        _runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{_gameSceneName}.unity"));
        if (!scene.IsValid)
        {
            scene = SceneRef.FromIndex(1);
        }

        StartGameArgs args = new StartGameArgs()
        {
            GameMode = mode,
            SessionName = roomName,
            PlayerCount = playerCount,
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            IsOpen = true,
            IsVisible = true
        };

        Debug.Log($"<color=cyan>[Lobby] 세션 시작 시도: Mode={mode}, TargetPlayers={playerCount}</color>");
        var result = await _runner.StartGame(args);

        if (!result.Ok)
        {
            Debug.LogError($"[Lobby] 접속 실패 사유: {result.ShutdownReason}");
            if (_statusText != null)
            {
                _statusText.text = $"<color=red>접속 실패: {result.ShutdownReason}\n다시 시도해 주십시오.</color>";
            }

            if (_runner != null)
            {
                Destroy(_runner);
                _runner = null;
            }
        }
        else
        {
            Debug.Log($"<color=green>[Lobby] 세션 접속 성공! RoomName={_runner.SessionInfo.Name}, IsServer={_runner.IsServer}</color>");
        }
    }
}