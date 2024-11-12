using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Share2Teams
{
    public class AppSettings
    {
        public string SourceSiteUrl { get; set; }
        public string SourceUserName { get; set; }
        public string SourcePassword { get; set; }
        public string Domain { get; set; }
        public string TargetSiteUrl { get; set; }
        public string TargetUserName { get; set; }
        public string TargetPassword { get; set; }
        public string PowerShellScriptPath { get; set; }
    }
}