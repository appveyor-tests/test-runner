using System;
using System.Collections.Generic;
using System.Text;

namespace TestSuiteRunner
{
    class NewBuildRequest
    {
        public string AccountName { get; set; }
        public string ProjectSlug { get; set; }
        public string Branch { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}
