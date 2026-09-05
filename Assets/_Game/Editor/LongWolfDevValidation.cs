using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit file-triggered local validation. Only handles the fixed create-and-validate request.</summary>
[InitializeOnLoad]
public static class LongWolfDevValidation
{
    private const string Request = "Tools/LongWolf.request";
    private const string Report = "Tools/LongWolf-validation.txt";
    private const string Running = "LongWolf.Validation.Running";
    private static int step;
    private static double since;
    private static FlockController flock;
    private static Wolf wolf;
    private static WolfTestGameController controller;
    private static int captures;
    private static float gameSince;
    private static double nextRequestCheck;
    private static bool previewSaved;
    private static readonly List<string> errors = new List<string>();

    static LongWolfDevValidation()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    [MenuItem("Game Jam/Wolf Test/Validate TestLongWolf")]
    public static void RequestValidation()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            File.WriteAllText(Request, "create-and-validate");
    }

    private static void OnLog(string condition, string stack, LogType type)
    {
        if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            errors.Add(condition);
    }

    private static void Tick()
    {
        try
        {
            if (!SessionState.GetBool(Running, false))
            {
                if (EditorApplication.timeSinceStartup < nextRequestCheck) return;
                nextRequestCheck = EditorApplication.timeSinceStartup + 0.5;
                if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                string command = File.ReadAllText(Request).Trim();
                File.Delete(Request);
                if (command != "create-and-validate")
                    throw new InvalidOperationException("Unsupported LongWolf validation request.");
                File.WriteAllText(Report, "Unity compilation completed. Creating scene.\n");
                LongWolfTestSceneSetup.Create();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Scene created; validation paused because another open scene has unsaved changes.");
                EditorSceneManager.OpenScene(LongWolfTestSceneSetup.ScenePath, OpenSceneMode.Single);
                ValidateGeometry();
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Scene scripts resolve");
                SessionState.SetBool(Running, true);
                SessionState.SetBool("LongWolf.Validation.Started", false);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;
            if (!SessionState.GetBool("LongWolf.Validation.Started", false))
            {
                SessionState.SetBool("LongWolf.Validation.Started", true);
                step = 0;
                captures = 0;
                previewSaved = false;
                since = EditorApplication.timeSinceStartup;
                errors.Clear();
            }
            double elapsed = EditorApplication.timeSinceStartup - since;
            if (step == 0 && elapsed > 0.6 && Time.timeSinceLevelLoad > 0.5f)
            {
                flock = Object.FindAnyObjectByType<FlockController>();
                controller = Object.FindAnyObjectByType<WolfTestGameController>();
                Check(flock != null && controller != null && flock.MemberCount == 10,
                    "Play starts with 10 sheep and controller (scene=" + SceneManager.GetActiveScene().path + ", count=" + (flock != null ? flock.MemberCount : -1) + ", gameTime=" + Time.time + ")");
                Object.FindAnyObjectByType<WolfEventDirector>().Stop();
                Object.FindAnyObjectByType<WolfSpawner>().StopSpawning();
                flock.GetComponent<FlockMovementController>().SetControlEnabled(false);
                SheepMember[] members = flock.Members.ToArray();
                for (int i = 0; i < members.Length; i++)
                {
                    members[i].GetComponent<SheepFlockAgent>().enabled = false;
                    members[i].GetComponent<Rigidbody2D>().position = new Vector2(i % 5, i < 5 ? 0f : 5f);
                    members[i].transform.position = new Vector2(i % 5, i < 5 ? 0f : 5f);
                }
                Physics2D.SyncTransforms();
                wolf = Spawn(new Vector2(-8f, 0f));
                ValidateWarningVisuals(wolf);
                Time.timeScale = 0f;
                Next(1);
            }
            else if (step == 1 && elapsed > 0.5)
            {
                Check(flock.MemberCount == 10 && captures == 0 && wolf.transform.position.x == -8f, "Pause prevents movement and capture");
                Time.timeScale = 1f;
                Next(2);
            }
            else if (step == 2 && elapsed > 1.3)
            {
                Check(flock.MemberCount == 5 && captures == 5, "100 units/sec sweep captures all 5 on-path sheep once; 5 off-path sheep survive");
                Check(Object.FindObjectsByType<ScatteredSheep>(FindObjectsSortMode.None).Length == 0, "No scattered or rescuable sheep created");
                Check(wolf.GetComponentsInChildren<SheepMember>().Length == 5, "All captured sheep travel with the wolf");
                foreach (SheepMember carried in wolf.GetComponentsInChildren<SheepMember>())
                    Check(carried.Flock == null && !carried.GetComponent<Rigidbody2D>().simulated, "Captured members leave flock and physics");
                flock.transform.position = new Vector2(0f, 5f);
                flock.GetComponent<Rigidbody2D>().position = new Vector2(0f, 5f);
                wolf = Spawn(new Vector2(-8f, 5f));
                Next(3);
            }
            else if (step == 3 && elapsed > 1.3)
            {
                Check(flock.MemberCount == 0 && captures == 10, "Second sweep captures the remaining 5");
                bool gameOver = (bool)typeof(WolfTestGameController).GetField("isGameOver", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
                Check(gameOver, "Zero sheep triggers Game Over");
                typeof(WolfTestGameController).GetMethod("RestartScene", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
                Next(4);
            }
            else if (step == 4 && elapsed > 1)
            {
                flock = Object.FindAnyObjectByType<FlockController>();
                Check(flock != null && flock.MemberCount == 10, "R restart handler restores scene to 10 sheep");
                Check(Object.FindObjectsByType<Wolf>(FindObjectsSortMode.None).Length == 0, "Restart clears old wolves and captured sheep");
                Next(5);
            }
            else if (step == 5)
            {
                if (flock.MemberCount == 10 && Time.time - gameSince < 25f) return;
                Check(Object.FindAnyObjectByType<WolfSpawner>().SpawnedCount > 0, "Unmodified scene rhythm spawns a long wolf");
                Check(flock.MemberCount < 10, "Natural stationary-play attack captures sheep");
                SavePreview();
                Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors));
                File.AppendAllText(Report, "PASS: Unity scene generation, references, swept collisions, pause, capture, Game Over, restart and natural rhythm.\n");
                SessionState.SetBool(Running, false);
                Time.timeScale = 1f;
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            File.AppendAllText(Report, "FAIL: " + exception + "\n");
            SessionState.SetBool(Running, false);
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }

    private static Wolf Spawn(Vector2 position)
    {
        Wolf instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LongWolfTestSceneSetup.PrefabPath), position, Quaternion.identity).GetComponent<Wolf>();
        SerializedObject settings = new SerializedObject(instance);
        settings.FindProperty("warningDuration").floatValue = 0.4f;
        settings.FindProperty("chargeSpeed").floatValue = 100f;
        settings.FindProperty("chargeOverrun").floatValue = 12f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        instance.Attacked += (_, result) => { if (result.CapturedSheep != null) captures++; };
        instance.Launch(flock);
        return instance;
    }

    private static void Next(int value) { step = value; since = EditorApplication.timeSinceStartup; gameSince = Time.time; }

    private static void SavePreview()
    {
        if (previewSaved || Camera.main == null) return;
        previewSaved = true;
        Camera camera = Camera.main;
        RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("Tools/LongWolf-preview.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
        }
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        if (name != "Scene scripts resolve") File.AppendAllText(Report, "PASS: " + name + "\n");
    }

    private static void ValidateGeometry()
    {
        Check(LongWolfSweep.TouchesSweep(new Vector2(5f, 0f), 0.3f, Vector2.zero, new Vector2(10f, 0f), Vector2.right, 7f, 1.3f), "Swept hit between physics endpoints");
        Check(!LongWolfSweep.TouchesSweep(new Vector2(12f, 0f), 0.3f, Vector2.zero, new Vector2(10f, 0f), Vector2.right, 7f, 1.3f), "Does not capture sheep ahead of the head");
        Check(!LongWolfSweep.TouchesSweep(new Vector2(5f, 2f), 0.3f, Vector2.zero, new Vector2(10f, 0f), Vector2.right, 7f, 1.3f), "Off-path sheep are safe");
        Check(LongWolfSweep.TouchesSweep(new Vector2(0f, -4f), 0.3f, Vector2.zero, Vector2.up, Vector2.up, 7f, 1.3f), "Tail contact works after rotating direction");
    }

    private static void ValidateWarningVisuals(Wolf instance)
    {
        LongWolfSweep sweep = instance.GetComponent<LongWolfSweep>();
        Check(Mathf.Approximately(sweep.BodyWidth, 0.8f), "Long wolf uses the narrower 0.8 width");
        SerializedObject settings = new SerializedObject(instance);
        SpriteRenderer warning = (SpriteRenderer)settings.FindProperty("warningRenderer").objectReferenceValue;
        Check(Mathf.Approximately(warning.transform.localScale.y, sweep.BodyWidth), "Warning width matches swept hit width");
        SerializedObject sweepSettings = new SerializedObject(sweep);
        Transform visual = (Transform)sweepSettings.FindProperty("bodyVisual").objectReferenceValue;
        Check(Mathf.Approximately(visual.localScale.y, sweep.BodyWidth), "Body visual width matches swept hit width");
        FieldInfo timer = typeof(Wolf).GetField("stateTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo update = typeof(Wolf).GetMethod("UpdateWarning", BindingFlags.NonPublic | BindingFlags.Instance);
        float previousScale = Time.timeScale;
        Time.timeScale = 0f;
        try
        {
            timer.SetValue(instance, 0f);
            update.Invoke(instance, null);
            float bright = warning.color.a;
            timer.SetValue(instance, 0.15f);
            update.Invoke(instance, null);
            Check(bright > 0f && Mathf.Approximately(warning.color.a, 0f), "Warning alternates visible red and fully transparent");
            timer.SetValue(instance, 0f);
            update.Invoke(instance, null);
        }
        finally { Time.timeScale = previousScale; }
    }
}
