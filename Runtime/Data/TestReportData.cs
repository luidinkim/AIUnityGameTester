using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIUnityTester.Data
{
    /// <summary>
    /// 테스트 리포트 데이터를 담는 직렬화 가능한 클래스.
    /// </summary>
    [Serializable]
    public class TestReportData
    {
        public string testName;
        public string startTime;
        public string endTime;
        public int totalSteps;
        public List<TestStepData> steps = new List<TestStepData>();

        public TestReportData(string name)
        {
            testName = name;
            startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            steps = new List<TestStepData>();
        }
    }

    /// <summary>
    /// 개별 테스트 단계 데이터.
    /// </summary>
    [Serializable]
    public class TestStepData
    {
        public int stepNumber;
        public string timestamp;
        public string thought;
        public string actionType;
        public string actionDetails;
        public string screenshotPath;

        public TestStepData(int step, string thought, string action, string details, string screenshot)
        {
            this.stepNumber = step;
            this.timestamp = DateTime.Now.ToString("HH:mm:ss");
            this.thought = thought;
            this.actionType = action;
            this.actionDetails = details;
            this.screenshotPath = screenshot;
        }
    }
}
