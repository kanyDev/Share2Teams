using Microsoft.SharePoint.Client;
using System;
using System.IO;
using System.Security;
using System.Threading.Tasks;

public class BulkFileTransfer
{
    private ClientContext clientContext;
    private SharePointOnlineCredentials credentials;

    public BulkFileTransfer(string siteUrl, string userName, string password)
    {
        this.clientContext = new ClientContext(siteUrl);
        this.credentials = new SharePointOnlineCredentials(userName, GetSecureString(password));
        this.clientContext.Credentials = this.credentials;
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

    public async Task<ListItemCollection> GetItemsAsync(string libraryName, string folderServerRelativeUrl)
    {
        List oList = clientContext.Web.Lists.GetByTitle(libraryName);
        CamlQuery query = new CamlQuery
        {
            FolderServerRelativeUrl = folderServerRelativeUrl,
            ViewXml = "<View><RowLimit>100</RowLimit></View>"
        };

        ListItemCollection items = oList.GetItems(query);
        clientContext.Load(items);
        await clientContext.ExecuteQueryAsync();
        return items;
    }

    public async Task<byte[]> DownloadFileAsync(string fileUrl)
    {
        FileInformation fileInfo = Microsoft.SharePoint.Client.File.OpenBinaryDirect(clientContext, fileUrl);
        using (var stream = fileInfo.Stream)
        using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }

    public async Task UploadFileAsync(string libraryName, string folderRelativeUrl, byte[] fileBytes, string fileName)
    {
        List targetList = clientContext.Web.Lists.GetByTitle(libraryName);
        clientContext.Load(targetList, list => list.RootFolder.ServerRelativeUrl);
        await clientContext.ExecuteQueryAsync();

        string fileServerRelativeUrl = $"{targetList.RootFolder.ServerRelativeUrl}/{folderRelativeUrl}/{fileName}";
        await UploadFileInChunksAsync(fileServerRelativeUrl, fileBytes);
    }

    private async Task UploadFileInChunksAsync(string fileServerRelativeUrl, byte[] fileBytes)
    {
        int chunkSize = 10485760; // 10MB
        long fileSize = fileBytes.Length;
        long offset = 0;
        Guid uploadId = Guid.NewGuid();

        using (MemoryStream stream = new MemoryStream(fileBytes))
        {
            Microsoft.SharePoint.Client.File uploadFile = null;

            while (offset < fileSize)
            {
                long remainingBytes = fileSize - offset;
                int bytesToRead = (int)Math.Min(chunkSize, remainingBytes);
                byte[] buffer = new byte[bytesToRead];
                stream.Read(buffer, 0, bytesToRead);

                using (MemoryStream chunkStream = new MemoryStream(buffer))
                {
                    if (offset == 0)
                    {
                        // Start upload
                        uploadFile = Microsoft.SharePoint.Client.File.CreateUploadSession(clientContext, fileServerRelativeUrl, uploadId, chunkStream);
                        await clientContext.ExecuteQueryAsync();
                    }
                    else if (remainingBytes <= chunkSize)
                    {
                        // Finish upload
                        uploadFile = uploadFile.FinishUpload(clientContext, uploadId, offset, chunkStream);
                        await clientContext.ExecuteQueryAsync();
                    }
                    else
                    {
                        // Continue upload
                        uploadFile = uploadFile.ContinueUpload(clientContext, uploadId, offset, chunkStream);
                        await clientContext.ExecuteQueryAsync();
                    }

                    offset += bytesToRead;
                }
            }
        }
    }

    public async Task EnsureFolderExistsAsync(string libraryName, string folderRelativeUrl)
    {
        List library = clientContext.Web.Lists.GetByTitle(libraryName);
        clientContext.Load(library.RootFolder);
        await clientContext.ExecuteQueryAsync();

        Folder folder = clientContext.Web.GetFolderByServerRelativeUrl($"{library.RootFolder.ServerRelativeUrl}/{folderRelativeUrl}");
        clientContext.Load(folder);

        try
        {
            await clientContext.ExecuteQueryAsync();
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

            await clientContext.ExecuteQueryAsync();
        }
    }
}
