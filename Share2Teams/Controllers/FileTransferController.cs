using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Web.Mvc;
using Share2Teams.Services;

namespace Share2Teams.Controllers
{
    public class FileTransferController : Controller
    {
        private readonly FileTransfer _fileTransferService;
        private readonly PowerShellRunner _powerShellRunner;

        // Controller에서 설정을 직접 가져오는 방식
        private string SourceSiteUrl => ConfigurationManager.AppSettings["SourceSiteUrl"];
        private string SourceUserName => ConfigurationManager.AppSettings["SourceUserName"];
        private string SourcePassword => ConfigurationManager.AppSettings["SourcePassword"];
        private string Domain => ConfigurationManager.AppSettings["Domain"];
        private string TargetSiteUrl => ConfigurationManager.AppSettings["TargetSiteUrl"];
        private string TargetUserName => ConfigurationManager.AppSettings["TargetUserName"];
        private string TargetPassword => ConfigurationManager.AppSettings["TargetPassword"];
        private string PowerShellScriptPath => ConfigurationManager.AppSettings["PowerShellScriptPath"];

        public FileTransferController()
        {
            // 파일 전송 서비스 초기화
            _fileTransferService = new FileTransfer(
                sourceSiteUrl: SourceSiteUrl,
                sourceUserName: SourceUserName,
                sourcePassword: SourcePassword,
                domain: Domain,
                targetSiteUrl: TargetSiteUrl,
                targetUserName: TargetUserName,
                targetPassword: TargetPassword
            );

            //_powerShellRunner = new PowerShellRunner();
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> TransferFiles()
        {
            try
            {
                // 파일 전송 메서드 호출
                await _fileTransferService.TransferFilesInFolderAsync(
                    sourceLibrary: "일반문서",
                    sourceFolderName: "2020",
                    targetLibrary: "문서",
                    targetFolderName: "General"
                );

               

                ViewBag.Message = "파일 전송과 메타, 권한, 버전 데이터 이전이 성공적으로 완료되었습니다.";
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"파일 전송 중 오류가 발생했습니다: {ex.Message}";
            }

            return View("Index");
        }

        private async Task RunPowerShellScriptAsync(string scriptPath, string arguments)
        {
            await Task.Run(() =>
            {
                PowerShellRunner.RunPowerShellScript(scriptPath, arguments);
            });
        }
    }
}
