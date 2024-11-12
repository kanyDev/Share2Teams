# 메타데이터 복사 함수
function Copy-Metadata {
    param(
        [string]$sourceSiteUrl,
        [string]$targetSiteUrl,
        [string]$sourceLibraryName,
        [string]$targetLibraryName,
        [string]$fileName,
        [string]$sourceUsername,
        [string]$sourcePassword,
        [string]$targetUsername,
        [string]$targetPassword
    )

    # SharePoint 인증
    $secureSourcePassword = ConvertTo-SecureString $sourcePassword -AsPlainText -Force
    $sourceCredentials = New-Object System.Management.Automation.PSCredential($sourceUsername, $secureSourcePassword)

    $secureTargetPassword = ConvertTo-SecureString $targetPassword -AsPlainText -Force
    $targetCredentials = New-Object System.Management.Automation.PSCredential($targetUsername, $secureTargetPassword)

    # SharePoint 클라이언트 객체 생성
    $sourceCtx = New-Object Microsoft.SharePoint.Client.ClientContext($sourceSiteUrl)
    $sourceCtx.Credentials = $sourceCredentials
    $targetCtx = New-Object Microsoft.SharePoint.Client.ClientContext($targetSiteUrl)
    $targetCtx.Credentials = $targetCredentials

    # 상대 경로 생성 (라이브러리 이름과 파일 이름을 기반으로)
    $sourceRelativeUrl = "/sites/yourSite/$sourceLibraryName/$fileName"
    $targetRelativeUrl = "/sites/yourSite/$targetLibraryName/$fileName"

    # 소스 파일 및 타겟 파일 가져오기
    $sourceFile = $sourceCtx.Web.GetFileByServerRelativeUrl($sourceRelativeUrl)
    $targetFile = $targetCtx.Web.GetFileByServerRelativeUrl($targetRelativeUrl)

    # 메타데이터 복사
    $sourceCtx.Load($sourceFile)
    $targetCtx.Load($targetFile)
    $sourceCtx.ExecuteQuery()
    $targetCtx.ExecuteQuery()

    $fields = @("Author", "Editor", "Created", "Modified")  # 복사할 메타데이터 필드

    foreach ($field in $fields) {
        $value = $sourceFile.ListItemAllFields[$field]
        $targetFile.ListItemAllFields[$field] = $value
    }

    $targetFile.ListItemAllFields.Update()
    $targetCtx.ExecuteQuery()

    Write-Host "메타데이터 복사 완료"
}

# 권한 복사 함수
function Copy-Permissions {
    param(
        [string]$sourceSiteUrl,
        [string]$targetSiteUrl,
        [string]$sourceLibraryName,
        [string]$targetLibraryName,
        [string]$fileName,
        [string]$sourceUsername,
        [string]$sourcePassword,
        [string]$targetUsername,
        [string]$targetPassword
    )

    # SharePoint 인증
    $secureSourcePassword = ConvertTo-SecureString $sourcePassword -AsPlainText -Force
    $sourceCredentials = New-Object System.Management.Automation.PSCredential($sourceUsername, $secureSourcePassword)

    $secureTargetPassword = ConvertTo-SecureString $targetPassword -AsPlainText -Force
    $targetCredentials = New-Object System.Management.Automation.PSCredential($targetUsername, $secureTargetPassword)

    # SharePoint 클라이언트 객체 생성
    $sourceCtx = New-Object Microsoft.SharePoint.Client.ClientContext($sourceSiteUrl)
    $sourceCtx.Credentials = $sourceCredentials
    $targetCtx = New-Object Microsoft.SharePoint.Client.ClientContext($targetSiteUrl)
    $targetCtx.Credentials = $targetCredentials

    # 상대 경로 생성 (라이브러리 이름과 파일 이름을 기반으로)
    $sourceRelativeUrl = "/sites/yourSite/$sourceLibraryName/$fileName"
    $targetRelativeUrl = "/sites/yourSite/$targetLibraryName/$fileName"

    # 소스 파일 및 타겟 파일 가져오기
    $sourceFile = $sourceCtx.Web.GetFileByServerRelativeUrl($sourceRelativeUrl)
    $targetFile = $targetCtx.Web.GetFileByServerRelativeUrl($targetRelativeUrl)

    # 권한 복사
    $sourceCtx.Load($sourceFile)
    $targetCtx.Load($targetFile)
    $sourceCtx.ExecuteQuery()
    $targetCtx.ExecuteQuery()

    # 소스 파일 권한 가져오기
    $sourcePermissions = $sourceFile.ListItemAllFields.RoleAssignments

    foreach ($sourcePermission in $sourcePermissions) {
        # 권한을 대상 파일에 적용
        $targetFile.ListItemAllFields.RoleAssignments.Add($sourcePermission.Member, $sourcePermission.RoleDefinitionBindings)
    }

    $targetFile.ListItemAllFields.Update()
    $targetCtx.ExecuteQuery()

    Write-Host "권한 복사 완료"
}

