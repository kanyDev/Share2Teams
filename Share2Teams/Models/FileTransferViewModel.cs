using System;
using System.Collections.Generic;
using System.Linq;

namespace Share2Teams.Models
{
    public class FileTransferViewModel
    {
        public string SourceSiteUrl { get; set; } = "http://ecm2016.hhi.local/ksoe/sites/131h9";

        public string TargetSiteUrl { get; set; } = "https://neoplusdevonline.sharepoint.com/sites/msteams_b1aac3";

        public string SourceLibrary { get; set; } = "일반문서";

        public string TargetLibrary { get; set; } = "문서";

        public string TargetFolderName { get; set; } = "General";


        // 쉼표로 구분된 파일 목록을 받기 위한 프로퍼티
        public string SelectedItemsText { get; set; } = "TestFileChageName.txt";

        public List<string> SelectedItems { get; set; } = new List<string>(); // 파일 목록 (쉼표로 구분)


        public string SourceUsername { get; set; } = "spfarm";

        public string SourcePassword { get; set; } = "P@ssw0rd1!";

        public string SourceDomain { get; set; } = "hhi";

        public string TargetUsername { get; set; } = "a001@neoplus.xyz";

        public string TargetPassword { get; set; } = "P@ssw0rd1!";
    }
}
