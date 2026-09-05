using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// TestSmartWolf 专用的两个可拖动调试窗口：
/// 1. 当前学习率 —— 狼群躲避记忆积累了多少样本、平均 / 最近躲避角度；
/// 2. 当前预测角度 —— 下一只狼会往哪偏多少度、正在场上的狼实际偏了多少。
/// 只在 Dev 测试场景里使用，不进正式关卡。
/// </summary>
[DisallowMultipleComponent]
public sealed class SmartWolfDebugView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WolfSpawner spawner;
    [SerializeField] private WolfEventDirector director;

    [Header("Window Layout")]
    [SerializeField] private Rect learningWindowRect = new Rect(10f, 180f, 340f, 230f);
    [SerializeField] private Rect predictionWindowRect = new Rect(10f, 430f, 340f, 200f);
    [SerializeField, Min(10)] private int fontSize = 16;
    [Tooltip("按 F1 隐藏 / 显示两个窗口。")]
    [SerializeField] private bool visible = true;

    private const int LearningWindowId = 7301;
    private const int PredictionWindowId = 7302;

    private Wolf trackedWolf;
    private float lastAppliedDegrees;
    private bool lastWolfPredicted;
    private int lastWolfIndex;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle windowStyle;
    private readonly StringBuilder builder = new StringBuilder();

    private void OnEnable()
    {
        if (spawner != null)
        {
            spawner.WolfSpawned += HandleWolfSpawned;
        }
    }

    private void OnDisable()
    {
        if (spawner != null)
        {
            spawner.WolfSpawned -= HandleWolfSpawned;
        }
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
        {
            visible = !visible;
        }

        if (trackedWolf != null && trackedWolf.PredictionApplied)
        {
            lastAppliedDegrees = trackedWolf.AppliedPredictionDegrees;
            lastWolfPredicted = true;
        }
    }

    private void HandleWolfSpawned(Wolf wolf)
    {
        trackedWolf = wolf;
        lastWolfIndex = spawner != null ? spawner.SpawnedCount : lastWolfIndex + 1;
        lastAppliedDegrees = 0f;
        lastWolfPredicted = false;
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        EnsureStyles();
        learningWindowRect = GUI.Window(LearningWindowId, learningWindowRect, DrawLearningWindow, "当前学习率", windowStyle);
        predictionWindowRect = GUI.Window(PredictionWindowId, predictionWindowRect, DrawPredictionWindow, "当前预测角度", windowStyle);
    }

    private void DrawLearningWindow(int id)
    {
        WolfDodgeMemory memory = spawner != null ? spawner.DodgeMemory : null;
        Wolf prefab = spawner != null ? spawner.WolfPrefab : null;
        int minimum = prefab != null ? prefab.PredictionMinimumSamples : 1;

        GUILayout.BeginVertical();
        if (memory == null)
        {
            GUILayout.Label("没有 WolfSpawner，无法读取狼群记忆。", labelStyle);
        }
        else
        {
            float rate = memory.Capacity > 0 ? (float)memory.Count / memory.Capacity : 0f;
            GUILayout.Label($"学习率: <b>{rate * 100f:0}%</b>  ({memory.Count} / {memory.Capacity} 次进攻)", labelStyle);
            DrawBar(rate, new Color(0.35f, 0.8f, 0.4f));

            string ready = memory.HasEnoughSamples(minimum)
                ? "<color=#7CFC00>已达到预判门槛</color>"
                : $"<color=#FFB347>还差 {Mathf.Max(0, minimum - memory.Count)} 次进攻开始预判</color>";
            GUILayout.Label($"预判门槛: {minimum} 次   {ready}", labelStyle);
            GUILayout.Label($"平均躲避角: <b>{FormatSigned(memory.AverageDegrees)}</b>   {Side(memory.AverageDegrees)}", labelStyle);
            GUILayout.Label($"最近一次: {FormatSigned(memory.LatestDegrees)}", labelStyle);
            GUILayout.Label($"最近样本: {FormatSamples(memory.Samples, 8)}", labelStyle);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("<size=12>正 = 红框左侧(逆时针)，负 = 右侧。F1 隐藏窗口，可拖动。</size>", labelStyle);
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void DrawPredictionWindow(int id)
    {
        WolfDodgeMemory memory = spawner != null ? spawner.DodgeMemory : null;
        Wolf prefab = spawner != null ? spawner.WolfPrefab : null;

        GUILayout.BeginVertical();
        if (prefab == null || memory == null)
        {
            GUILayout.Label("缺少狼 prefab 或狼群记忆。", labelStyle);
        }
        else if (!prefab.UseDodgePrediction)
        {
            GUILayout.Label("<color=#FFB347>狼 prefab 关闭了预判 (Use Dodge Prediction)。</color>", labelStyle);
        }
        else
        {
            float preview = prefab.PreviewPredictionDegrees(memory);
            GUILayout.Label($"下一只狼预测偏转: <b>{FormatSigned(preview)}</b>   {Side(preview)}", labelStyle);
            GUILayout.Label(
                $"= 平均 {FormatSigned(memory.AverageDegrees)} × 强度 {prefab.PredictionStrength:0.00}，上限 ±{prefab.PredictionMaxDegrees:0}°",
                labelStyle);
            GUILayout.Label($"预警 {prefab.WarningDuration:0.00}s，最后 {prefab.PredictionLeadTime:0.00}s 红框转向预判方向", labelStyle);
        }

        GUILayout.Space(6f);
        if (trackedWolf != null)
        {
            string phase = trackedWolf.IsWarning
                ? $"预警中 {trackedWolf.WarningElapsed:0.00}s"
                : trackedWolf.IsCharging ? "冲锋中" : "撤退 / 结束";
            string applied = trackedWolf.PredictionApplied
                ? $"实际偏转 <b>{FormatSigned(trackedWolf.AppliedPredictionDegrees)}</b> {Side(trackedWolf.AppliedPredictionDegrees)} 转向 {trackedWolf.PredictionTurnProgress * 100f:0}%"
                : "尚未预判";
            GUILayout.Label($"场上狼 #{lastWolfIndex}: {phase}，{applied}", labelStyle);
        }
        else if (lastWolfIndex > 0)
        {
            string applied = lastWolfPredicted
                ? $"实际偏转 {FormatSigned(lastAppliedDegrees)} {Side(lastAppliedDegrees)}"
                : "没有预判(样本不足或偏转 <0.5°)";
            GUILayout.Label($"上一只狼 #{lastWolfIndex}: {applied}", labelStyle);
        }
        else
        {
            GUILayout.Label("还没有狼出场。", labelStyle);
        }

        if (director != null)
        {
            GUILayout.Label($"节奏: {director.Phase}  剩余 {Mathf.Max(0f, director.PhaseTimeRemaining):0.0}s  第 {director.RoundIndex} 轮", labelStyle);
        }

        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void DrawBar(float fraction, Color fill)
    {
        Rect rect = GUILayoutUtility.GetRect(10f, 14f, GUILayout.ExpandWidth(true));
        GUI.Box(rect, GUIContent.none);
        Rect inner = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(fraction), rect.height - 4f);
        Color previous = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(inner, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private string FormatSamples(IEnumerable<float> samples, int maximum)
    {
        List<float> list = new List<float>(samples);
        builder.Clear();
        int start = Mathf.Max(0, list.Count - maximum);
        for (int index = start; index < list.Count; index++)
        {
            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(list[index].ToString("+0;-0;0"));
        }
        return builder.Length == 0 ? "—" : builder.ToString();
    }

    private static string FormatSigned(float degrees) => $"{degrees:+0.0;-0.0;0.0}°";

    private static string Side(float degrees)
    {
        if (Mathf.Abs(degrees) < 0.5f)
            return "(不偏)";
        return degrees > 0f ? "(偏向左 / 逆时针)" : "(偏向右 / 顺时针)";
    }

    private void EnsureStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true,
            wordWrap = true
        };
        labelStyle.normal.textColor = Color.white;

        headerStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };

        windowStyle = new GUIStyle(GUI.skin.window)
        {
            fontSize = fontSize,
            richText = true
        };
        windowStyle.normal.textColor = Color.white;
        windowStyle.onNormal.textColor = Color.white;
    }
}
