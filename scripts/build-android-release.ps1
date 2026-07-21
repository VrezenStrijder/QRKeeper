param(
    [ValidateSet("apk", "aab")]
    [string]$PackageFormat = "apk",

    [ValidateSet("android-arm64", "android-arm", "android-x64", "android-x86")]
    [string]$RuntimeIdentifier = "android-arm64",

    [string]$Configuration = "Release",

    [string]$OutputDirectory = "",

    [string]$KeyStore = "",

    [string]$StorePass = "",

    [string]$KeyAlias = "",

    [string]$KeyPass = "",

    [string]$VersionName = "",

    [int]$VersionCode = 0,

    [switch]$NoVersionPrompt,

    [switch]$NoPause
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Text)
    Write-Host "[*] $Text" -ForegroundColor Yellow
}

function Write-Ok {
    param([string]$Text)
    Write-Host "[OK] $Text" -ForegroundColor Green
}

function Pause-Script {
    if ($NoPause -or -not [Environment]::UserInteractive) {
        return
    }

    Write-Host "Press Enter to close..." -ForegroundColor DarkGray
    [void][Console]::ReadLine()
}

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-ProjectVersions {
    param([string]$ProjectPath)

    [xml]$project = Get-Content -Raw $ProjectPath
    $displayVersion = $project.Project.PropertyGroup |
        ForEach-Object { $_.ApplicationDisplayVersion } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($displayVersion)) {
        $displayVersion = "1.0.0"
    }

    $versionCodeText = $project.Project.PropertyGroup |
        ForEach-Object { $_.ApplicationVersion } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    $versionCode = 1
    if (-not [int]::TryParse($versionCodeText, [ref]$versionCode) -or $versionCode -lt 1) {
        $versionCode = 1
    }

    return [pscustomobject]@{
        DisplayVersion = [string]$displayVersion
        VersionCode = $versionCode
    }
}

function Set-OrCreate-ChildText {
    param(
        [System.Xml.XmlDocument]$Document,
        [System.Xml.XmlElement]$Parent,
        [string]$Name,
        [string]$Value
    )

    $node = $Parent.SelectSingleNode($Name)
    if ($null -eq $node) {
        $node = $Document.CreateElement($Name)
        [void]$Parent.AppendChild($node)
    }

    $node.InnerText = $Value
}

