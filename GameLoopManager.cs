using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// [GameLoopManager] 삭제되었던 SafeLobbyLoader를 완벽히 복원하여 CS0103 에러를 박멸하고,
/// 매 라운드 TacticalBoxSpawner를 호출하여 아이템이 정상 리스폰되는 최종 매니저입니다.
/// </summary>
public class GameLoopManager : NetworkBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    public enum GameState
    {
        Waiting,
        Playing,
        RoundEnd,
        MatchEnd
    }

    [Networked] private GameState _netCurrentState { get; set; }
    private GameState _localCurrentState = GameState.Waiting;
    public GameState CurrentState
    {
        get => (Object != null && Object.IsValid) ? _netCurrentState : _localCurrentState;
        set
        {
            _localCurrentState = value;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) _netCurrentState = value;
        }
    }

    public bool IsExitPopupOpen { get; set; } = false;

    [Networked] private int _netCurrentHunterIndex { get; set; }
    private int _localCurrentHunterIndex = 1;
    public int CurrentHunterIndex
    {
        get => (Object != null && Object.IsValid) ? _netCurrentHunterIndex : _localCurrentHunterIndex;
        set
        {
            _localCurrentHunterIndex = value;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) _netCurrentHunterIndex = value;
        }
    }

    [Networked] private int _netRoundNumber { get; set; }
    private int _localRoundNumber = 1;
    public int RoundNumber
    {
        get => (Object != null && Object.IsValid) ? _netRoundNumber : _localRoundNumber;
        set
        {
            _localRoundNumber = value;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) _netRoundNumber = value;
        }
    }

    [Header("UI 텍스트 연결")]
    [SerializeField] private Text _centerNoticeText;
    [SerializeField] private Text _timerText;
    [SerializeField] private Text _scoreText;

    [Header("게임 룰 파라미터")]
    [SerializeField] private int _turnDuration = 10; 

    [Networked] public int P1Score { get; set; }
    [Networked] public int P2Score { get; set; }
    [Networked] public NetworkBool IsMatchStarted { get; set; }

    private Coroutine _turnCoroutine;
    private int _syncedTimer = 10;

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
        EnsureAutoHUD();

        if (LobbyManager.IsSinglePlayMode)
        {
            StartCoroutine(LocalInstantStartRoutine());
        }
        else
        {
            CurrentState = GameState.Waiting;
            ShowNoticeDirectly("상대방 접속을 기다리는 중입니다... (1/2)");
            if (_scoreText != null) _scoreText.text = "1:1 온라인 대전 매칭 대기 중...";
            if (_timerText != null) _timerText.text = "대기중";
        }
    }

    public override void Spawned()
    {
        EnsureAutoHUD();
        UpdateScoreUI();

        if (!LobbyManager.IsSinglePlayMode)
        {
            if (Object != null && Object.HasStateAuthority)
            {
                _netRoundNumber = 1;
                _netCurrentHunterIndex = 1;
                P1Score = 0;
                P2Score = 0;
                IsMatchStarted = false;
                
                StartCoroutine(CheckMatchStartConditions());
            }
            else
            {
                StartCoroutine(ClientWaitingWatcherRoutine());
            }
        }
    }

    private void Update()
    {
        if (!LobbyManager.IsSinglePlayMode && IsMatchStarted)
        {
            if (Runner == null || !Runner.IsRunning)
            {
                if (SceneManager.GetActiveScene().buildIndex != 0)
                {
                    SafeLobbyLoader.Execute(Runner);
                }
            }
        }
    }

    private IEnumerator ClientWaitingWatcherRoutine()
    {
        while (!IsMatchStarted)
        {
            int currentCount = (Runner != null) ? Runner.ActivePlayers.Count() : 1;
            ShowNoticeDirectly($"방장 룸 접속 완료! 대전 시작 대기 중... ({currentCount}/2)");
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator LocalInstantStartRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        CurrentState = GameState.Playing;
        SetupLocalPlayerDirectly();
        UpdateScoreUI();
        StartCoroutine(ExecuteTurnStartReloadSequence());
    }

    private void SetupLocalPlayerDirectly()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>().OrderBy(p => p.gameObject.name).ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            players[i].CurrentRole = (i == 0) ? LobbyManager.SinglePlayRole : PlayerController.Role.Hider;
            players[i].PlayerSlot = i + 1;
            players[i].AmmoCount = 20; 
            players[i].Health = 100;
            players[i].ApplyLaserVisualUpdate(TacticalItemType.None);
        }
    }

    private IEnumerator CheckMatchStartConditions()
    {
        while (!IsMatchStarted)
        {
            int currentCount = (Runner != null) ? Runner.ActivePlayers.Count() : 1;
            ShowNoticeDirectly($"공식 1:1 룸 접속 완료! 도전자 대기 중... ({currentCount}/2)");

            if (Runner != null && Runner.ActivePlayers.Count() >= 2)
            {
                IsMatchStarted = true;
                if (Object.HasStateAuthority)
                {
                    PlayerController[] players = FindObjectsOfType<PlayerController>().OrderBy(p => p.gameObject.name).ToArray();
                    var playerRefs = Runner.ActivePlayers.OrderBy(pr => pr.PlayerId).ToArray();
                    
                    for(int i = 0; i < players.Length && i < playerRefs.Length; i++)
                    {
                        players[i].Object.AssignInputAuthority(playerRefs[i]);
                    }
                }

                RPC_BroadcastMatchStartNotice();
                yield return new WaitForSeconds(1.0f); 

                if (MapGimmickBuilder.Instance != null)
                {
                    MapGimmickBuilder.Instance.RPC_ForceRebuildMap();
                }

                yield return new WaitForSeconds(0.5f);

                SetupMultiplayerRoles();
                StartNewTurn();
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastMatchStartNotice()
    {
        ShowNoticeDirectly("도전자 입장 완료! 1:1 대전을 시작합니다!");
    }

    private void SetupMultiplayerRoles()
    {
        if (!Object.HasStateAuthority) return; 

        PlayerController[] players = FindObjectsOfType<PlayerController>().OrderBy(p => p.gameObject.name).ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            players[i].PlayerSlot = i + 1;
            players[i].CurrentRole = (i == 0) ? PlayerController.Role.Hunter : PlayerController.Role.Hider;
            players[i].AmmoCount = 20; 
            players[i].Health = 100;
        }
        CurrentHunterIndex = 1;
        
        RPC_ForceRefreshClients();
        UpdateScoreUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ForceRefreshClients()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach(var p in players)
        {
            p.UpdateRoleAndTransform();
        }
    }

    public void StartNewTurn()
    {
        if (Object != null && !Object.HasStateAuthority) return;

        if (_turnCoroutine != null) StopCoroutine(_turnCoroutine);
        _turnCoroutine = StartCoroutine(TurnTimerRoutine());
    }

    private IEnumerator TurnTimerRoutine()
    {
        CurrentState = GameState.Playing;
        yield return StartCoroutine(ExecuteTurnStartReloadSequence());

        int timer = _turnDuration;
        while (timer > 0)
        {
            SetSyncedTime(timer);
            yield return new WaitForSeconds(1.0f);
            timer--;
        }

        SetSyncedTime(0);
        ShowNoticeDirectly("시간 종료! 히더 생존 성공!");
        RPC_BroadcastTimeOut();

        yield return new WaitForSeconds(1.5f);
        ApplyScore(false); 
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastTimeOut()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.SetActionLock(true);
            Animator anim = p.GetComponentInChildren<Animator>();
            if (anim != null && p.CurrentRole == PlayerController.Role.Hider)
            {
                anim.SetTrigger("Victory");
            }
        }
    }

    private IEnumerator ExecuteTurnStartReloadSequence()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        PlayerController currentHunter = players.FirstOrDefault(p => p.CurrentRole == PlayerController.Role.Hunter);

        if (currentHunter != null)
        {
            currentHunter.SetActionLock(true);

            Animator anim = currentHunter.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.ResetTrigger("Reload");
                anim.SetTrigger("Reload");
            }

            ShowNoticeDirectly($"[ROUND {RoundNumber}] 탄약 장전 중...");

            if (Object != null && Object.IsValid && Object.HasStateAuthority)
            {
                if (TacticalBoxSpawner.Instance != null) TacticalBoxSpawner.Instance.TrySpawnBoxForNewTurn();
            }
            else if (LobbyManager.IsSinglePlayMode) 
            {
                if (TacticalBoxSpawner.Instance != null) TacticalBoxSpawner.Instance.TrySpawnBoxForNewTurn();
            }

            yield return new WaitForSeconds(2.0f);

            currentHunter.SetActionLock(false);
            ShowNoticeDirectly("장전 완료! 사격 개시!");
            yield return new WaitForSeconds(0.8f);
            ShowNoticeDirectly("");
        }
    }

    public void ApplyScore(bool hiderKilled)
    {
        if (CurrentState == GameState.MatchEnd) return; 
        CurrentState = GameState.RoundEnd;

        if (_turnCoroutine != null)
        {
            StopCoroutine(_turnCoroutine);
            _turnCoroutine = null;
        }

        if (Runner == null || Runner.ActivePlayers.Count() < 2 || LobbyManager.IsSinglePlayMode)
        {
            StartCoroutine(DirectResetRoutine());
            return;
        }

        int winnerSlot = hiderKilled ? CurrentHunterIndex : (CurrentHunterIndex == 1 ? 2 : 1);
        if (winnerSlot == 1) P1Score++;
        else P2Score++;

        UpdateScoreUI();

        if (P1Score >= 2 || P2Score >= 2)
        {
            CurrentState = GameState.MatchEnd;
            StartCoroutine(MatchEndRoutine(P1Score >= 2 ? 1 : 2));
        }
        else
        {
            SwitchTurnRoles();
        }
    }

    private void SwitchTurnRoles()
    {
        if (Object != null && !Object.HasStateAuthority) return;

        RoundNumber++;
        CurrentHunterIndex = (CurrentHunterIndex == 1) ? 2 : 1;

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.CurrentRole = (p.CurrentRole == PlayerController.Role.Hunter) ? PlayerController.Role.Hider : PlayerController.Role.Hunter;
            p.AmmoCount = 20; 
            p.Health = 100;
            p.HasHiderShield = false;
            p.SetActionLock(false);
        }

        if (MapGimmickBuilder.Instance != null)
        {
            MapGimmickBuilder.Instance.RPC_ForceRebuildMap();
        }

        RPC_ForceRefreshClients(); 
        StartNewTurn();
    }

    private IEnumerator MatchEndRoutine(int winnerSlot)
    {
        ShowNoticeDirectly($"게임 종료!\n플레이어 {winnerSlot} 최종 승리!\n\n(상단 EXIT 버튼으로 언제든 로비로 돌아갈 수 있습니다)");
        yield break; 
    }

    private IEnumerator DirectResetRoutine()
    {
        yield return new WaitForSeconds(1.0f);

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.AmmoCount = 20; 
            p.Health = 100;
            p.HasHiderShield = false;
            p.SetActionLock(false);
        }

        if (MapGimmickBuilder.Instance != null)
        {
            MapGimmickBuilder.Instance.RPC_ForceRebuildMap();
        }

        CurrentState = GameState.Playing;
        StartCoroutine(ExecuteTurnStartReloadSequence());
    }

    private void UpdateScoreUI()
    {
        EnsureAutoHUD();
        if (_scoreText != null)
        {
            if (LobbyManager.IsSinglePlayMode) _scoreText.text = "[TEST MODE] 사격 연습장";
            else _scoreText.text = $"P1 (Hunter): {P1Score}   VS   P2 (Hider): {P2Score}";
        }
        if (_timerText != null && LobbyManager.IsSinglePlayMode) _timerText.text = "TIME: ∞";
    }

    public void SetSyncedTime(int seconds)
    {
        _syncedTimer = seconds;
        EnsureAutoHUD();
        if (_timerText != null) _timerText.text = $"TIME: {_syncedTimer}s";
    }

    private void ShowNoticeDirectly(string notice)
    {
        EnsureAutoHUD();
        if (_centerNoticeText != null)
        {
            _centerNoticeText.text = notice;
            _centerNoticeText.gameObject.SetActive(!string.IsNullOrEmpty(notice));
        }
    }

    private void EnsureAutoHUD()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("Auto_EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }

        if (_scoreText != null && _timerText != null && _centerNoticeText != null) return;

        GameObject canvasObj = new GameObject("Direct_HUD_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();
        Font defFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (_scoreText == null)
        {
            GameObject sObj = new GameObject("Direct_ScoreText");
            sObj.transform.SetParent(canvasObj.transform, false);
            _scoreText = sObj.AddComponent<Text>();
            _scoreText.font = defFont;
            _scoreText.fontSize = 32;
            _scoreText.fontStyle = FontStyle.Bold;
            _scoreText.alignment = TextAnchor.MiddleCenter;
            _scoreText.color = Color.cyan;
            _scoreText.raycastTarget = false;
            RectTransform sRect = sObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.5f, 1f); sRect.anchorMax = new Vector2(0.5f, 1f);
            sRect.pivot = new Vector2(0.5f, 1f); sRect.anchoredPosition = new Vector2(0, -30);
            sRect.sizeDelta = new Vector2(800, 50);
        }

        if (_timerText == null)
        {
            GameObject tObj = new GameObject("Direct_TimerText");
            tObj.transform.SetParent(canvasObj.transform, false);
            _timerText = tObj.AddComponent<Text>();
            _timerText.font = defFont;
            _timerText.fontSize = 28;
            _timerText.fontStyle = FontStyle.Bold;
            _timerText.alignment = TextAnchor.MiddleCenter;
            _timerText.color = Color.white;
            _timerText.raycastTarget = false;
            RectTransform tRect = tObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f); tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f); tRect.anchoredPosition = new Vector2(0, -80);
            tRect.sizeDelta = new Vector2(400, 45);
        }

        if (_centerNoticeText == null)
        {
            GameObject nObj = new GameObject("Direct_NoticeText");
            nObj.transform.SetParent(canvasObj.transform, false);
            _centerNoticeText = nObj.AddComponent<Text>();
            _centerNoticeText.font = defFont;
            _centerNoticeText.fontSize = 48;
            _centerNoticeText.fontStyle = FontStyle.Bold;
            _centerNoticeText.alignment = TextAnchor.MiddleCenter;
            _centerNoticeText.color = new Color(1.0f, 0.85f, 0.1f);
            _centerNoticeText.raycastTarget = false;
            RectTransform nRect = nObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.5f, 0.5f); nRect.anchorMax = new Vector2(0.5f, 0.5f);
            nRect.pivot = new Vector2(0.5f, 0.5f); nRect.anchoredPosition = new Vector2(0, 100);
            nRect.sizeDelta = new Vector2(1000, 120);
        }
    }
}

