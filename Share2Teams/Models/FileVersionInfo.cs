using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Share2Teams.Models
{
    public class FileVersionInfo
    {
        public string VersionLabel { get; set; }
        public DateTime Created { get; set; }

        public bool IsCurrentVersion { get; set; }

        public string CheckInComment { get; set; }

        public int Size { get; set; }

        // 생성자 정의
        public FileVersionInfo(string versionLabel, DateTime created, bool isCurrentVersion, string checkInComment, int size)
        {
            VersionLabel = versionLabel;
            Created = created;
            IsCurrentVersion = isCurrentVersion;
            CheckInComment = checkInComment;
            Size = size;
        }
    }
}