using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// TestWolf 开发场景的流程控制：显示调试 HUD，
/// 当羊群成员数归零时停止生成狼并显示 Game Over，按 R 重开。
/// </summary>
[DisallowMultipleComponent]
public sealed class WolfTestGameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private FlockMovementController flockMovement;
    [SerializeField] private WolfSpawner spawner;
    [SerializeField] private WolfEventDirector eventDirector;

    [Header("Debug HUD")]
    [SerializeField] private bool showDebugHud = true;

    private bool isGameOver;
    private int sheepTaken;
    private int centerHits;
    private int grazes;
    private float scatteredRefreshTimer;
    private int scatteredCount;
    private float elapsed;

    private void Awake()
    {
        if (flock != null && flockMovement == null)
        {
            flockMovement = flock.GetComponent<FlockMovementController>();
        }
    }

    private void OnEnable()
    {
        if (flock != null)
        {
            flock.MemberCountChanged += HandleMemberCountChanged;
        }

        if (spawner != null)
        {
            spawner.WolfSpawned += HandleWolfSpawned;
        }
    }

    private void OnDisable()
    {
        if (flock != null)
        {
            flock.MemberCountChanged -= HandleMemberCountChanged;
        }

        if (spawner != null)
        {
            spawner.WolfSpawned -= HandleWolfSpawned;
        }
    }

    private void Update()
    {
        if (!isGameOver)
        {
            elapsed += Time.deltaTime;
        }

        scatteredRefreshTimer -= Time.unscaledDeltaTime;
        if (scatteredRefreshTimer <= 0f)
        {
            scatteredRefreshTimer = 0.25f;
            scatteredCount = CountScatteredSheep();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            RestartScene();
        }
    }

    private void HandleWolfSpawned(Wolf wolf)
    {
        wolf.Attacked += HandleWolfAttacked;
    }

    private void HandleWolfAttacked(Wolf wolf, WolfAttackResult result)
    {
        if (result.CenterHit)
        {
            centerHits++;
        }
        else
        {
            grazes++;
        }

        if (result.CapturedSheep != null)
        {
            sheepTaken++;
        }
    }

    private void HandleMemberCountChanged(int memberCount)
    {
        if (memberCount <= 0 && !isGameOver)
        {
            TriggerGameOver();
        }
    }

    private void TriggerGameOver()
    {
        isGameOver = true;

        if (spawner != null)
        {
            spawner.StopSpawning();
        }

        if (eventDirector != null)
        {
            eventDirector.Stop();
        }

        if (flockMovement != null)
        {
            flockMovement.SetControlEnabled(false);
        }

        Debug.Log("Game over: the last sheep was taken by the wolves.", this);
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        Scene active = SceneManager.GetActiveScene();

        if (Application.CanStreamedLevelBeLoaded(active.name))
        {
            SceneManager.LoadScene(active.name);
            return;
        }

#if UNITY_EDITOR
        // Dev Scene 不在 Build Settings 里，编辑器下用路径重载。
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            active.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogWarning($"Scene {active.name} is not in Build Settings; cannot restart.", this);
#endif
    }

    private static int CountScatteredSheep()
    {
        int count = 0;
        foreach (ScatteredSheep sheep in FindObjectsByType<ScatteredSheep>())
        {
            if (sheep.IsScattered)
            {
                count++;
            }
        }

        return count;
    }

    private void OnGUI()
    {
        if (!showDebugHud)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            richText = true
        };
        style.normal.textColor = Color.white;

        int members = flock != null ? flock.MemberCount : 0;
        int alive = spawner != null ? spawner.AliveCount : 0;
        int spawned = spawner != null ? spawner.SpawnedCount : 0;

        GUI.Box(new Rect(10f, 10f, 300f, 150f), GUIContent.none);
        GUILayout.BeginArea(new Rect(20f, 16f, 290f, 150f));
        GUILayout.Label($"Sheep 羊群: <b>{members}</b>", style);
        GUILayout.Label($"Scattered 散落: {scatteredCount}", style);
        GUILayout.Label($"Taken 被叼走: {sheepTaken}", style);
        GUILayout.Label($"Wolves 狼: {alive} alive / {spawned} spawned", style);
        GUILayout.Label($"Center hits {centerHits} / Grazes {grazes}   {elapsed:0.0}s", style);
        GUILayout.EndArea();

        if (!isGameOver)
            return;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(1f, 0.3f, 0.25f, 1f);

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter
        };
        hintStyle.normal.textColor = Color.white;

        float width = Screen.width;
        float height = Screen.height;
        GUI.Box(new Rect(width * 0.25f, height * 0.35f, width * 0.5f, height * 0.3f), GUIContent.none);
        GUI.Label(new Rect(0f, height * 0.38f, width, 70f), "GAME OVER", titleStyle);
        GUI.Label(
            new Rect(0f, height * 0.5f, width, 40f),
            $"最后一只羊被狼撞到了 · 坚持 {elapsed:0.0} 秒 · 被叼走 {sheepTaken} 只",
            hintStyle);
        GUI.Label(new Rect(0f, height * 0.56f, width, 40f), "按 R 重新开始 (Press R to restart)", hintStyle);
    }
}
