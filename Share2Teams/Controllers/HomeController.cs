using Microsoft.SharePoint.Client;
using Share2Teams.Models;
using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Share2Teams.Models;
using Share2Teams.Services;
using System.Management.Automation.Language;

namespace Share2Teams.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public async Task<ActionResult> CheckVersionFile()
        {
            //string siteUrl = "http://ecm2016.hhi.local/ksoe/sites/131h9";
            string siteUrl = "https://neoplusdevonline.sharepoint.com/sites/msteams_b1aac3";

            //string versionRelativeUrl = "/ksoe/sites/131h9/_vti_history/512/DocLib/TestFileChageName.txt";
            string versionRelativeUrl = "/sites/msteams_b1aac3/_vti_history/1024/Shared Documents/TestFile.txt";
            //string versionRelativeUrl = "/sites/msteams_b1aac3/Shared Documents/TestFile.txt";
            
            try
            {
                

                // SharePoint 클라이언트 컨텍스트 초기화
                using (var context = new ClientContext(siteUrl))
                {
                    // SharePoint 로그인 정보 설정
                    //context.Credentials = new NetworkCredential("spfarm", "P@ssw0rd1!", "hhi");
                    context.Credentials = new SharePointOnlineCredentials("a001@neoplus.xyz", GetSecureString("P@ssw0rd1!"));

                    try
                    {
                        // 파일 로드 시도
                        var file = context.Web.GetFileByServerRelativeUrl(versionRelativeUrl);
                        context.Load(file, f => f.Exists);
                        await Task.Run(() => context.ExecuteQuery());

                        if (file.Exists)
                        {
                            return Content($"File exists at {versionRelativeUrl}. Access is permitted.");
                        }
                        else
                        {
                            return Content($"File does not exist or access is denied for {versionRelativeUrl}.");
                        }
                    }
                    catch (ClientRequestException cre)
                    {
                        // 요청 오류 처리 (예: 404 Not Found)
                        return Content($"ClientRequestException: {cre.Message}");
                    }
                    catch (ServerException se)
                    {
                        // 서버 오류 처리 (예: 401 Unauthorized 등)
                        return Content($"ServerException: {se.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // 일반 오류 처리
                return Content($"Error accessing the file: {ex.Message}");
            }
        }

        public SecureString GetSecureString(string password)
        {
            SecureString securePassword = new SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            return securePassword;
        }

        [HttpGet]
        public async Task<ActionResult> DownloadFileVersion()
        {
            string siteUrl = "http://ecm2016.hhi.local/ksoe/sites/131h9";
            string fileRelativeUrl = "/ksoe/sites/131h9/DocLib/TestFileChageName.txt";

            var fileVersions = new List<Share2Teams.Models.FileVersionInfo>();

            using (var context = new ClientContext(siteUrl))
            {
                context.Credentials = new NetworkCredential("spfarm", "P@ssw0rd1!", "hhi");

                try
                {
                    // 파일 객체 로드
                    var file = context.Web.GetFileByServerRelativeUrl(fileRelativeUrl);
                    context.Load(file.Versions, v => v.Include(
                        version => version.VersionLabel,
                        version => version.Created,
                        version => version.IsCurrentVersion,
                        version => version.CheckInComment,
                        version => version.Size
                        ));

                    await Task.Run(() => context.ExecuteQuery());

                    // 파일 버전 정보 저장
                    fileVersions = file.Versions.Select(v => new FileVersionInfo(
                        v.VersionLabel, 
                        v.Created, 
                        v.IsCurrentVersion, 
                        v.CheckInComment,
                        v.Size
                    )).ToList();
                }
                catch (Exception ex)
                {
                    return Content($"Error retrieving file versions: {ex.Message}");
                }
            }

            // 버전 목록을 뷰로 전달
            return View(fileVersions);
        }
        

        [HttpPost]
        public async Task<ActionResult> DownloadFileVersionAsync(string versionLabel)
        {
            string sourceSiteUrl = "http://ecm2016.hhi.local/ksoe/sites/131h9";
            string fileRelativeUrl = "/ksoe/sites/131h9/DocLib/TestFileChageName.txt";

            string targetSiteUrl = "https://neoplusdevonline.sharepoint.com/sites/msteams_b1aac3";
            using (var sourceContext = new ClientContext(sourceSiteUrl))
            using (var targetContext = new ClientContext(targetSiteUrl))
            {
                sourceContext.Credentials = new NetworkCredential("spfarm", "P@ssw0rd1!", "hhi");
                targetContext.Credentials = new SharePointOnlineCredentials("a001@neoplus.xyz", GetSecureString("P@ssw0rd1!"));
                try
                {
                    // 파일 객체 로드
                    var file = sourceContext.Web.GetFileByServerRelativeUrl(fileRelativeUrl);
                    sourceContext.Load(file.Versions);
                    await Task.Run(() => sourceContext.ExecuteQuery());

                    // 특정 버전 찾기
                    var version = file.Versions.FirstOrDefault(v => v.VersionLabel == versionLabel);
                    if (version != null)
                    {
                        var versionStream = version.OpenBinaryStream();
                        sourceContext.ExecuteQuery();

                        using (var memoryStream = new MemoryStream())
                        {
                            versionStream.Value.CopyTo(memoryStream);
                            byte[] fileBytes = memoryStream.ToArray();

                            // 로컬에 파일 다운로드
                            string fileName = $"TestFile_v{versionLabel}.txt";
                            return File(fileBytes, "application/octet-stream", fileName);

                            string targetLibrary = "문서";
                            string targetFolderName = "General";

                            

                        }
                    }
                    else
                    {
                        return Content($"Version {versionLabel} not found for the file.");
                    }
                }
                catch (Exception ex)
                {
                    return Content($"Error retrieving version {versionLabel}: {ex.Message}");
                }
            }
        }
    }
}