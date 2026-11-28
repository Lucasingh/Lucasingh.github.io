param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$NotesFile,
    [string]$Token = $env:GITHUB_TOKEN,
    [switch]$SkipPush
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$repo = 'Lucasingh/Lucasingh.github.io'
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$iscc = 'C:\Program Files\Inno Setup 7\ISCC.exe'

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "版本号格式应为 x.y.z，当前为 $Version" }
if (-not $NotesFile) { $NotesFile = Join-Path $root "release-notes\v$Version.md" }
if (-not (Test-Path $NotesFile)) { throw "缺少更新说明文件：$NotesFile。请先写好本次更新内容再发布。" }
if (-not (Test-Path $iscc)) { throw "未找到 Inno Setup 编译器：$iscc" }

$notes = Get-Content $NotesFile -Raw -Encoding UTF8

Write-Host "[1/6] 更新版本号到 $Version"
$csproj = Join-Path $root 'src\AovoCL.Desktop\AovoCL.Desktop.csproj'
$iss = Join-Path $root 'installer\AovoCL.iss'
(Get-Content $csproj -Raw -Encoding UTF8) -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>" |
    Set-Content $csproj -Encoding UTF8 -NoNewline
(Get-Content $iss -Raw -Encoding UTF8) -replace '#define MyAppVersion "[^"]+"', "#define MyAppVersion ""$Version""" |
    Set-Content $iss -Encoding UTF8 -NoNewline

Write-Host "[2/6] 发布程序"
$publish = Join-Path $root "dist\publish-v$Version"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& $dotnet publish (Join-Path $root 'src\AovoCL.Desktop\AovoCL.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $publish --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }

Write-Host "[3/6] 打包安装程序"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup 打包失败' }
$setup = Join-Path $root "dist\AovoCL-Setup-$Version-win-x64.exe"
if (-not (Test-Path $setup)) { throw "安装包未生成：$setup" }

Write-Host "[4/6] 提交版本变更"
Push-Location $root
git add 'src/AovoCL.Desktop/AovoCL.Desktop.csproj' 'installer/AovoCL.iss' $NotesFile
git commit -m "chore(release): v$Version" | Out-Null
git tag -f "v$Version" | Out-Null
if (-not $SkipPush) {
    git push origin main
    git push -f origin "v$Version"
}
Pop-Location

Write-Host "[5/6] 创建 GitHub Release"
if ($Token) {
    $headers = @{ Authorization = "Bearer $Token"; 'User-Agent' = 'AovoCL-Release' }
    $body = @{
        tag_name = "v$Version"
        name     = "AovoCL $Version"
        body     = $notes
    } | ConvertTo-Json -Depth 3
    $release = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$repo/releases" `
        -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
    $uploadUrl = ($release.upload_url -replace '\{\?name,label\}', '') + "?name=$(Split-Path $setup -Leaf)"
    Invoke-RestMethod -Method Post -Uri $uploadUrl -Headers $headers `
        -ContentType 'application/vnd.github+json' -InFile $setup | Out-Null
    Write-Host "已创建 Release 并上传安装包：$($release.html_url)"
}
else {
    Write-Host '未提供 GITHUB_TOKEN，请手动在 GitHub Releases 创建 ' -NoNewline
    Write-Host "v$Version 并上传 $setup" -NoNewline
    Write-Host '，更新说明见：' -NoNewline
    Write-Host $NotesFile
}

Write-Host "[6/6] 完成：$setup"
