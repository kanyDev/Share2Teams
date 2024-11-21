using Microsoft.SharePoint.Client;
using Share2Teams.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.SharePoint.News.DataModel;
using System.Linq;
using System.Data;


namespace Share2Teams.Services
{
    public class FileTransfer
    {
        private string sourceSiteUrl;
        private string sourceUserName;
        private string sourcePassword;
        private string sourceDomain;

        private string targetSiteUrl;
        private string targetUserName;
        private string targetPassword;


        private string sourceLibrary;
        private string targetLibrary;
        private List<string> selectedItems;
        private string targetFolderName;

        public FileTransfer(FileTransferViewModel model)
        {
            this.sourceSiteUrl = model.SourceSiteUrl;
            this.sourceUserName = model.SourceUsername;
            this.sourcePassword = model.SourcePassword;
            this.sourceDomain = model.SourceDomain;

            this.targetSiteUrl = model.TargetSiteUrl;
            this.targetUserName = model.TargetUsername;
            this.targetPassword = model.TargetPassword;

            this.sourceLibrary = model.SourceLibrary;
            this.targetLibrary = model.TargetLibrary;
            this.selectedItems = model.SelectedItems;
            this.targetFolderName = model.TargetFolderName;


        }

        // 파일(들) 혹은 폴더 선택 가능
        public async Task TransferFilesAsync(
            string sourceLibrary,
            List<string> selectedItems,
            string targetLibrary,
            string targetFolderName)
        {
            using (ClientContext sourceContext = new ClientContext(sourceSiteUrl))
            using (ClientContext targetContext = new ClientContext(targetSiteUrl))
            {
                // 인증 정보 설정
                sourceContext.Credentials = new NetworkCredential(sourceUserName, sourcePassword, sourceDomain);
                targetContext.Credentials = new SharePointOnlineCredentials(targetUserName, GetSecureString(targetPassword));

                List sourceList = sourceContext.Web.Lists.GetByTitle(sourceLibrary);
                sourceContext.Load(sourceList.RootFolder);
                await sourceContext.ExecuteQueryAsync();


                foreach (var selectedItem in selectedItems)
                {
                    string itemServerRelativeUrl = $"{sourceList.RootFolder.ServerRelativeUrl}/{selectedItem}";

                    CamlQuery query = new CamlQuery
                    {
                        ViewXml = $@"
                        <View>
                            <Query>
                                <Where>
                                    <Eq>
                                        <FieldRef Name='FileRef' />
                                        <Value Type='Text'>{itemServerRelativeUrl}</Value>
                                    </Eq>
                                </Where>
                            </Query>
                        </View>"
                    };

                    ListItemCollection items = sourceList.GetItems(query);
                    sourceContext.Load(items);
                    await sourceContext.ExecuteQueryAsync();

                    if (items.Count > 0)
                    {
                        ListItem item = items[0];

                        sourceContext.Load(item, i => i.FileSystemObjectType, i => i["Created"], i => i["Modified"]);
                        await sourceContext.ExecuteQueryAsync();

                        if (item.FileSystemObjectType == FileSystemObjectType.Folder)
                        {
                            // 폴더일 경우
                            string newTargetFolder = $"{targetFolderName}/{selectedItem}";
                            await TransferFilesAsync(sourceLibrary, new List<string> { selectedItem }, targetLibrary, newTargetFolder);
                        }
                        else if (item.FileSystemObjectType == FileSystemObjectType.File)
                        {
                            // 파일일 경우
                            string sourceFileUrl = itemServerRelativeUrl;

                            // 버전 및 현재 파일 처리
                            await HandleFileTransferAsync(sourceContext, targetContext, sourceFileUrl, targetLibrary, targetFolderName, selectedItem);
                        }
                    }
                }
            }
        }

        private async Task HandleFileTransferAsync(
            ClientContext sourceContext,
            ClientContext targetContext,
            string sourceFileUrl,
            string targetLibrary,
            string targetFolderName,
            string selectedItem)
        {
            var sourceFile = sourceContext.Web.GetFileByServerRelativeUrl(sourceFileUrl);
            sourceContext.Load(sourceFile, file => file.Versions, file => file.ListItemAllFields);
            await sourceContext.ExecuteQueryAsync();


            if (sourceFile.Versions.Count > 0)
            {
                Debug.WriteLine("버전 파일 존재. 버전 이관 처리 시작.");
                await MigrateFileVersions(sourceContext, targetContext, sourceFile, targetLibrary, targetFolderName, selectedItem);
            }

            
            Debug.WriteLine("현재 파일 이관 처리 시작.");
            await MigrateCurrentFile(sourceContext, targetContext, sourceFileUrl, sourceFile, targetLibrary, targetFolderName, selectedItem);
        }

