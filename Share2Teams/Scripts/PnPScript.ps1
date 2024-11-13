# CSOM 라이브러리 경로
$clientDllPath = "C:\Users\jaypa\source\repos\Share2Teams\packages\Microsoft.SharePointOnline.CSOM.16.1.25409.12000\lib\netstandard2.0\Microsoft.SharePoint.Client.dll"
$clientRuntimeDllPath = "C:\Users\jaypa\source\repos\Share2Teams\packages\Microsoft.SharePointOnline.CSOM.16.1.25409.12000\lib\netstandard2.0\Microsoft.SharePoint.Client.Runtime.dll"

# PowerShell에서 CSOM 라이브러리 로드
Add-Type -Path $clientDllPath
Add-Type -Path $clientRuntimeDllPath


# 사용자 인증 정보를 입력받습니다.
$sourceSiteUrl = "https://source.sharepoint.com/sites/yoursite"
$targetSiteUrl = "https://target.sharepoint.com/sites/yoursite"
$sourceUserName = "username@domain.com"
$sourcePassword = "password"
$domain = "domain"
$targetUserName = "username@domain.com"
$targetPassword = "password"

# CSOM 인증을 위한 클라이언트 컨텍스트 생성
$sourceContext = New-Object Microsoft.SharePoint.Client.ClientContext($sourceSiteUrl)
$sourceContext.Credentials = New-Object System.Net.NetworkCredential($sourceUserName, $sourcePassword, $domain)

$targetContext = New-Object Microsoft.SharePoint.Client.ClientContext($targetSiteUrl)
$targetContext.Credentials = New-Object System.Net.NetworkCredential($targetUserName, $targetPassword, $domain)

# 파일 다운로드 함수
function Download-FileFromSource($fileName, $sourceFolderUrl) {
    $fileUrl = "$sourceFolderUrl/$fileName"
    $fileInfo = [Microsoft.SharePoint.Client.File]::OpenBinaryDirect($sourceContext, $fileUrl)
    $fileStream = $fileInfo.Stream
    $memoryStream = New-Object System.IO.MemoryStream
    $fileStream.CopyTo($memoryStream)
    return $memoryStream.ToArray()
}

# 파일 업로드 및 메타데이터 업데이트 함수
function Upload-FileToTarget($fileBytes, $fileName, $targetFolderUrl, $targetLibrary) {
    $targetList = $targetContext.Web.Lists.GetByTitle($targetLibrary)
    $targetFolder = $targetContext.Web.GetFolderByServerRelativeUrl($targetFolderUrl)
    $targetContext.Load($targetList)
    $targetContext.Load($targetFolder)
    $targetContext.ExecuteQuery()

    $fileUrl = "$targetFolder.ServerRelativeUrl/$fileName"
    $fileCreationInfo = New-Object Microsoft.SharePoint.Client.FileCreationInformation
    $fileCreationInfo.Content = $fileBytes
    $fileCreationInfo.Url = $fileUrl
    $uploadFile = $targetFolder.Files.Add($fileCreationInfo)
    $targetContext.Load($uploadFile)
    $targetContext.ExecuteQuery()

    # 메타데이터 업데이트 (작성자, 작성일자 등)
    $listItem = $uploadFile.ListItemAllFields
    $listItem["Author"] = $targetContext.Web.CurrentUser
    $listItem["Created"] = [System.DateTime]::Now
    $listItem.Update()
    $targetContext.ExecuteQuery()

    # 버전 관리 활성화 (만약 버전 관리가 필요한 경우)
    $uploadFile.Versions.Add()
    $targetContext.ExecuteQuery()

    Write-Host "File '$fileName' uploaded to '$targetFolderUrl' with metadata and versioning enabled."
}

# 파일 및 메타데이터 전송 함수
function Transfer-File($sourceFolderUrl, $targetFolderUrl, $fileName, $targetLibrary) {
    $fileBytes = Download-FileFromSource -fileName $fileName -sourceFolderUrl $sourceFolderUrl
    Upload-FileToTarget -fileBytes $fileBytes -fileName $fileName -targetFolderUrl $targetFolderUrl -targetLibrary $targetLibrary
}

# 소스 및 타겟 폴더 경로
$sourceFolderUrl = "/sites/yoursite/Shared Documents/SourceFolder"
$targetFolderUrl = "/sites/yoursite/Shared Documents/TargetFolder"
$targetLibrary = "Documents"

# 파일 이름 리스트 (예시)
$fileNames = @("file1.txt", "file2.txt", "file3.txt")

# 각 파일에 대해 메타데이터, 버전, 권한을 전송
foreach ($fileName in $fileNames) {
    Transfer-File -sourceFolderUrl $sourceFolderUrl -targetFolderUrl $targetFolderUrl -fileName $fileName -targetLibrary $targetLibrary
}
