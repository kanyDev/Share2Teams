using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Share2Teams.Models;
using Share2Teams.Services;

namespace Share2Teams.Controllers
{
    public class FileTransferController : Controller
    {
        public ActionResult Index()
        {
            var model = new FileTransferViewModel();
            return View(model);
        }

        [HttpPost]
        public async Task<ActionResult> TransferFiles(FileTransferViewModel model)  // 비동기 메서드로 수정
        {
            try
            {
                // 쉼표로 구분된 파일 목록을 List로 변환
                model.SelectedItems = model.SelectedItemsText.Split(',').Select(f => f.Trim()).ToList();

                // FileTransferService 인스턴스 생성
                var fileTransferService = new FileTransfer(model);

                // 비동기적으로 파일 이관 작업 수행
                await fileTransferService.TransferFilesAsync(model.SourceLibrary, model.SelectedItems, model.TargetLibrary, model.TargetFolderName);

                return Json(new { success = true, message = "파일 이관이 완료되었습니다." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"오류 발생: {ex.Message}" });
            }
        }
    }

}
