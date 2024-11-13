param(
    [string]$sourceSiteUrl,
    [string]$targetSiteUrl,
    [string]$sourceLibraryName,
    [string]$targetLibraryName,
    [array]$itemNames,  # 복수 항목을 리스트로 받음
    [bool]$isFolder,
    [string]$sourceUsername,
    [string]$sourcePassword,
    [string]$targetUsername,
    [string]$targetPassword
)

# 소스 사이트에 연결
Connect-PnPOnline -Url $sourceSiteUrl -Credentials (New-Object System.Management.Automation.PSCredential($sourceUsername, (ConvertTo-SecureString $sourcePassword -AsPlainText -Force)))

foreach ($itemName in $itemNames)
{
    if ($isFolder) {
        # 폴더 이관 로직
        $sourceFolder = Get-PnPFolder -Url $sourceLibraryName/$itemName
        $targetFolder = Ensure-PnPFolder -FolderSiteRelativeUrl "$targetLibraryName/$itemName" -List $targetLibraryName

        # 폴더 내 파일 이관
        $files = Get-PnPFolderItem -Folder $sourceFolder
        foreach ($file in $files) {
            Copy-PnPFile -SourceUrl $file.ServerRelativeUrl -TargetUrl "$targetFolder.ServerRelativeUrl/$file.Name" -OverwriteIfAlreadyExists
            # 메타데이터 및 권한 이관
            Set-PnPListItem -List $targetLibraryName -Identity $file.ListItemAllFields.Id -Values @{
                "Author" = $file["Author"]
                "Editor" = $file["Editor"]
            }
        }
    } else {
        # 파일 이관 로직
        $sourceFile = Get-PnPFile -Url $sourceLibraryName/$itemName
        $targetFile = Copy-PnPFile -SourceUrl $sourceFile.ServerRelativeUrl -TargetUrl "$targetLibraryName/$itemName" -OverwriteIfAlreadyExists
        # 메타데이터 및 권한 이관
        Set-PnPListItem -List $targetLibraryName -Identity $targetFile.ListItemAllFields.Id -Values @{
            "Author" = $sourceFile["Author"]
            "Editor" = $sourceFile["Editor"]
        }
    }
}
