#Requires -Version 7.0
<#
.SYNOPSIS
    Downloads the Microsoft-signed Pix2d package straight from the Microsoft Store CDN.

.DESCRIPTION
    The Store re-signs every submission with a certificate that chains to a root Windows
    already trusts, so the package it serves installs by double-click on any machine —
    unlike the portable .zip, whose .exe is unsigned and gets flagged by SmartScreen.
    This script fetches that signed package so it can be attached to a GitHub release.

    No third-party service is involved (in particular NOT store.rg-adguard.net, which
    rate-limits and blocks datacenter IPs — i.e. GitHub Actions runners). It talks to the
    same two Microsoft endpoints the Store client itself uses:

      1. DisplayCatalog  https://displaycatalog.mp.microsoft.com/v7.0/products/<ProductId>
         Store product id -> WuCategoryId (the Windows Update category of the app).
      2. FE3 / Windows Update  https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx
         GetCookie -> SyncUpdates (filtered by that category; anonymous ticket is enough)
         -> GetExtendedUpdateInfo2 on the /secured endpoint for the actual CDN url.

    The download is then verified three ways before it is accepted: SHA256 against the
    digest Windows Update reported, Authenticode signature status, and package identity +
    version parsed out of the bundle manifest.

.PARAMETER ProductId
    Store product id. Pix2d = 9NBLGGH1ZDFV (same constant as DesktopReviewService).

.PARAMETER ExpectedIdentityName
    Package identity the download must carry. Guards against the catalog handing back a
    different app; keep in sync with build/msix/Package.appxmanifest.

.PARAMETER ExpectedVersion
    Optional X.Y.Z. When given, the script fails unless the app version inside the package
    matches — that is how CI decides whether the Store has certified this release yet.
    Note it is the version of the packages *inside* the bundle that is compared: winapp CLI
    stamps the bundle itself with a date-derived version (e.g. 2026.725.1408.0).

.PARAMETER OutDir
    Where to put the package. Created if missing.

.PARAMETER SkipSignatureCheck
    Skip the Authenticode check (it needs Windows). Digest + identity checks still run.

.OUTPUTS
    The downloaded file path on the pipeline. When $env:GITHUB_OUTPUT is set, also writes
    the `path`, `file-name`, `version` and `version-4` step outputs.

.EXAMPLE
    ./build/download-store-msix.ps1
    Download whatever version the Store currently serves into artifacts/store-msix/.

.EXAMPLE
    ./build/download-store-msix.ps1 -ExpectedVersion 3.11.2
    Fail unless the Store is serving exactly 3.11.2 (used by the release workflow).
