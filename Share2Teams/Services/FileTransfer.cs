using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Web;
using System.Threading.Tasks;


namespace Share2Teams.Services
{
    public class FileTransfer
    {
        private string sourceSiteUrl;
        private string sourceUserName;
        private string sourcePassword;
        private string domain;

        private string targetSiteUrl;
        private string targetUserName;
        private string targetPassword;

        public FileTransfer(string sourceSiteUrl, string sourceUserName, string sourcePassword, string domain,
                                string targetSiteUrl, string targetUserName, string targetPassword)
        {
            this.sourceSiteUrl = sourceSiteUrl;
            this.sourceUserName = sourceUserName;
            this.sourcePassword = sourcePassword;
            this.domain = domain;

            this.targetSiteUrl = targetSiteUrl;
            this.targetUserName = targetUserName;
            this.targetPassword = targetPassword;
        }

        // 파일(들) 혹은 폴더 선택가능
        public async Task TransferFilesAsync(
            string sourceLibrary,
            List<string> sourceItemNames,
            string targetLibrary,
            string targetFolderName,
            bool isFolder)
        {
            using (ClientContext clientContext = new ClientContext(sourceSiteUrl))
            {
                clientContext.Credentials = new NetworkCredential(sourceUserName, sourcePassword, domain);
                List oList = clientContext.Web.Lists.GetByTitle(sourceLibrary);
                clientContext.Load(oList.RootFolder);
                await clientContext.ExecuteQueryAsync();

                foreach (var sourceItemName in sourceItemNames)
                {
                    if (isFolder)
                    {
                        // 소스 폴더 경로 설정
                        string folderServerRelativeUrl = $"{oList.RootFolder.ServerRelativeUrl}/{sourceItemName}";
                        string targetFolderRelativeUrl = $"{targetFolderName}/{sourceItemName}";
                        // 중첩된 폴더 구조에서 파일 전송
                        await TransferFilesRecursively(clientContext, oList, folderServerRelativeUrl, targetLibrary, targetFolderRelativeUrl);
                    }
                    else
                    {
                        // 파일 처리 로직 추가
                        string fileServerRelativeUrl = $"{oList.RootFolder.ServerRelativeUrl}/{sourceItemName}";
                        byte[] fileBytes = DownloadFileFromSource(oList, sourceItemName, fileServerRelativeUrl);
                        if (fileBytes != null)
                        {
                            UploadFileToTarget(targetLibrary, targetFolderName, fileBytes, sourceItemName);
                        }
                    }
                }
            }
        }



        private async Task TransferFilesRecursively(ClientContext clientContext, List oList, string folderServerRelativeUrl, string targetLibrary, string targetFolderRelativeUrl)
        {
            CamlQuery query = new CamlQuery
            {
                FolderServerRelativeUrl = folderServerRelativeUrl,
                ViewXml = "<View><RowLimit>100</RowLimit></View>"
            };

            ListItemCollection items;

            do
            {
                items = oList.GetItems(query);
                clientContext.Load(items);
                await clientContext.ExecuteQueryAsync();

                foreach (var item in items)
                {
                    if (item.FileSystemObjectType == FileSystemObjectType.File)
                    {
                        // 파일 다운로드 및 업로드
                        string fileName = item["FileLeafRef"].ToString();
                        byte[] fileBytes = DownloadFileFromSource(oList, fileName, folderServerRelativeUrl);

                        if (fileBytes != null)
                        {
                            UploadFileToTarget(targetLibrary, targetFolderRelativeUrl, fileBytes, fileName);
                        }
                    }
                    else if (item.FileSystemObjectType == FileSystemObjectType.Folder)
                    {
                        // 하위 폴더가 있는 경우 재귀 호출
                        string subFolderName = item["FileLeafRef"].ToString();
                        string subFolderServerRelativeUrl = $"{folderServerRelativeUrl}/{subFolderName}";

                        // 타겟에 하위 폴더가 없다면 생성
                        string subFolderTargetRelativeUrl = $"{targetFolderRelativeUrl}/{subFolderName}";
                        EnsureFolderExists(new ClientContext(targetSiteUrl), targetLibrary, subFolderTargetRelativeUrl);

                        await TransferFilesRecursively(clientContext, oList, subFolderServerRelativeUrl, targetLibrary, subFolderTargetRelativeUrl);
                    }
                }

                query.ListItemCollectionPosition = items.ListItemCollectionPosition;

            } while (query.ListItemCollectionPosition != null);
        }

        private void EnsureFolderExists(ClientContext clientContext, string libraryName, string folderRelativeUrl)
        {
            // folderRelativeUrl: 문서/General/2020/01
            clientContext.Credentials = new SharePointOnlineCredentials(targetUserName, GetSecureString(targetPassword));
            List library = clientContext.Web.Lists.GetByTitle(libraryName);
            clientContext.Load(library.RootFolder);
            clientContext.ExecuteQuery();

            Folder folder = clientContext.Web.GetFolderByServerRelativeUrl($"{library.RootFolder.ServerRelativeUrl}/{folderRelativeUrl}");
            clientContext.Load(folder);

            try
            {
                clientContext.ExecuteQuery();
            }
            catch (ServerException)
            {
                // 폴더가 없으면 상위 폴더에 새 폴더를 생성합니다.
                Folder parentFolder = library.RootFolder;
                string[] folderSegments = folderRelativeUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string segment in folderSegments)
                {
                    Folder newFolder = parentFolder.Folders.Add(segment);
                    clientContext.Load(newFolder);
                    parentFolder = newFolder;  // 다음 폴더의 상위 폴더를 설정
                }

                clientContext.ExecuteQuery();
            }
        }


        private byte[] DownloadFileFromSource(List oList, string fileName, string folderServerRelativeUrl)
        {
            using (ClientContext clientContext = new ClientContext(sourceSiteUrl))
            {
                clientContext.Credentials = new NetworkCredential(sourceUserName, sourcePassword, domain);
                string fileUrl = $"{folderServerRelativeUrl}/{fileName}";
                FileInformation fileInfo = Microsoft.SharePoint.Client.File.OpenBinaryDirect(clientContext, fileUrl);
                using (var stream = fileInfo.Stream)
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        public void UploadFileToTarget(string targetLibrary, string targetFolderName, byte[] fileBytes, string fileName)
        {
            using (ClientContext clientContext = new ClientContext(targetSiteUrl))
            {
                clientContext.Credentials = new SharePointOnlineCredentials(targetUserName, GetSecureString(targetPassword));

                // 대상 라이브러리 가져오기
                List targetList = clientContext.Web.Lists.GetByTitle(targetLibrary);
                clientContext.Load(targetList, list => list.RootFolder.ServerRelativeUrl);
                clientContext.ExecuteQuery();

                // 대상 라이브러리의 RootFolder 경로에 상대 경로 추가
                string fileServerRelativeUrl = $"{targetList.RootFolder.ServerRelativeUrl}/{targetFolderName}/{fileName}";

                // 파일 업로드
                Microsoft.SharePoint.Client.File.SaveBinaryDirect(clientContext, fileServerRelativeUrl, new MemoryStream(fileBytes), true);
            }
        }


        private SecureString GetSecureString(string password)
        {
            SecureString securePassword = new SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            return securePassword;
        }
    }

}