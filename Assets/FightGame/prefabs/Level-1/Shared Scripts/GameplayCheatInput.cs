using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Ctrl+Shift+Пробел: в бою — мгновенная смерть врагов; в обучении — сразу конец. Ctrl+Shift+G: смерть игрока (только после начала боя).</summary>
[DefaultExecutionOrder(12000)]
public class GameplayCheatInput : MonoBehaviour
{
    static bool _sceneHookRegistered;

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
        if (!IsCheatScene(scene))
        {
            GameplayCheatInput existing = FindAnyObjectByType<GameplayCheatInput>(FindObjectsInactive.Include);
            if (existing != null)
                Destroy(existing.gameObject);
            return;
        }

        EnsureForScene(scene);
    }

    static void EnsureForScene(Scene scene)
    {
        if (!IsCheatScene(scene))
            return;

        if (FindAnyObjectByType<GameplayCheatInput>(FindObjectsInactive.Include) != null)
            return;

        new GameObject(nameof(GameplayCheatInput)).AddComponent<GameplayCheatInput>();
    }

    static bool IsCheatScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        string name = scene.name;
        if (name == "StartMenu" || name.Contains("Menu"))
            return false;

        string path = scene.path.Replace('\\', '/');
        return name == "EducationDemo" || name == "battle" || name == "Level-2" || name == "Level-3"
            || path.EndsWith("/EducationDemo.unity", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/battle.unity", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Level-2.unity", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Level-3.unity", System.StringComparison.OrdinalIgnoreCase);
    }

    void Update()
    {
        if (GameFinaleController.IsPlaying || GameDeathController.IsShowing || GamePauseController.IsPaused)
            return;

        if (GameplayCheatKeys.WasCtrlShiftGPressed())
        {
            if (!BattleIntroController.FightStarted)
                return;

            GameplayCheatKeys.KillPlayerInScene();
            return;
        }

        if (!GameplayCheatKeys.WasCtrlShiftSpacePressed())
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name == "EducationDemo")
        {
            TutorialQuestController tutorial = FindAnyObjectByType<TutorialQuestController>();
            if (tutorial != null)
                tutorial.ForceCompleteTutorial();
            return;
        }

        if (!BattleIntroController.FightStarted)
        {
            BattleIntroController.ForceSkipIntroAndBeginFight();
            GameplayCheatKeys.KillAllEnemiesInScene(force: true);
            return;
        }

        GameplayCheatKeys.KillAllEnemiesInScene();
    }
}

public static class GameplayCheatKeys
{
    public static bool WasCtrlShiftSpacePressed()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (ctrl && shift && Input.GetKeyDown(KeyCode.Space))
            return true;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            bool ctrlNew = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            bool shiftNew = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            if (ctrlNew && shiftNew && Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;
        }
#endif
        return false;
    }

    public static bool WasCtrlShiftGPressed()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (ctrl && shift && Input.GetKeyDown(KeyCode.G))
            return true;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            bool ctrlNew = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            bool shiftNew = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            if (ctrlNew && shiftNew && Keyboard.current.gKey.wasPressedThisFrame)
                return true;
        }
#endif
        return false;
    }

    public static void KillAllEnemiesInScene(bool force = false)
    {
        SimpleHealth[] all = Object.FindObjectsByType<SimpleHealth>();
        for (int i = 0; i < all.Length; i++)
        {
            SimpleHealth health = all[i];
            if (health == null || health.IsDead || !IsKillableEnemy(health.gameObject))
                continue;

            if (force)
                health.ForceKill();
            else
                health.TakeDamage(int.MaxValue / 4);
        }
    }

    public static void KillPlayerInScene()
    {
        SimpleHealth[] all = Object.FindObjectsByType<SimpleHealth>();
        for (int i = 0; i < all.Length; i++)
        {
            SimpleHealth health = all[i];
            if (health == null || health.IsDead || !IsPlayer(health.gameObject))
                continue;

            health.ForceKill();
        }
    }

    static bool IsPlayer(GameObject who)
    {
        if (who == null)
            return false;

        Transform root = who.transform.root;
        if (root.CompareTag("Player"))
            return true;

        return root.GetComponentInChildren<HeroKnight>(true) != null;
    }

    static bool IsKillableEnemy(GameObject who)
    {
        if (who == null)
            return false;

        Transform root = who.transform.root;
        if (root.CompareTag("Player"))
            return false;
        if (root.GetComponentInChildren<HeroKnight>(true) != null)
            return false;
        if (root.CompareTag("Enemy"))
            return true;
        if (root.GetComponentInChildren<EnemyAI>(true) != null)
            return true;
        if (root.GetComponentInChildren<BossEnemyBridge>(true) != null)
            return true;

        return false;
    }
}