#>
[CmdletBinding()]
param(
    [string] $ProductId = '9NBLGGH1ZDFV',
    [string] $ExpectedIdentityName = '58815Gritsenko.PixelArtStudio',
    [string] $ExpectedVersion = '',
    [string] $OutDir = 'artifacts/store-msix',
    [switch] $SkipSignatureCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Fe3Url        = 'https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx'
$Fe3SecuredUrl = "$Fe3Url/secured"
$WuNs          = 'http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService'

# Device attributes decide which packages Windows Update considers applicable. Claiming a
# plain x64 Windows 11 desktop client is what gets us the retail desktop bundle.
$DeviceAttributes = 'E:BranchReadinessLevel=CB&CurrentBranch=ge_release&OEMModel=&FlightRing=Retail' +
    '&AttrDataVer=228&InstallLanguage=en-US&OSUILocale=en-US&InstallationType=Client' +
    '&FlightingBranchName=&App=WU_STORE&ProcessorManufacturer=GenuineIntel&AppVer=10.0.26100.1' +
    '&OSArchitecture=AMD64&UpdateManagementGroup=2&IsDeviceRetailDemo=0&OSSkuId=48' +
    '&OSVersion=10.0.26100.4061&DeviceFamily=Windows.Desktop'

function New-SoapSecurityHeader {
    # Anonymous ticket: no MSA/AAD token needed for free apps, the ticket type alone is
    # what FE3 wants to see. Timestamps must be fresh or the envelope is rejected.
    $now = [DateTime]::UtcNow
    @"
    <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <Timestamp xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
        <Created>$($now.ToString('yyyy-MM-ddTHH:mm:ssZ'))</Created>
        <Expires>$($now.AddMinutes(10).ToString('yyyy-MM-ddTHH:mm:ssZ'))</Expires>
      </Timestamp>
      <wuws:WindowsUpdateTicketsToken wsu:id="ClientMSA" xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" xmlns:wuws="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization">
        <TicketType Name="MSA" Version="1.0" Policy="MBI_SSL" />
        <TicketType Name="AAD" Version="1.0" Policy="MBI_SSL" />
      </wuws:WindowsUpdateTicketsToken>
    </o:Security>
"@
}

function Invoke-Fe3 {
    param(
        [Parameter(Mandatory)] [string] $Action,
        [Parameter(Mandatory)] [string] $Body,
        [switch] $Secured
    )
    $url = if ($Secured) { $Fe3SecuredUrl } else { $Fe3Url }
    $envelope = @"
<s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
  <s:Header>
    <a:Action s:mustUnderstand="1">$WuNs/$Action</a:Action>
    <a:MessageID>urn:uuid:$([guid]::NewGuid())</a:MessageID>
    <a:To s:mustUnderstand="1">$url</a:To>
$(New-SoapSecurityHeader)
  </s:Header>
  <s:Body>
$Body
  </s:Body>
</s:Envelope>
"@
    # -MaximumRetryCount: FE3 occasionally answers 500 on a cold cache.
    $response = Invoke-WebRequest -Uri $url -Method Post -Body $envelope `
        -ContentType 'application/soap+xml; charset=utf-8' `
        -MaximumRetryCount 3 -RetryIntervalSec 5 -UseBasicParsing
    [xml] $response.Content
}

function Get-WuCategoryId {
    param([Parameter(Mandatory)] [string] $ProductId)

    $uri = "https://displaycatalog.mp.microsoft.com/v7.0/products/$ProductId" +
           '?market=US&languages=en-us&fieldsTemplate=Details'
    $product = (Invoke-RestMethod -Uri $uri -MaximumRetryCount 3 -RetryIntervalSec 5).Product

    $categoryId = $product.DisplaySkuAvailabilities.Sku.Properties.FulfillmentData.WuCategoryId |
        Where-Object { $_ } | Select-Object -First 1
    if (-not $categoryId) {
        throw "DisplayCatalog returned no WuCategoryId for product '$ProductId'."
    }

    $title = $product.LocalizedProperties.ProductTitle | Select-Object -First 1
    Write-Host "Store product : $ProductId ($title)"
    Write-Host "WuCategoryId  : $categoryId"
    $categoryId
}

function Get-StorePackageCandidates {
    <#
        Walks SyncUpdates for one app category and returns every downloadable package it
        advertises. The response is paged: `Truncated` means "call again, and tell me what
        you already know about" — hence the growing $seenIds list, without which the loop
        would hand back the same page forever.
    #>
    param([Parameter(Mandatory)] [string] $CategoryId)

    $cookieBody = @"
    <GetCookie xmlns="$WuNs">
      <oldCookie><Expiration>1601-01-01T00:00:00Z</Expiration></oldCookie>
      <lastChange>2015-10-21T17:01:07.1472913Z</lastChange>
      <currentTime>$([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ'))</currentTime>
      <protocolVersion>1.81</protocolVersion>
    </GetCookie>
"@
    $cookie = (Invoke-Fe3 -Action 'GetCookie' -Body $cookieBody).SelectSingleNode(
        '//*[local-name()="GetCookieResult"]/*[local-name()="EncryptedData"]')?.InnerText
    if (-not $cookie) { throw 'FE3 GetCookie returned no cookie.' }

    $candidates = @()
    $seenIds = [System.Collections.Generic.List[string]]::new()

    for ($page = 1; $page -le 20; $page++) {
        $cachedIds = ($seenIds | ForEach-Object { "<int>$_</int>" }) -join ''
        $syncBody = @"
    <SyncUpdates xmlns="$WuNs">
      <cookie>
        <Expiration>2050-01-01T00:00:00Z</Expiration>
        <EncryptedData>$cookie</EncryptedData>
      </cookie>
      <parameters>
        <ExpressQuery>false</ExpressQuery>
        <InstalledNonLeafUpdateIDs>
          <int>1</int><int>2</int><int>3</int><int>11</int><int>19</int>
          <int>2359974</int><int>5169044</int><int>8788830</int><int>23110993</int><int>23110994</int>
          <int>59830006</int><int>59830007</int><int>59830008</int><int>60484010</int><int>62450018</int>
          <int>62450019</int><int>62450020</int><int>98959022</int><int>98959023</int><int>98959024</int>
          <int>98959025</int><int>98959026</int><int>129905029</int><int>130040030</int><int>130040031</int>
          <int>130040032</int><int>130040033</int><int>138372035</int><int>138372036</int><int>139536037</int>
          <int>139536038</int><int>139536039</int><int>139536040</int><int>142045136</int><int>158941041</int>
          <int>158941042</int><int>158941043</int><int>158941044</int><int>159123045</int><int>159130046</int>
          <int>160733047</int><int>160733048</int><int>160733049</int><int>160733050</int><int>161870051</int>
          <int>163325052</int><int>164253053</int><int>165786054</int><int>165786055</int><int>165786056</int>
          <int>165786057</int>
        </InstalledNonLeafUpdateIDs>
        <OtherCachedUpdateIDs>$cachedIds</OtherCachedUpdateIDs>
        <SkipSoftwareSync>false</SkipSoftwareSync>
        <NeedTwoGroupOutOfScopeUpdates>true</NeedTwoGroupOutOfScopeUpdates>
        <FilterAppCategoryIds>
          <CategoryIdentifier><Id>$CategoryId</Id></CategoryIdentifier>
        </FilterAppCategoryIds>
        <TreatAppCategoryIdsAsInstalled>true</TreatAppCategoryIdsAsInstalled>
        <AlsoPerformRegularSync>false</AlsoPerformRegularSync>
        <ComputerSpec/>
        <ExtendedUpdateInfoParameters>
          <XmlUpdateFragmentTypes>
            <XmlUpdateFragmentType>Extended</XmlUpdateFragmentType>
            <XmlUpdateFragmentType>LocalizedProperties</XmlUpdateFragmentType>
          </XmlUpdateFragmentTypes>
          <Locales><string>en-US</string><string>en</string></Locales>
        </ExtendedUpdateInfoParameters>
        <ClientPreferredLanguages><string>en-US</string></ClientPreferredLanguages>
        <ProductsParameters>
          <SyncCurrentVersionOnly>false</SyncCurrentVersionOnly>
          <DeviceAttributes>$([System.Security.SecurityElement]::Escape($DeviceAttributes))</DeviceAttributes>
          <CallerAttributes>E:Interactive=1&amp;IsSeeker=0&amp;</CallerAttributes>
          <Products/>
        </ProductsParameters>
      </parameters>
    </SyncUpdates>
"@
        # XPath throughout: the response is sparse (whole sections are simply absent), and
        # dotted property access on a missing node throws under Set-StrictMode.
        $result = (Invoke-Fe3 -Action 'SyncUpdates' -Body $syncBody).
            SelectSingleNode('/*[local-name()="Envelope"]/*[local-name()="Body"]/*[local-name()="SyncUpdatesResponse"]/*[local-name()="SyncUpdatesResult"]')
        if (-not $result) { throw 'FE3 SyncUpdates returned no result (auth or envelope rejected?).' }

        $newUpdates = @($result.SelectNodes('*[local-name()="NewUpdates"]/*[local-name()="UpdateInfo"]'))
        if ($newUpdates.Count -eq 0) { break }

        # UpdateInfo carries the update identity; the parallel ExtendedUpdateInfo section
        # (keyed by the same numeric id) carries the file list. Join them on that id.
        $extendedById = @{}
        foreach ($extended in @($result.SelectNodes('*[local-name()="ExtendedUpdateInfo"]/*[local-name()="Updates"]/*[local-name()="Update"]'))) {
            $extendedId = $extended.SelectSingleNode('*[local-name()="ID"]').InnerText
            $extendedById[$extendedId] = ($extendedById[$extendedId] ?? '') +
                $extended.SelectSingleNode('*[local-name()="Xml"]').InnerText
        }

        foreach ($update in $newUpdates) {
            $updateId = $update.SelectSingleNode('*[local-name()="ID"]').InnerText
            $seenIds.Add($updateId)

            $fragment = ([xml] "<root>$($update.SelectSingleNode('*[local-name()="Xml"]').InnerText)</root>").DocumentElement
            if ($fragment.SelectSingleNode('Properties/@UpdateType').Value -ne 'Software') { continue }
            if (-not $extendedById.ContainsKey($updateId)) { continue }

            $extendedXml = ([xml] "<root>$($extendedById[$updateId])</root>").DocumentElement
            # The bundle/package to install (as opposed to its block map .cab sidecar).
            $mainFileName = $extendedXml.SelectSingleNode(
                'HandlerSpecificData/AppxPackageInstallData[@MainPackage="true"]/@PackageFileName')?.Value
            if (-not $mainFileName) { continue }

            $file = $extendedXml.SelectSingleNode("Files/File[@FileName='$mainFileName']")
            if (-not $file) { continue }

            # InstallerSpecificIdentifier is the package full name:
            #   <identity>_<version>_<arch>_<resource>_<publisherId>
            $fullName = $file.SelectSingleNode('@InstallerSpecificIdentifier')?.Value
            if (-not $fullName) { continue }
            $parts = $fullName.Split('_')
            if ($parts.Count -lt 3) { continue }

            $candidates += [pscustomobject]@{
                IdentityName  = $parts[0]
                Version       = [version] $parts[1]
                Architecture  = $parts[2]
                FullName      = $fullName
                UpdateId      = $fragment.SelectSingleNode('UpdateIdentity/@UpdateID').Value
                Revision      = [int] $fragment.SelectSingleNode('UpdateIdentity/@RevisionNumber').Value
                # Server-side name is a bare GUID; only its extension is meaningful.
                Extension     = [System.IO.Path]::GetExtension($mainFileName)
                Size          = [long] $file.SelectSingleNode('@Size').Value
                Sha256Base64  = $file.SelectSingleNode('AdditionalDigest[@Algorithm="SHA256"]')?.InnerText
                Encrypted     = $mainFileName -match '\.e(appx|msix)'
            }
        }

        if ($result.SelectSingleNode('*[local-name()="Truncated"]')?.InnerText -ne 'true') { break }
    }

    $candidates
}

function Get-Fe3DownloadUrl {
    param(
        [Parameter(Mandatory)] [string] $UpdateId,
        [Parameter(Mandatory)] [int]    $Revision
    )

    $body = @"
    <GetExtendedUpdateInfo2 xmlns="$WuNs">
      <updateIDs>
        <UpdateIdentity>
          <UpdateID>$UpdateId</UpdateID>
          <RevisionNumber>$Revision</RevisionNumber>
        </UpdateIdentity>
      </updateIDs>
      <infoTypes>
        <XmlUpdateFragmentType>FileUrl</XmlUpdateFragmentType>
        <XmlUpdateFragmentType>FileDecryption</XmlUpdateFragmentType>
      </infoTypes>
      <deviceAttributes>$([System.Security.SecurityElement]::Escape($DeviceAttributes))</deviceAttributes>
    </GetExtendedUpdateInfo2>
"@
    $urls = @((Invoke-Fe3 -Action 'GetExtendedUpdateInfo2' -Body $body -Secured).SelectNodes(
        '//*[local-name()="FileLocations"]/*[local-name()="FileLocation"]/*[local-name()="Url"]') |
        ForEach-Object { $_.InnerText })
    if ($urls.Count -eq 0) {
        throw "Windows Update returned no download location for update $UpdateId (revision $Revision)."
    }
    # tlu.dl.delivery.mp.microsoft.com is the plain CDN; emirror hosts encrypted blobs.
    ($urls | Where-Object { $_ -notmatch 'emirror' } | Select-Object -First 1) ?? $urls[0]
}

function Get-PackageManifestInfo {
    <#
        Reads the manifest out of the package (an MSIX is a zip) and returns its identity
        plus, for a bundle, the versions of the application packages inside it.

        The distinction matters: winapp CLI stamps the *bundle* with a date-derived version
        (2026.725.1408.0) while the packages inside carry the real Pix2dVersion (3.11.2.0).
        So the app version — the one that maps to a GitHub release tag — is the inner one.

        Uses System.IO.Compression rather than the Appx cmdlets so it also runs off-Windows.
    #>
    param([Parameter(Mandatory)] [string] $Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $Path))
    try {
        $entry = $zip.Entries |
            Where-Object { $_.FullName -in @('AppxMetadata/AppxBundleManifest.xml', 'AppxBundleManifest.xml', 'AppxManifest.xml') } |
            Select-Object -First 1
        if (-not $entry) { throw "No Appx(Bundle)Manifest.xml inside '$Path' — not an MSIX/APPX package?" }

        $reader = [System.IO.StreamReader]::new($entry.Open())
        try { $manifest = ([xml] $reader.ReadToEnd()).DocumentElement } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }

    $identity = $manifest.SelectSingleNode('*[local-name()="Identity"]')

    # Resource packages (language/scale assets) share the bundle version, so only the
    # application slices are asked for the app version.
    $appPackages = @($manifest.SelectNodes('*[local-name()="Packages"]/*[local-name()="Package"][@Type="application"]') |
        ForEach-Object {
            [pscustomobject]@{
                Architecture = $_.GetAttribute('Architecture')
                Version      = [version] $_.GetAttribute('Version')
            }
        })

    [pscustomobject]@{
        IdentityName = $identity.GetAttribute('Name')
        Publisher    = $identity.GetAttribute('Publisher')
        Version      = [version] $identity.GetAttribute('Version')
        AppPackages  = $appPackages
        # A plain (non-bundle) package is its own application slice.
        AppVersion   = if ($appPackages.Count -gt 0) {
                           ($appPackages.Version | Sort-Object -Descending | Select-Object -First 1)
                       } else { [version] $identity.GetAttribute('Version') }
    }
}

function Assert-SignedByStore {
    param([Parameter(Mandatory)] [string] $Path)

    if ($SkipSignatureCheck) {
        Write-Warning 'Authenticode check skipped (-SkipSignatureCheck).'
        return
    }
    if (-not $IsWindows) {
        Write-Warning 'Authenticode check skipped: Get-AuthenticodeSignature needs Windows.'
        return
    }

    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ($signature.Status -ne 'Valid') {
        throw "Package is not validly signed (status: $($signature.Status)). " +
              'A Store download must be signed — refusing to publish it.'
    }
    Write-Host "Signature     : $($signature.Status) — $($signature.SignerCertificate.Subject)"
}

# ---------------------------------------------------------------------------------------

$categoryId = Get-WuCategoryId -ProductId $ProductId
$candidates = @(Get-StorePackageCandidates -CategoryId $categoryId)
Write-Host "Packages seen : $($candidates.Count)"

$ours = @($candidates | Where-Object { $_.IdentityName -eq $ExpectedIdentityName -and -not $_.Encrypted })
if ($ours.Count -eq 0) {
    throw "Windows Update advertised no unencrypted package with identity '$ExpectedIdentityName' " +
          "in category $categoryId. Candidates: " + (($candidates | ForEach-Object { $_.FullName }) -join ', ')
}

# Bundles cover every architecture in one file, so prefer them over a single-arch package.
# Windows Update only exposes the *bundle* version here, which is date-derived and therefore
# monotonic — good enough to pick "newest", but the app version comes from the manifest below.
$package = $ours |
    Sort-Object @{ Expression = { $_.Extension -like '*bundle' }; Descending = $true },
                @{ Expression = { $_.Version }; Descending = $true },
                @{ Expression = { $_.Revision }; Descending = $true } |
    Select-Object -First 1

Write-Host "Selected      : $($package.FullName) ($([math]::Round($package.Size / 1MB, 1)) MB)"

$null = New-Item -ItemType Directory -Path $OutDir -Force
# Downloaded under a temporary name: the final name needs the app version, which is only
# known once the manifest inside the package has been read.
$tempFile = Join-Path (Resolve-Path $OutDir) "store-download$($package.Extension)"

$url = Get-Fe3DownloadUrl -UpdateId $package.UpdateId -Revision $package.Revision
Write-Host "Downloading   : $($url -replace '\?.*$', '?…')"
Invoke-WebRequest -Uri $url -OutFile $tempFile -MaximumRetryCount 3 -RetryIntervalSec 5

$actualSize = (Get-Item $tempFile).Length
if ($actualSize -ne $package.Size) {
    throw "Downloaded $actualSize bytes, Windows Update promised $($package.Size)."
}
if ($package.Sha256Base64) {
    $actualSha256 = [Convert]::ToBase64String(
        [System.Security.Cryptography.SHA256]::HashData([System.IO.File]::ReadAllBytes($tempFile)))
    if ($actualSha256 -ne $package.Sha256Base64) {
        Remove-Item $tempFile -Force
        throw "SHA256 mismatch: got $actualSha256, expected $($package.Sha256Base64). Download discarded."
    }
    Write-Host "SHA256        : verified"
}

$info = Get-PackageManifestInfo -Path $tempFile
if ($info.IdentityName -ne $ExpectedIdentityName) {
    throw "Package identity mismatch: manifest says '$($info.IdentityName)', expected '$ExpectedIdentityName'."
}
Write-Host "Bundle        : $($info.IdentityName) $($info.Version) ($($info.Publisher))"
Write-Host ("App packages  : " + (($info.AppPackages | ForEach-Object { "$($_.Architecture) $($_.Version)" }) -join ', '))

Assert-SignedByStore -Path $tempFile

$appVersion = $info.AppVersion
$version3 = '{0}.{1}.{2}' -f $appVersion.Major, $appVersion.Minor, $appVersion.Build

if ($ExpectedVersion) {
    $wanted = [version] $ExpectedVersion
    $wanted3 = '{0}.{1}.{2}' -f $wanted.Major, $wanted.Minor, $wanted.Build
    if ($version3 -ne $wanted3) {
        Remove-Item $tempFile -Force
        throw "The Store is serving $version3, not $wanted3. Certification lags the tag by " +
              'hours to days — retry once the submission is live.'
    }
}

# Named after the release-asset convention used by release-publish.yml
# (Pix2d_win-x64-3.11.2-portable.zip); the bundle itself covers every architecture.
$fileName = "Pix2d_win-$version3-store-signed$($package.Extension)"
$outFile = Join-Path (Resolve-Path $OutDir) $fileName
Move-Item -Path $tempFile -Destination $outFile -Force
Write-Host "Saved         : $outFile"

if ($env:GITHUB_OUTPUT) {
    @(
        "path=$outFile"
        "file-name=$fileName"
        "version=$version3"
        "app-version-4=$appVersion"
        "bundle-version=$($info.Version)"
    ) | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

$outFile
