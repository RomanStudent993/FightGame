using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Финальная сцена после смерти босса на Level-3: камера, кадры и текст.</summary>
[DefaultExecutionOrder(11000)]
public class GameFinaleController : MonoBehaviour
{
    const string MainMenuSceneName = "StartMenu";

#if UNITY_EDITOR
    const string EditorEndFolder = "Assets/FightGame/prefabs/End/";
    const string EditorBattleBackgroundPath = "Assets/FightGame/prefabs/Level-1/Sprites/unity_background.png";
    const string EditorHeavyBanditPath = "Assets/FightGame/prefabs/Level-1/Prefabs/HeavyBandit.prefab";
    const string EditorEvilWizardPath = "Assets/FightGame/prefabs/Level-2/Prefabs/EvilWizard.prefab";
#endif

    static readonly Color StoryColor = new Color(0.94f, 0.86f, 0.62f, 1f);
    static readonly Color EndTitleColor = new Color(0.94f, 0.86f, 0.62f, 1f);

    static GameFinaleController _instance;
    static bool _sceneHookRegistered;

    [Header("Тайминг")]
    [SerializeField] float delayBeforeFinale = 0.6f;
    [SerializeField] float fadeDarkDuration = 1.1f;
    [SerializeField] float maxDarkOverlayAlpha = 0.78f;
    [SerializeField] float worldVisibleOverlayAlpha = 0.38f;
    [SerializeField] float charsPerSecond = 28f;
    [SerializeField] float pauseAfterPeriod = 0.3f;
    [SerializeField] float pauseAfterParagraph = 0.5f;
    [SerializeField] float holdAfterBeat = 0.35f;
    [SerializeField] float villagersPhotoHoldSeconds = 0.75f;
    [SerializeField] float imageFadeDuration = 0.55f;
    [SerializeField] float endTitleFadeDuration = 1.2f;
    [SerializeField] float endHoldSeconds = 3f;
    [SerializeField] float endFadeOutDuration = 1.1f;
    [SerializeField] int storyFontSize = 28;
    [SerializeField] int endTitleFontSize = 112;

    [Header("Камера — босс")]
    [SerializeField] float bossCloseOrthoSize = 1.85f;
    [SerializeField] float bossFocusHeightFromFeet = 1.3f;
    [SerializeField] float bossCameraMoveDuration = 1.05f;
    [SerializeField] float waitBossDeathFrameSeconds = 0.45f;

    [Header("Камера — кадр Level 1")]
    [SerializeField] Vector3 flashbackCameraPosition = new Vector3(0f, 4.07f, -10f);
    [SerializeField] float flashbackOrthoSize = 5f;
    [SerializeField] float flashbackCameraMoveDuration = 0.9f;
    [Tooltip("Доп. сдвиг мага по Y на кадре «Воины все были убиты» (если спрайт чуть утоплен).")]
    [SerializeField] float flashbackWizardFeetRaise = 0f;
    [SerializeField] float flashbackFloorContactY = -0.57f;

    [Header("Префабы (опционально)")]
    [SerializeField] GameObject heavyBanditPrefab;
    [SerializeField] GameObject evilWizardPrefab;
    [SerializeField] Sprite battleBackgroundSprite;

    Camera _camera;
    Vector3 _cameraStartPos;
    float _cameraStartOrtho;
    Transform _boss;
    Transform _player;
    GameObject _flashbackStage;
    readonly List<GameObject> _hiddenSceneRoots = new List<GameObject>();
    Canvas _uiCanvas;
    Image _darkOverlay;
    Image _photoImage;
    Text _storyText;
    Text _endTitleText;
    Coroutine _routine;
    bool _finaleStarted;

    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHook()
    {
        if (_sceneHookRegistered)
            return;

        _sceneHookRegistered = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsFinaleScene(scene))
        {
            if (_instance != null)
                Destroy(_instance.gameObject);
            return;
        }

