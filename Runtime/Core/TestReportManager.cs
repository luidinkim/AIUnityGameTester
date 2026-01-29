using System;
using System.IO;
using System.Text;
using UnityEngine;
using AIUnityTester.Data;

namespace AIUnityTester.Core
{
    /// <summary>
    /// AI 테스트 결과를 기록하고 Markdown/HTML로 내보내는 관리자 클래스.
    /// </summary>
    public class TestReportManager
    {
        private TestReportData _currentReport;
        private string _reportFolder;
        private int _stepCounter = 0;

        public bool IsRecording => _currentReport != null;
        public string ReportFolder => _reportFolder;

        public TestReportManager()
        {
            _reportFolder = Path.Combine(Application.persistentDataPath, "AITesterReports");
            if (!Directory.Exists(_reportFolder))
            {
                Directory.CreateDirectory(_reportFolder);
            }
        }

        /// <summary>
        /// 새로운 테스트 리포트 기록을 시작합니다.
        /// </summary>
        public void StartNewReport(string testName = null)
        {
            if (string.IsNullOrEmpty(testName))
            {
                testName = $"Test_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            _currentReport = new TestReportData(testName);
            _stepCounter = 0;

            Debug.Log($"[TestReportManager] Started new report: {testName}");
        }

        /// <summary>
        /// 테스트 단계를 기록합니다.
        /// </summary>
        public void LogStep(string thought, AIActionData action, Texture2D screenshot = null)
        {
            if (_currentReport == null)
            {
                Debug.LogWarning("[TestReportManager] No active report. Call StartNewReport first.");
                return;
            }

            _stepCounter++;

            string screenshotPath = "";
            if (screenshot != null)
            {
                screenshotPath = SaveScreenshot(screenshot, _stepCounter);
            }

            string actionDetails = GetActionDetails(action);

            var step = new TestStepData(
                _stepCounter,
                thought,
                action.actionType,
                actionDetails,
                screenshotPath
            );

            _currentReport.steps.Add(step);
        }

        /// <summary>
        /// 테스트 리포트 기록을 종료합니다.
        /// </summary>
        public void EndReport()
        {
            if (_currentReport == null) return;

            _currentReport.endTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _currentReport.totalSteps = _stepCounter;

            Debug.Log($"[TestReportManager] Report ended. Total steps: {_stepCounter}");
        }

        /// <summary>
        /// 현재 리포트를 Markdown 형식으로 내보냅니다.
        /// </summary>
        public string ExportToMarkdown()
        {
            if (_currentReport == null)
            {
                Debug.LogWarning("[TestReportManager] No report to export.");
                return null;
            }

            StringBuilder sb = new StringBuilder();

            // Header
            sb.AppendLine($"# AI Test Report: {_currentReport.testName}");
            sb.AppendLine();
            sb.AppendLine($"- **Start Time**: {_currentReport.startTime}");
            sb.AppendLine($"- **End Time**: {_currentReport.endTime}");
            sb.AppendLine($"- **Total Steps**: {_currentReport.totalSteps}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Steps
            sb.AppendLine("## Test Steps");
            sb.AppendLine();

            foreach (var step in _currentReport.steps)
            {
                sb.AppendLine($"### Step {step.stepNumber} [{step.timestamp}]");
                sb.AppendLine();
                sb.AppendLine($"**Thought**: {step.thought}");
                sb.AppendLine();
                sb.AppendLine($"**Action**: `{step.actionType}` - {step.actionDetails}");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(step.screenshotPath))
                {
                    string relativePath = Path.GetFileName(step.screenshotPath);
                    sb.AppendLine($"![Screenshot]({relativePath})");
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Save file
            string fileName = $"{_currentReport.testName}.md";
            string filePath = Path.Combine(_reportFolder, fileName);
            File.WriteAllText(filePath, sb.ToString());

            Debug.Log($"[TestReportManager] Markdown report saved: {filePath}");
            return filePath;
        }

        /// <summary>
        /// 현재 리포트를 HTML 형식으로 내보냅니다.
        /// </summary>
        public string ExportToHTML()
        {
            if (_currentReport == null)
            {
                Debug.LogWarning("[TestReportManager] No report to export.");
                return null;
            }

            StringBuilder sb = new StringBuilder();

            // HTML Header
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ko\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine($"  <title>AI Test Report: {_currentReport.testName}</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; background: #1a1a2e; color: #eee; }");
            sb.AppendLine("    h1 { color: #00d9ff; border-bottom: 2px solid #00d9ff; padding-bottom: 10px; }");
            sb.AppendLine("    h2 { color: #ff6b6b; }");
            sb.AppendLine("    .meta { background: #16213e; padding: 15px; border-radius: 8px; margin-bottom: 20px; }");
            sb.AppendLine("    .step { background: #0f3460; padding: 20px; border-radius: 8px; margin-bottom: 15px; }");
            sb.AppendLine("    .step-header { color: #00d9ff; font-size: 1.2em; margin-bottom: 10px; }");
            sb.AppendLine("    .thought { color: #94d2bd; font-style: italic; }");
            sb.AppendLine("    .action { background: #1a1a2e; padding: 10px; border-radius: 4px; margin-top: 10px; }");
            sb.AppendLine("    .action code { color: #ff6b6b; }");
            sb.AppendLine("    img { max-width: 100%; border-radius: 8px; margin-top: 10px; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Title & Meta
            sb.AppendLine($"  <h1>🤖 AI Test Report: {_currentReport.testName}</h1>");
            sb.AppendLine("  <div class=\"meta\">");
            sb.AppendLine($"    <p><strong>Start Time:</strong> {_currentReport.startTime}</p>");
            sb.AppendLine($"    <p><strong>End Time:</strong> {_currentReport.endTime}</p>");
            sb.AppendLine($"    <p><strong>Total Steps:</strong> {_currentReport.totalSteps}</p>");
            sb.AppendLine("  </div>");

            // Steps
            sb.AppendLine("  <h2>📝 Test Steps</h2>");

            foreach (var step in _currentReport.steps)
            {
                sb.AppendLine("  <div class=\"step\">");
                sb.AppendLine($"    <div class=\"step-header\">Step {step.stepNumber} <span style=\"color:#888\">[{step.timestamp}]</span></div>");
                sb.AppendLine($"    <p class=\"thought\">💭 {step.thought}</p>");
                sb.AppendLine($"    <div class=\"action\">🎮 <code>{step.actionType}</code> - {step.actionDetails}</div>");

                if (!string.IsNullOrEmpty(step.screenshotPath))
                {
                    string relativePath = Path.GetFileName(step.screenshotPath);
                    sb.AppendLine($"    <img src=\"{relativePath}\" alt=\"Screenshot Step {step.stepNumber}\">");
                }

                sb.AppendLine("  </div>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            // Save file
            string fileName = $"{_currentReport.testName}.html";
            string filePath = Path.Combine(_reportFolder, fileName);
            File.WriteAllText(filePath, sb.ToString());

            Debug.Log($"[TestReportManager] HTML report saved: {filePath}");
            return filePath;
        }

        private string SaveScreenshot(Texture2D screenshot, int stepNumber)
        {
            try
            {
                byte[] bytes = screenshot.EncodeToJPG(85);
                string fileName = $"{_currentReport.testName}_step{stepNumber:D3}.jpg";
                string filePath = Path.Combine(_reportFolder, fileName);
                File.WriteAllBytes(filePath, bytes);
                return filePath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TestReportManager] Failed to save screenshot: {e.Message}");
                return "";
            }
        }

        private string GetActionDetails(AIActionData action)
        {
            switch (action.actionType)
            {
                case "Click":
                    return $"Position ({action.screenPosition.x:F2}, {action.screenPosition.y:F2})";
                case "Drag":
                    return $"From ({action.screenPosition.x:F2}, {action.screenPosition.y:F2}) to ({action.targetPosition.x:F2}, {action.targetPosition.y:F2})";
                case "KeyPress":
                    return $"Key: {action.keyName}";
                case "Type":
                    return $"Text: \"{action.textToType}\"";
                case "Wait":
                    return $"Duration: {action.duration}s";
                default:
                    return "";
            }
        }

        /// <summary>
        /// 리포트 폴더를 탐색기에서 엽니다.
        /// </summary>
        public void OpenReportFolder()
        {
            #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", _reportFolder.Replace("/", "\\"));
            #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", _reportFolder);
            #endif
        }
    }
}