        private async Task MigrateFileVersions(
            ClientContext sourceContext,
            ClientContext targetContext,
            Microsoft.SharePoint.Client.File sourceFile,
            string targetLibrary,
            string targetFolderName,
            string selectedItem,
            int largeFileThreshold = 1024 * 1024 * 10) // 10MB 임계값
        {
            foreach (var version in sourceFile.Versions)
            {
                sourceContext.Load(version, v => v.VersionLabel, v => v.Created, v => v.CheckInComment);
                await sourceContext.ExecuteQueryAsync();

                // 버전 파일 다운로드
                byte[] versionBytes = await DownloadVersionFile(sourceContext, version);

                /*
                // 파일 크기에 따라 업로드 방식 결졍
                long fileSize = versionBytes.Length;
                if (fileSize > largeFileThreshold)
                {
                    Debug.WriteLine("Large File Upload");
                    //await LargeFileUploader()
                }
                else
                {
                    Debug.WriteLine("Small File Upload");
                    SmallFileUploader(targetContext, targetLibrary, targetFolderName, versionBytes, selectedItem);
                }
                */
                List targetList = targetContext.Web.Lists.GetByTitle(targetLibrary);
                targetContext.Load(targetList, list => list.RootFolder.ServerRelativeUrl);
                targetContext.ExecuteQuery();

                string targetFolderRelativeUrl = $"{targetList.RootFolder.ServerRelativeUrl}/{targetFolderName}";

                var targetFolder = targetContext.Web.GetFolderByServerRelativeUrl($"{targetFolderRelativeUrl}");
                FileCreationInformation fileInfo = new FileCreationInformation
                {
                    Content = versionBytes,
                    Overwrite = true,
                    Url = $"{selectedItem}"
                };

                // 파일 업로드
                var uploadFile = targetFolder.Files.Add(fileInfo);

                Debug.WriteLine($"{selectedItem} 버전 : {version.VersionLabel} 다운로드 ,  version.Created: {version.Created}");

                targetContext.Load(uploadFile);
                await targetContext.ExecuteQueryAsync();

                // 메타데이터 설정
                ListItem targetItem = uploadFile.ListItemAllFields;
                
                DateTime createdTime = version.Created;

                createdTime = createdTime.AddHours(17);

                targetItem["Modified"] = createdTime;
                targetItem["Created"] = createdTime;

                targetItem.UpdateOverwriteVersion();
                await targetContext.ExecuteQueryAsync();

            }
        }
        private async Task<byte[]> DownloadVersionFile(ClientContext sourceContext, FileVersion version)
        {
            // 버전 파일 다운로드는 FileVersion.OpenBinaryStream() 으로만 가능, version.Url 로 다운로드 불가능
            var versionStream = version.OpenBinaryStream();
            await sourceContext.ExecuteQueryAsync();

            using (var memoryStream = new MemoryStream())
            {
                versionStream.Value.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private async Task MigrateCurrentFile(
            ClientContext sourceContext,
            ClientContext targetContext,
            string sourceFileUrl,
            Microsoft.SharePoint.Client.File sourceFile,
            string targetLibrary,
            string targetFolderName,
            string targetFileName)
        {
            var fileStream = sourceFile.OpenBinaryStream();
            await sourceContext.ExecuteQueryAsync();

            byte[] currentFileBytes = DownloadFile(sourceContext, sourceFileUrl);

            SmallFileUploader(targetContext, targetLibrary, targetFolderName, currentFileBytes, targetFileName);

            // 대상 라이브러리와 파일 가져오기
            List targetList = targetContext.Web.Lists.GetByTitle(targetLibrary);
            targetContext.Load(targetList, list => list.RootFolder.ServerRelativeUrl);
            targetContext.ExecuteQuery();

            string targetFolderRelativeUrl = $"{targetList.RootFolder.ServerRelativeUrl}/{targetFolderName}/{targetFileName}";

            Microsoft.SharePoint.Client.File targetFile = targetContext.Web.GetFileByServerRelativeUrl(targetFolderRelativeUrl);
            targetContext.Load(targetFile);
            targetContext.ExecuteQuery();

            // 메타데이터 복사
            ListItem targetItem = targetFile.ListItemAllFields;
            
            targetItem["Modified"] = sourceFile.ListItemAllFields["Modified"];
            targetItem["Created"] = sourceFile.ListItemAllFields["Created"];
            targetItem.UpdateOverwriteVersion();
            await targetContext.ExecuteQueryAsync();
        }


        private void EnsureFolderExists(ClientContext clientContext, string libraryName, string folderRelativeUrl)
        {
            clientContext.Credentials = new SharePointOnlineCredentials(targetUserName, GetSecureString(targetPassword));
            List library = clientContext.Web.Lists.GetByTitle(libraryName);
            clientContext.Load(library.RootFolder);
            clientContext.ExecuteQuery();

            Microsoft.SharePoint.Client.Folder folder = clientContext.Web.GetFolderByServerRelativeUrl($"{library.RootFolder.ServerRelativeUrl}/{folderRelativeUrl}");
            clientContext.Load(folder);

            try
            {
                clientContext.ExecuteQuery();
            }
            catch (ServerException)
            {
                Microsoft.SharePoint.Client.Folder parentFolder = library.RootFolder;
                string[] folderSegments = folderRelativeUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string segment in folderSegments)
                {
                    Microsoft.SharePoint.Client.Folder newFolder = parentFolder.Folders.Add(segment);
                    clientContext.Load(newFolder);
                    parentFolder = newFolder;
                }

                clientContext.ExecuteQuery();
            }
        }

    
        // 용량 상관없이 가능 
        private byte[] DownloadFile(ClientContext sourceContext, string itemServerRelativeUrl)
        {
            FileInformation fileInfo = Microsoft.SharePoint.Client.File.OpenBinaryDirect(sourceContext, itemServerRelativeUrl);
            using (var stream = fileInfo.Stream)
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public void SmallFileUploader(
            ClientContext targetContext
            , string targetLibrary
            , string targetFolderName
            , byte[] fileBytes
            , string selectedItem)
        {
            List targetList = targetContext.Web.Lists.GetByTitle(targetLibrary);
            targetContext.Load(targetList, list => list.RootFolder.ServerRelativeUrl);


            targetContext.ExecuteQuery();

            string targetFileRelativeUrl = $"{targetList.RootFolder.ServerRelativeUrl}/{targetFolderName}/{selectedItem}";

            // 파일 데이터 복사
            Microsoft.SharePoint.Client.File.SaveBinaryDirect(targetContext, targetFileRelativeUrl, new MemoryStream(fileBytes), true);

            
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