        EnsureForScene(scene);
    }

    static void EnsureForScene(Scene scene)
    {
        if (!IsFinaleScene(scene))
            return;

        if (FindAnyObjectByType<GameFinaleController>(FindObjectsInactive.Include) != null)
            return;

        new GameObject("GameFinaleController").AddComponent<GameFinaleController>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    void OnEnable()
    {
        SimpleHealth.Died += OnCharacterDied;
    }

    void OnDisable()
    {
        SimpleHealth.Died -= OnCharacterDied;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        if (!IsPlaying)
            return;

        IsPlaying = false;
    }

    static bool IsFinaleScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        string name = scene.name;
        string path = scene.path.Replace('\\', '/');
        return name == "Level-3" || path.EndsWith("/Level-3.unity", System.StringComparison.OrdinalIgnoreCase);
    }

    void OnCharacterDied(GameObject who)
    {
        if (_finaleStarted || who == null)
            return;

        if (!IsBossDeath(who))
            return;

        _finaleStarted = true;
        IsPlaying = true;
        _boss = who.transform.root;
        LockGameplay();

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(FinaleRoutine());
    }

    static bool IsBossDeath(GameObject who)
    {
        Transform root = who.transform.root;
        if (root.GetComponent<BossEnemyBridge>() != null)
            return true;

        return root.name == "Boss";
    }

    IEnumerator FinaleRoutine()
    {
        _instance = this;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delayBeforeFinale));

        if (!TryResolveRefs())
        {
            Debug.LogWarning("GameFinaleController: не найдены камера/босс — финал пропущен.");
            FinishFinaleAndReturnToMenu();
            yield break;
        }

        HideMissionHud();
        BuildUi();
        CaptureCameraDefaults();

        yield return PlaySegments();
        FinishFinaleAndReturnToMenu();
    }

    bool TryResolveRefs()
    {
        _camera = Camera.main;
        if (_camera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>();
            _camera = cameras.Length > 0 ? cameras[0] : null;
        }

        if (_boss == null)
        {
            BossEnemyBridge bridge = FindAnyObjectByType<BossEnemyBridge>();
            if (bridge != null)
                _boss = bridge.transform;
        }

        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null)
        {
            HeroKnight hero = FindAnyObjectByType<HeroKnight>();
            if (hero != null)
                playerGo = hero.gameObject;
        }

        if (playerGo != null)
            _player = playerGo.transform;

        return _camera != null && _boss != null;
    }

    void CaptureCameraDefaults()
    {
        _cameraStartPos = _camera.transform.position;
        _cameraStartOrtho = _camera.orthographicSize;
    }

    IEnumerator WaitForBossDeathFrame()
    {
        Animator anim = _boss != null ? _boss.GetComponent<Animator>() : null;
        SimpleHealth hp = _boss != null ? _boss.GetComponent<SimpleHealth>() : null;
        float timeout = 2.5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (anim != null)
            {
                AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
                bool inDeath = st.IsName("Death") || st.IsName("Boss_Death") || st.IsName("DeathNoBlood");
                if (inDeath && (st.normalizedTime >= 0.85f || anim.speed < 0.01f))
                    break;
                if (hp != null && hp.IsDead && anim.speed < 0.01f)
                    break;
            }
            else if (hp != null && hp.IsDead)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float hold = Mathf.Max(0f, waitBossDeathFrameSeconds);
        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);
    }

    IEnumerator ShowBossCloseUp()
    {
        yield return WaitForBossDeathFrame();

        if (_player != null)
            _player.gameObject.SetActive(false);

        SetWorldVisualMode(true);
        yield return FadeOverlay(0f, maxDarkOverlayAlpha, fadeDarkDuration);
        yield return FadeOverlay(maxDarkOverlayAlpha, worldVisibleOverlayAlpha, 0.45f);
        yield return ZoomCameraToBoss();
    }

    IEnumerator ZoomCameraToBoss()
    {
        Vector3 fromPos = _camera.transform.position;
        float fromSize = _camera.orthographicSize;
        Vector3 focus = GetFocusPoint(_boss);
        Vector3 toPos = new Vector3(focus.x, focus.y, _cameraStartPos.z);
        float duration = Mathf.Max(0.05f, bossCameraMoveDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / duration));
            _camera.transform.position = Vector3.Lerp(fromPos, toPos, k);
            _camera.orthographicSize = Mathf.Lerp(fromSize, bossCloseOrthoSize, k);
            yield return null;
        }

        _camera.transform.position = toPos;
        _camera.orthographicSize = bossCloseOrthoSize;
    }

    IEnumerator PlaySegments()
    {
        FinaleSegment[] segments =
        {
            new FinaleSegment("Гниющий Король упал. Доспехи осыпались ржавой пылью.", FinaleVisualKind.KeepBossShot),
            new FinaleSegment("Воины все были убиты.", FinaleVisualKind.DeadWarriors),
            new FinaleSegment("Принцесса выглянула из-за прохода и пошла к выходу.", FinaleVisualKind.Photo, "hide-princess"),
            new FinaleSegment("Дождь стих. В разбитых окнах забрезжил свет.", FinaleVisualKind.Photo, "villagers1"),
            new FinaleSegment("Люди выходили на порог.", FinaleVisualKind.Photo, "villagers2"),
            new FinaleSegment("Страх выдохнул.", FinaleVisualKind.Photo, "villagers3"),
            new FinaleSegment("Это была великая победа для всего замка.", FinaleVisualKind.Photo, "princess"),
            new FinaleSegment(string.Empty, FinaleVisualKind.TheEnd),
        };

        for (int i = 0; i < segments.Length; i++)
        {
            FinaleSegment segment = segments[i];
            Coroutine visualRoutine = StartCoroutine(ApplySegmentVisual(segment));

            if (!string.IsNullOrEmpty(segment.Text))
            {
                var line = new StringBuilder(segment.Text.Length);
                for (int c = 0; c < segment.Text.Length; c++)
                {
                    line.Append(segment.Text[c]);
                    _storyText.text = line.ToString();
                    yield return WaitCharDelay(segment.Text[c], segment.Text, c);
                }
            }

            if (visualRoutine != null)
                yield return visualRoutine;

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, GetHoldForSegment(segment)));
            _storyText.text = string.Empty;
        }
    }

    float GetHoldForSegment(FinaleSegment segment)
    {
        if (segment.PhotoName == "villagers2" || segment.PhotoName == "villagers3")
            return villagersPhotoHoldSeconds;

        return holdAfterBeat;
    }

    IEnumerator ApplySegmentVisual(FinaleSegment segment)
    {
        switch (segment.Visual)
        {
            case FinaleVisualKind.KeepBossShot:
                yield return ShowBossCloseUp();
                yield break;

            case FinaleVisualKind.DeadWarriors:
                yield return ShowDeadWarriorsShot();
                yield break;

            case FinaleVisualKind.Photo:
                yield return ShowPhoto(segment.PhotoName);
                yield break;

            case FinaleVisualKind.TheEnd:
                yield return PlayTheEndSequence();
                yield break;
        }
    }

    IEnumerator ShowDeadWarriorsShot()
    {
        SetWorldVisualMode(true);
        yield return FadeOverlay(_darkOverlay.color.a, maxDarkOverlayAlpha, 0.35f);

        HideLevelSceneForFlashback();
        BuildFlashbackStage();

        Vector3 fromPos = _camera.transform.position;
        float fromSize = _camera.orthographicSize;
        float duration = Mathf.Max(0.05f, flashbackCameraMoveDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / duration));
            _camera.transform.position = Vector3.Lerp(fromPos, flashbackCameraPosition, k);
            _camera.orthographicSize = Mathf.Lerp(fromSize, flashbackOrthoSize, k);
            yield return null;
        }

        _camera.transform.position = flashbackCameraPosition;
        _camera.orthographicSize = flashbackOrthoSize;
        yield return FadeOverlay(maxDarkOverlayAlpha, worldVisibleOverlayAlpha, 0.4f);
    }

    IEnumerator ShowPhoto(string photoName)
    {
        SetWorldVisualMode(false);
        Sprite sprite = ResolveEndSprite(photoName);
        if (sprite == null)
        {
            Debug.LogWarning($"GameFinaleController: не найдено фото «{photoName}».");
            yield break;
        }

        if (sprite.texture != null)
            sprite.texture.filterMode = FilterMode.Point;

        _photoImage.sprite = sprite;
        _photoImage.enabled = true;

        float fromA = _photoImage.color.a;
        float duration = Mathf.Max(0.05f, imageFadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / duration));
            _photoImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(fromA, 1f, k));
            _darkOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(_darkOverlay.color.a, maxDarkOverlayAlpha, k));
            yield return null;
        }

        _photoImage.color = Color.white;
        _darkOverlay.color = new Color(0f, 0f, 0f, maxDarkOverlayAlpha);
    }

    IEnumerator PlayTheEndSequence()
    {
        float duration = Mathf.Max(0.05f, imageFadeDuration);
        float startPhoto = _photoImage.color.a;
        float startDark = _darkOverlay.color.a;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / duration));
            _photoImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(startPhoto, 0f, k));
            _darkOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(startDark, 1f, k));
            yield return null;
        }

        _photoImage.enabled = false;
        _darkOverlay.color = new Color(0f, 0f, 0f, 1f);
        _storyText.gameObject.SetActive(false);

        _endTitleText.gameObject.SetActive(true);
        Color c = EndTitleColor;
        c.a = 0f;
        _endTitleText.color = c;

        float titleDuration = Mathf.Max(0.05f, endTitleFadeDuration);
        t = 0f;
        while (t < titleDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / titleDuration));
            c.a = k;
            _endTitleText.color = c;
            yield return null;
        }

        _endTitleText.color = EndTitleColor;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, endHoldSeconds));

        float fadeOut = Mathf.Max(0.05f, endFadeOutDuration);
        t = 0f;
        Color titleStart = EndTitleColor;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / fadeOut));
            Color faded = titleStart;
            faded.a = 1f - k;
            _endTitleText.color = faded;
            yield return null;
        }

        _endTitleText.gameObject.SetActive(false);
    }

    void BuildFlashbackStage()
    {
        if (_flashbackStage != null)
            return;

        Sprite bg = ResolveBattleBackground();
        GameObject banditPrefab = ResolveHeavyBanditPrefab();
        GameObject wizardPrefab = ResolveEvilWizardPrefab();

        _flashbackStage = new GameObject("FinaleFlashbackStage");

        if (bg != null)
        {
            GameObject bgGo = new GameObject("FlashbackBackground");
            bgGo.transform.SetParent(_flashbackStage.transform, false);
            SpriteRenderer sr = bgGo.AddComponent<SpriteRenderer>();
            sr.sprite = bg;
            sr.sortingOrder = 0;
            bgGo.transform.position = new Vector3(0f, 3.99f, 0f);
            bgGo.transform.localScale = new Vector3(1.3857391f, 1.32132f, 1f);
        }
        else
        {
            Debug.LogWarning("GameFinaleController: не найден фон Level 1 для флешбэка.");
        }

        if (banditPrefab != null)
        {
            GameObject bandit = SpawnFlashbackCorpse(banditPrefab, new Vector3(2.8f, 0f, 0f), 1.25f, "FinaleDeadBandit");
            AlignCharacterToFloor(bandit, flashbackFloorContactY);

            if (wizardPrefab != null)
                SpawnFlashbackWizard(wizardPrefab, bandit);
        }
        else if (wizardPrefab != null)
        {
            Debug.LogWarning("GameFinaleController: не найден префаб HeavyBandit.");
            SpawnFlashbackWizard(wizardPrefab, null);
        }
    }

    GameObject SpawnFlashbackCorpse(GameObject prefab, Vector3 position, float uniformScale, string objectName)
    {
        GameObject corpse = Instantiate(prefab, position, Quaternion.identity, _flashbackStage.transform);
        corpse.name = objectName;
        corpse.transform.localScale = Vector3.one * uniformScale;
        ForceDeathPose(corpse);
        SettleDeathAnimation(corpse);
        return corpse;
    }

    void SpawnFlashbackWizard(GameObject wizardPrefab, GameObject referenceBandit)
    {
        GameObject wizard = SpawnFlashbackCorpse(wizardPrefab, new Vector3(-2.4f, 0f, 0f), 2.8f, "FinaleDeadWizard");
        float floorY = flashbackFloorContactY + flashbackWizardFeetRaise;
        if (referenceBandit != null)
        {
            Transform banditSensor = FindGroundSensor(referenceBandit);
            if (banditSensor != null)
                floorY = banditSensor.position.y + flashbackWizardFeetRaise;
        }

        AlignCharacterToFloor(wizard, floorY);
        SettleDeathAnimation(wizard);
        AlignCharacterToFloor(wizard, floorY);
    }

    static void SettleDeathAnimation(GameObject go)
    {
        Animator anim = go != null ? go.GetComponentInChildren<Animator>(true) : null;
        if (anim == null)
            return;

        float previousSpeed = anim.speed;
        anim.speed = 1f;
        for (int i = 0; i < 10; i++)
            anim.Update(0.025f);
        anim.speed = previousSpeed;
    }

    static Transform FindGroundSensor(GameObject go)
    {
        if (go == null)
            return null;

        Transform direct = go.transform.Find("GroundSensor");
        if (direct != null)
            return direct;

        Transform[] children = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == "GroundSensor")
                return children[i];
        }

        return null;
    }

    static void AlignCharacterToFloor(GameObject go, float targetContactY)
    {
        if (go == null)
            return;

        Transform groundSensor = FindGroundSensor(go);
        if (groundSensor != null)
        {
            float delta = targetContactY - groundSensor.position.y;
            if (Mathf.Abs(delta) > 0.0001f)
                go.transform.position += new Vector3(0f, delta, 0f);
            return;
        }

        AlignSpriteFeetToY(go, targetContactY);
    }

    static void ForceDeathPose(GameObject enemy)
    {
        if (enemy == null)
            return;

        foreach (MonoBehaviour behaviour in enemy.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        foreach (Collider2D col in enemy.GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;

        foreach (Rigidbody2D rb in enemy.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        foreach (SpriteRenderer sprite in enemy.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.sortingOrder = 20;

        Animator anim = enemy.GetComponentInChildren<Animator>(true);
        if (anim == null)
            return;

        anim.enabled = true;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.applyRootMotion = false;
        anim.Rebind();
        anim.Update(0f);

        if (HasAnimatorTrigger(anim, "Death"))
        {
            anim.ResetTrigger("Death");
            anim.SetTrigger("Death");
            anim.Update(0.05f);
        }

        if (HasAnimatorState(anim, "Death"))
            anim.Play("Death", 0, 0.99f);
        else if (HasAnimatorState(anim, "Wizard_Death"))
            anim.Play("Wizard_Death", 0, 0.99f);
        else if (HasAnimatorState(anim, "HeavyBandit_Death"))
            anim.Play("HeavyBandit_Death", 0, 0.99f);

        anim.speed = 1f;
        SettleDeathAnimation(enemy);
        anim.speed = 0f;
    }

    static bool HasAnimatorTrigger(Animator anim, string triggerName)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
            return false;

        for (int i = 0; i < anim.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = anim.GetParameter(i);
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                return true;
        }

        return false;
    }

    static float GetSpriteFeetY(GameObject go)
    {
        if (go == null)
            return 0f;

        float feetY = float.PositiveInfinity;
        SpriteRenderer[] sprites = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null || !sr.enabled || sr.sprite == null)
                continue;

            feetY = Mathf.Min(feetY, sr.bounds.min.y);
        }

        if (!float.IsPositiveInfinity(feetY))
            return feetY;

        Collider2D col = go.GetComponentInChildren<Collider2D>(true);
        if (col != null)
            return col.bounds.min.y;

        return go.transform.position.y;
    }

    static void AlignSpriteFeetToY(GameObject go, float targetFeetY)
    {
        if (go == null)
            return;

        float delta = targetFeetY - GetSpriteFeetY(go);
        if (Mathf.Abs(delta) > 0.0001f)
            go.transform.position += new Vector3(0f, delta, 0f);
    }

    static bool HasAnimatorState(Animator anim, string stateName)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
            return false;

        int hash = Animator.StringToHash(stateName);
        return anim.HasState(0, hash);
    }

    void HideLevelSceneForFlashback()
    {
        _hiddenSceneRoots.Clear();
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root == gameObject || root == _flashbackStage)
                continue;
            if (root.name == "Main Camera" || root.name == "FinaleFlashbackStage" || root.name == "GameFinale_UI")
                continue;
            if (root.GetComponent<Canvas>() != null && root.name.Contains("Finale"))
                continue;
            if (root.GetComponent<GameFinaleController>() != null)
                continue;

            _hiddenSceneRoots.Add(root);
            root.SetActive(false);
        }
    }

    void SetWorldVisualMode(bool worldVisible)
    {
        if (_photoImage != null)
        {
            if (!worldVisible)
            {
                _photoImage.enabled = false;
                _photoImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        if (_storyText != null)
            _storyText.gameObject.SetActive(true);
    }

    void BuildUi()
    {
        if (_uiCanvas != null)
            return;

        _uiCanvas = ContinuePrompt.CreateTransitionCanvas("GameFinale_UI", 1600);
        Transform root = _uiCanvas.transform;

        RectTransform darkRt = CreateStretchRect(root, "DarkOverlay");
        _darkOverlay = darkRt.gameObject.AddComponent<Image>();
        _darkOverlay.color = new Color(0f, 0f, 0f, 0f);
        _darkOverlay.raycastTarget = false;

        RectTransform photoRt = CreateStretchRect(root, "Photo");
        _photoImage = photoRt.gameObject.AddComponent<Image>();
        _photoImage.preserveAspect = false;
        _photoImage.type = Image.Type.Simple;
        _photoImage.color = new Color(1f, 1f, 1f, 0f);
        _photoImage.enabled = false;
        _photoImage.raycastTarget = false;

        RectTransform textRt = CreateRect(root, "StoryText",
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(-72f, 200f));
        _storyText = textRt.gameObject.AddComponent<Text>();
        _storyText.font = ResolveStoryFont("Конец");
        _storyText.fontSize = storyFontSize;
        _storyText.lineSpacing = 0.92f;
        _storyText.color = StoryColor;
        _storyText.alignment = TextAnchor.LowerLeft;
        _storyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _storyText.verticalOverflow = VerticalWrapMode.Overflow;
        _storyText.raycastTarget = false;
        _storyText.text = "";

        RectTransform endRt = CreateStretchRect(root, "EndTitle");
        _endTitleText = endRt.gameObject.AddComponent<Text>();
        _endTitleText.font = ResolveStoryFont("Конец");
        _endTitleText.fontSize = endTitleFontSize;
        _endTitleText.fontStyle = FontStyle.Bold;
        _endTitleText.color = EndTitleColor;
        _endTitleText.alignment = TextAnchor.MiddleCenter;
        _endTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _endTitleText.verticalOverflow = VerticalWrapMode.Overflow;
        _endTitleText.raycastTarget = false;
        _endTitleText.text = "Конец";
        _endTitleText.gameObject.SetActive(false);
    }

    void HideMissionHud()
    {
        GameObject hud = GameObject.Find("MissionObjective_HUD");
        if (hud != null)
            hud.SetActive(false);
    }

    void LockGameplay()
    {
        foreach (HeroKnight hero in FindObjectsByType<HeroKnight>())
            hero.enabled = false;

        foreach (PlayerAttackDamage atk in FindObjectsByType<PlayerAttackDamage>())
            atk.enabled = false;

        foreach (PlayerShieldDefense shield in FindObjectsByType<PlayerShieldDefense>())
            shield.enabled = false;

        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>())
            ai.enabled = false;

        foreach (EnemyContactDamage contact in FindObjectsByType<EnemyContactDamage>())
            contact.enabled = false;

        foreach (Rigidbody2D rb in FindObjectsByType<Rigidbody2D>())
        {
            if (rb.CompareTag("Player"))
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }
    }

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOut(Mathf.Clamp01(t / duration));
            float a = Mathf.Lerp(from, to, k);
            _darkOverlay.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }

        _darkOverlay.color = new Color(0f, 0f, 0f, to);
    }

    IEnumerator WaitCharDelay(char c, string full, int index)
    {
        if (index >= full.Length - 1)
            yield break;

        float delay = DelayFor(c, full, index);
        if (delay <= 0f)
            yield break;

        float end = Time.unscaledTime + delay;
        while (Time.unscaledTime < end)
            yield return null;
    }

    float DelayFor(char c, string full, int index)
    {
        float baseDelay = 1f / Mathf.Max(1f, charsPerSecond);

        if (c == '\n')
        {
            bool paragraphBreak = index > 0 && full[index - 1] == '\n';
            return paragraphBreak ? pauseAfterParagraph : baseDelay * 0.5f;
        }

        if (c == '.' || c == '!' || c == '?')
            return baseDelay + pauseAfterPeriod;

        if (c == ',' || c == ';' || c == ':')
            return baseDelay + pauseAfterPeriod * 0.35f;

        return baseDelay;
    }

    void FinishFinaleAndReturnToMenu()
    {
        IsPlaying = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            SceneManager.LoadScene(MainMenuSceneName);
        else
            Debug.LogWarning($"GameFinaleController: сцена «{MainMenuSceneName}» недоступна.");
    }

    Vector3 GetFocusPoint(Transform target)
    {
        Collider2D col = target != null ? target.GetComponent<Collider2D>() : null;
        if (col != null)
        {
            Bounds b = col.bounds;
            return new Vector3(b.center.x, b.min.y + bossFocusHeightFromFeet, 0f);
        }

        Vector3 p = target.position;
        p.y += bossFocusHeightFromFeet;
        return p;
    }

    Sprite ResolveEndSprite(string baseName)
    {
        Sprite sprite = Resources.Load<Sprite>($"End/{baseName}");
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{EditorEndFolder}{baseName}.jpeg");
        if (sprite != null)
            return sprite;
#endif
        return null;
    }

    Sprite ResolveBattleBackground()
    {
        if (battleBackgroundSprite != null)
            return battleBackgroundSprite;

        Sprite sprite = Resources.Load<Sprite>("Finale/unity_background");
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorBattleBackgroundPath);
        if (sprite != null)
            return sprite;
