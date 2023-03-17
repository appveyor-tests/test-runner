using System;
using System.Collections.Generic;
using System.Text;

namespace TestSuiteRunner
{
    class TestItem
    {
        public string TestName { get; set; }
        public string AccountName { get; set; }
        public string ProjectSlug { get; set; }
        public string Branch { get; set; }
        public int TimeoutMinutes { get; set; }
        public bool ShouldSucceed { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
        public string[] Images{ get; set; }
    }
}
