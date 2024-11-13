using Microsoft.SharePoint.Client;
using Share2Teams.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Threading.Tasks;

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
            using (ClientContext clientContext = new ClientContext(sourceSiteUrl))
            {
                Console.WriteLine(sourceUserName);
                clientContext.Credentials = new NetworkCredential(sourceUserName, sourcePassword, sourceDomain);
                List oList = clientContext.Web.Lists.GetByTitle(sourceLibrary);
                clientContext.Load(oList.RootFolder);
                await clientContext.ExecuteQueryAsync();

                foreach (var selectedItem in selectedItems)
                {
                    // selectedItem이 폴더인지 파일인지 확인
                    string itemServerRelativeUrl = $"{oList.RootFolder.ServerRelativeUrl}/{selectedItem}";

                    // CAML query to get the item by server relative URL
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

                    ListItemCollection items = oList.GetItems(query);
                    clientContext.Load(items);
                    await clientContext.ExecuteQueryAsync();

                    if (items.Count > 0)
                    {
                        ListItem item = items[0]; // Assume that the item exists

                        clientContext.Load(item, i => i.FileSystemObjectType);
                        await clientContext.ExecuteQueryAsync();

                        if (item.FileSystemObjectType == FileSystemObjectType.Folder)
                        {
                            // 폴더일 경우 재귀적으로 파일 전송
                            string targetFolderRelativeUrl = $"{targetFolderName}/{selectedItem}";
                            await TransferFilesRecursively(clientContext, oList, itemServerRelativeUrl, targetLibrary, targetFolderRelativeUrl);
                        }
                        else if (item.FileSystemObjectType == FileSystemObjectType.File)
                        {
                            // 파일일 경우 다운로드 및 업로드
                            byte[] fileBytes = DownloadFileFromSource(oList, selectedItem, itemServerRelativeUrl);
                            if (fileBytes != null)
                            {
                                UploadFileToTarget(targetLibrary, targetFolderName, fileBytes, selectedItem);
                            }
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
                Folder parentFolder = library.RootFolder;
                string[] folderSegments = folderRelativeUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string segment in folderSegments)
                {
                    Folder newFolder = parentFolder.Folders.Add(segment);
                    clientContext.Load(newFolder);
                    parentFolder = newFolder;
                }

                clientContext.ExecuteQuery();
            }
        }

        private byte[] DownloadFileFromSource(List oList, string fileName, string folderServerRelativeUrl)
        {
            using (ClientContext clientContext = new ClientContext(sourceSiteUrl))
            {
                clientContext.Credentials = new NetworkCredential(sourceUserName, sourcePassword, sourceDomain);
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

                List targetList = clientContext.Web.Lists.GetByTitle(targetLibrary);
                clientContext.Load(targetList, list => list.RootFolder.ServerRelativeUrl);
                clientContext.ExecuteQuery();

                string fileServerRelativeUrl = $"{targetList.RootFolder.ServerRelativeUrl}/{targetFolderName}/{fileName}";

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