/// <summary>
/// ★ [핵심 복원]: CS0103 에러의 원인이었던 '안전 귀환 캡슐' 선언부 완벽 복구
/// </summary>
public class SafeLobbyLoader : MonoBehaviour
{
    private static bool _isExecuting = false;

    public static void Execute(NetworkRunner runner)
    {
        if (_isExecuting) return;
        _isExecuting = true;

        GameObject obj = new GameObject("SafeLobbyLoader");
        DontDestroyOnLoad(obj);
        obj.AddComponent<SafeLobbyLoader>().StartCoroutine(Routine(runner, obj));
    }

    private static IEnumerator Routine(NetworkRunner runner, GameObject obj)
    {
        CreateBlackLoadingScreen();

        if (runner != null && runner.IsRunning)
        {
            runner.Shutdown();
            yield return new WaitForSeconds(0.5f);
        }

        SceneManager.LoadScene(0);
        _isExecuting = false;
        Destroy(obj);
    }

    private static void CreateBlackLoadingScreen()
    {
        GameObject canvasObj = new GameObject("ShutdownCanvas");
        DontDestroyOnLoad(canvasObj);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        
        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bg = bgObj.AddComponent<UnityEngine.UI.Image>();
        bg.color = Color.black;
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.sizeDelta = Vector2.zero;
        bg.rectTransform.anchoredPosition = Vector2.zero;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Text txt = textObj.AddComponent<UnityEngine.UI.Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = "로비로 안전하게 귀환 중입니다...";
        txt.fontSize = 40;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800, 100);
        rt.anchoredPosition = Vector2.zero;

        Destroy(canvasObj, 1.5f); 
    }
}