#endif
        return null;
    }

    GameObject ResolveHeavyBanditPrefab()
    {
        if (heavyBanditPrefab != null)
            return heavyBanditPrefab;

        GameObject prefab = Resources.Load<GameObject>("Finale/HeavyBandit");
        if (prefab != null)
            return prefab;

#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorHeavyBanditPath);
        if (prefab != null)
            return prefab;
#endif
        return null;
    }

    GameObject ResolveEvilWizardPrefab()
    {
        if (evilWizardPrefab != null)
            return evilWizardPrefab;

        GameObject prefab = Resources.Load<GameObject>("Finale/EvilWizard");
        if (prefab != null)
            return prefab;

#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorEvilWizardPath);
        if (prefab != null)
            return prefab;
#endif
        return null;
    }

    static Font ResolveStoryFont(string sample)
    {
        GameFont.Reload();
        Font font = GameFont.ResolveForText(sample, 32);
        return font != null ? font : GameFont.BuiltInFallback;
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    static RectTransform CreateStretchRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform CreateRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
        return rt;
    }

    readonly struct FinaleSegment
    {
        public readonly string Text;
        public readonly FinaleVisualKind Visual;
        public readonly string PhotoName;

        public FinaleSegment(string text, FinaleVisualKind visual, string photoName = null)
        {
            Text = text;
            Visual = visual;
            PhotoName = photoName;
        }
    }

    enum FinaleVisualKind
    {
        KeepBossShot,
        DeadWarriors,
        Photo,
        TheEnd,
    }
}