# 버전 기록 복사 함수
function Copy-VersionHistory {
    param(
        [string]$sourceSiteUrl,
        [string]$targetSiteUrl,
        [string]$sourceLibraryName,
        [string]$targetLibraryName,
        [string]$fileName,
        [string]$sourceUsername,
        [string]$sourcePassword,
        [string]$targetUsername,
        [string]$targetPassword
    )

    # SharePoint 인증
    $secureSourcePassword = ConvertTo-SecureString $sourcePassword -AsPlainText -Force
    $sourceCredentials = New-Object System.Management.Automation.PSCredential($sourceUsername, $secureSourcePassword)

    $secureTargetPassword = ConvertTo-SecureString $targetPassword -AsPlainText -Force
    $targetCredentials = New-Object System.Management.Automation.PSCredential($targetUsername, $secureTargetPassword)

    # SharePoint 클라이언트 객체 생성
    $sourceCtx = New-Object Microsoft.SharePoint.Client.ClientContext($sourceSiteUrl)
    $sourceCtx.Credentials = $sourceCredentials
    $targetCtx = New-Object Microsoft.SharePoint.Client.ClientContext($targetSiteUrl)
    $targetCtx.Credentials = $targetCredentials

    # 상대 경로 생성 (라이브러리 이름과 파일 이름을 기반으로)
    $sourceRelativeUrl = "/sites/yourSite/$sourceLibraryName/$fileName"
    $targetRelativeUrl = "/sites/yourSite/$targetLibraryName/$fileName"

    # 소스 파일 및 타겟 파일 가져오기
    $sourceFile = $sourceCtx.Web.GetFileByServerRelativeUrl($sourceRelativeUrl)
    $targetFile = $targetCtx.Web.GetFileByServerRelativeUrl($targetRelativeUrl)

    # 버전 기록 복사
    $sourceCtx.Load($sourceFile)
    $targetCtx.Load($targetFile)
    $sourceCtx.ExecuteQuery()
    $targetCtx.ExecuteQuery()

    $versions = $sourceFile.Versions
    $sourceCtx.Load($versions)
    $sourceCtx.ExecuteQuery()

    foreach ($version in $versions) {
        # 버전 정보를 대상 파일로 복사
        $targetFile.Versions.Add($version.VersionLabel, $version.Created, $version.CreatedBy)
    }

    $targetFile.Update()
    $targetCtx.ExecuteQuery()

    Write-Host "버전 기록 복사 완료"
}

# 메타데이터, 권한, 버전 기록을 한 번에 처리하는 함수
function Copy-All {
    param(
        [string]$sourceSiteUrl,
        [string]$targetSiteUrl,
        [string]$sourceLibraryName,
        [string]$targetLibraryName,
        [string]$fileName,
        [string]$sourceUsername,
        [string]$sourcePassword,
        [string]$targetUsername,
        [string]$targetPassword
    )

    # 메타데이터 복사
    Copy-Metadata -sourceSiteUrl $sourceSiteUrl -targetSiteUrl $targetSiteUrl -sourceLibraryName $sourceLibraryName -targetLibraryName $targetLibraryName -fileName $fileName -sourceUsername $sourceUsername -sourcePassword $sourcePassword -targetUsername $targetUsername -targetPassword $targetPassword

    # 권한 복사
    Copy-Permissions -sourceSiteUrl $sourceSiteUrl -targetSiteUrl $targetSiteUrl -sourceLibraryName $sourceLibraryName -targetLibraryName $targetLibraryName -fileName $fileName -sourceUsername $sourceUsername -sourcePassword $sourcePassword -targetUsername $targetUsername -targetPassword $targetPassword

    # 버전 기록 복사
    Copy-VersionHistory -sourceSiteUrl $sourceSiteUrl -targetSiteUrl $targetSiteUrl -sourceLibraryName $sourceLibraryName -targetLibraryName $targetLibraryName -fileName $fileName -sourceUsername $sourceUsername -sourcePassword $sourcePassword -targetUsername $targetUsername -targetPassword $targetPassword

    Write-Host "모든 파일 복사 작업 완료"
}

# Copy-All 호출 예시
Copy-All -sourceSiteUrl "https://sourceSiteUrl" -targetSiteUrl "https://targetSiteUrl" -sourceLibraryName "Shared Documents" -targetLibraryName "Shared Documents" -fileName "exampleFile.txt" -sourceUsername "sourceUser@example.com" -sourcePassword "sourcePassword123" -targetUsername "targetUser@example.com" -targetPassword "targetPassword123"