function Set-ProjectVersions {
    param(
        [string]$ProjectPath,
        [string]$DisplayVersion,
        [int]$VersionCode
    )

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($ProjectPath)

    $propertyGroup = @($document.Project.PropertyGroup) |
        Where-Object { $_.ApplicationDisplayVersion -or $_.ApplicationVersion } |
        Select-Object -First 1

    if ($null -eq $propertyGroup) {
        $propertyGroup = @($document.Project.PropertyGroup) | Select-Object -First 1
    }

    if ($null -eq $propertyGroup) {
        throw "No PropertyGroup found in Android project: $ProjectPath"
    }

    Set-OrCreate-ChildText -Document $document -Parent $propertyGroup -Name "ApplicationDisplayVersion" -Value $DisplayVersion
    Set-OrCreate-ChildText -Document $document -Parent $propertyGroup -Name "ApplicationVersion" -Value ([string]$VersionCode)

    $writerSettings = New-Object System.Xml.XmlWriterSettings
    $writerSettings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writerSettings.Indent = $true
    $writerSettings.OmitXmlDeclaration = $true

    $writer = [System.Xml.XmlWriter]::Create($ProjectPath, $writerSettings)
    try {
        $document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

function Set-CoreAppVersion {
    param(
        [string]$ConstantsPath,
        [string]$DisplayVersion
    )

    if (-not (Test-Path $ConstantsPath -PathType Leaf)) {
        throw "App constants file not found: $ConstantsPath"
    }

    $content = Get-Content -Raw -Encoding UTF8 $ConstantsPath
    $pattern = 'public const string AppVersion = "[^"]+";'
    if ($content -notmatch $pattern) {
        throw "AppConstants.AppVersion was not found in $ConstantsPath"
    }

    $updated = $content -replace $pattern, "public const string AppVersion = `"$DisplayVersion`";"
    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($ConstantsPath, $updated, [System.Text.UTF8Encoding]::new($false))
    }
}

function Test-VersionName {
    param([string]$Value)

    return $Value -match "^[0-9A-Za-z][0-9A-Za-z._+-]*$"
}

function Resolve-ReleaseVersions {
    param([object]$CurrentVersions)

    Write-Step "Current Android versionName: $($CurrentVersions.DisplayVersion)"
    Write-Step "Current Android versionCode: $($CurrentVersions.VersionCode)"

    $targetDisplayVersion = $CurrentVersions.DisplayVersion
    if (-not [string]::IsNullOrWhiteSpace($VersionName)) {
        $targetDisplayVersion = $VersionName.Trim()
    }

    if (-not $NoVersionPrompt) {
        $displayInput = Read-Host "New versionName / ApplicationDisplayVersion [$targetDisplayVersion]"
        if (-not [string]::IsNullOrWhiteSpace($displayInput)) {
            $targetDisplayVersion = $displayInput.Trim()
        }
    }

    if (-not (Test-VersionName -Value $targetDisplayVersion)) {
        throw "Invalid versionName '$targetDisplayVersion'. Use letters, numbers, dots, hyphens, underscores, or plus signs."
    }

    $targetVersionCode = $CurrentVersions.VersionCode
    if ($VersionCode -gt 0) {
        $targetVersionCode = $VersionCode
    }
    elseif ($targetDisplayVersion -ne $CurrentVersions.DisplayVersion) {
        $targetVersionCode = $CurrentVersions.VersionCode + 1
    }

    if (-not $NoVersionPrompt) {
        $codeInput = Read-Host "New versionCode / ApplicationVersion [$targetVersionCode]"
        if (-not [string]::IsNullOrWhiteSpace($codeInput)) {
            $parsedVersionCode = 0
            if (-not [int]::TryParse($codeInput.Trim(), [ref]$parsedVersionCode)) {
                throw "versionCode must be a positive integer."
            }

            $targetVersionCode = $parsedVersionCode
        }
    }

    if ($targetVersionCode -lt 1) {
        throw "versionCode must be a positive integer."
    }

    if ($targetVersionCode -lt $CurrentVersions.VersionCode) {
        throw "versionCode cannot be lower than the current ApplicationVersion."
    }

    if ($targetDisplayVersion -ne $CurrentVersions.DisplayVersion -or $targetVersionCode -ne $CurrentVersions.VersionCode) {
        Set-ProjectVersions -ProjectPath $projectPath -DisplayVersion $targetDisplayVersion -VersionCode $targetVersionCode
        Set-CoreAppVersion -ConstantsPath $coreConstantsPath -DisplayVersion $targetDisplayVersion
        Write-Ok "Android project version updated to $targetDisplayVersion (versionCode $targetVersionCode)."
    }
    else {
        Set-CoreAppVersion -ConstantsPath $coreConstantsPath -DisplayVersion $targetDisplayVersion
        Write-Ok "Android project version kept at $targetDisplayVersion (versionCode $targetVersionCode)."
    }

    return [pscustomobject]@{
        DisplayVersion = $targetDisplayVersion
        VersionCode = $targetVersionCode
    }
}

$repoRoot = Get-RepoRoot
$projectPath = Join-Path $repoRoot "src\QRKeeper.Android\QRKeeper.Android.csproj"
$coreConstantsPath = Join-Path $repoRoot "src\QRKeeper.Core\Common\AppConstants.cs"

if (-not (Test-Path $projectPath -PathType Leaf)) {
    throw "Android project not found: $projectPath"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\android"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$publishDirectory = Join-Path $repoRoot "src\QRKeeper.Android\bin\$Configuration\net8.0-android\$RuntimeIdentifier\publish"
$currentVersions = Get-ProjectVersions -ProjectPath $projectPath
$releaseVersions = Resolve-ReleaseVersions -CurrentVersions $currentVersions
$version = $releaseVersions.DisplayVersion
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$artifactExtension = if ($PackageFormat -eq "aab") { "aab" } else { "apk" }
$artifactName = "QRKeeper-Android-$version-$RuntimeIdentifier-$timestamp.$artifactExtension"
$artifactPath = Join-Path $OutputDirectory $artifactName

Write-Step "Repository: $repoRoot"
Write-Step "Project: $projectPath"
Write-Step "Configuration: $Configuration"
Write-Step "RuntimeIdentifier: $RuntimeIdentifier"
Write-Step "PackageFormat: $PackageFormat"
Write-Step "VersionName: $($releaseVersions.DisplayVersion)"
Write-Step "VersionCode: $($releaseVersions.VersionCode)"

$publishArgs = @(
    "publish",
    $projectPath,
    "-f", "net8.0-android",
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "/p:AndroidPackageFormat=$PackageFormat",
    "-v", "minimal"
)

$hasSigning = -not [string]::IsNullOrWhiteSpace($KeyStore)
if ($hasSigning) {
    if ([string]::IsNullOrWhiteSpace($StorePass) -or
        [string]::IsNullOrWhiteSpace($KeyAlias) -or
        [string]::IsNullOrWhiteSpace($KeyPass)) {
        throw "When -KeyStore is provided, -StorePass, -KeyAlias, and -KeyPass are required."
    }

    $publishArgs += "/p:AndroidKeyStore=true"
    $publishArgs += "/p:AndroidSigningKeyStore=$KeyStore"
    $publishArgs += "/p:AndroidSigningStorePass=$StorePass"
    $publishArgs += "/p:AndroidSigningKeyAlias=$KeyAlias"
    $publishArgs += "/p:AndroidSigningKeyPass=$KeyPass"
}

Write-Step "Running dotnet publish..."
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if ($PackageFormat -eq "apk") {
    $sourcePackage = Join-Path $publishDirectory "com.qrkeeper.app-Signed.apk"
    if (-not (Test-Path $sourcePackage -PathType Leaf)) {
        $sourcePackage = Join-Path $publishDirectory "com.qrkeeper.app.apk"
    }
} else {
    $sourcePackage = Get-ChildItem -Path $publishDirectory -Filter "*.aab" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 |
        ForEach-Object { $_.FullName }
}

if ([string]::IsNullOrWhiteSpace($sourcePackage) -or -not (Test-Path $sourcePackage -PathType Leaf)) {
    throw "Published $PackageFormat package was not found in $publishDirectory"
}

Copy-Item -LiteralPath $sourcePackage -Destination $artifactPath -Force
Write-Ok "Package copied to $artifactPath"

if ($PackageFormat -eq "apk") {
    Write-Step "Checking ARM64 native libraries..."
    $arm64Entries = tar -tf $artifactPath | Select-String "^lib/arm64-v8a/"
    if ($RuntimeIdentifier -eq "android-arm64" -and $arm64Entries.Count -eq 0) {
        throw "The APK does not contain lib/arm64-v8a entries. It may not be suitable for current ARM64 Android devices."
    }

    if ($arm64Entries.Count -gt 0) {
        Write-Ok "ARM64 native libraries found."
    }
}

Write-Ok "Android release build completed."
Write-Host $artifactPath

Pause-Script
