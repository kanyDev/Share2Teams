using System;
using System.Collections.Generic;
using System.Linq;

namespace Share2Teams.Models
{
    public class FileTransferViewModel
    {
        public string SourceSiteUrl { get; set; }

        public string TargetSiteUrl { get; set; }

        public string SourceLibrary { get; set; }

        public string TargetLibrary { get; set; }

        public string TargetFolderName { get; set; }


        // 쉼표로 구분된 파일 목록을 받기 위한 프로퍼티
        public string SelectedItemsText { get; set; }

        public List<string> SelectedItems { get; set; } = new List<string>(); // 파일 목록 (쉼표로 구분)


        public string SourceUsername { get; set; }

        public string SourcePassword { get; set; }

        public string SourceDomain { get; set; }

        public string TargetUsername { get; set; }

        public string TargetPassword { get; set; }
    }
}
