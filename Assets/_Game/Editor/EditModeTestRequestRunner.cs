using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// 开发辅助：存在 Temp/run_editmode_tests.txt 时，脚本重载后自动跑一遍 EditMode 测试，
/// 结果写到 Temp/editmode_test_results.txt。方便在没法点 Test Runner 窗口时远程触发。
/// </summary>
public static class EditModeTestRequestRunner
{
    private const string RequestFile = "Temp/run_editmode_tests.txt";
    private const string ResultFile = "Temp/editmode_test_results.txt";

    [InitializeOnLoadMethod]
    private static void CheckRequest()
    {
        if (!File.Exists(RequestFile))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestFile))
                return;

            File.Delete(RequestFile);
            RunEditModeTests();
        };
    }

    [MenuItem("Game Jam/Dev/Run EditMode Tests (log to Temp)")]
    public static void RunEditModeTests()
    {
        TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new ResultCollector());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }

    private sealed class ResultCollector : ICallbacks
    {
        private readonly StringBuilder log = new StringBuilder();
        private int passed;
        private int failed;

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite)
                return;

            bool ok = result.TestStatus == TestStatus.Passed;
            if (ok) passed++; else failed++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")} {result.Test.FullName}");
            if (!ok && !string.IsNullOrEmpty(result.Message))
                log.AppendLine("    " + result.Message.Replace("\n", "\n    "));
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            log.Insert(0, $"EditMode tests: {passed} passed, {failed} failed\n");
            File.WriteAllText(ResultFile, log.ToString());
            Debug.Log(log.ToString());
        }
    }
}
