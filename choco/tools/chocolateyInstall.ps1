$ErrorActionPreference = 'Stop'
$packageName = 'acai'
$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url = "https://github.com/acai-lang/acai-language/releases/download/v0.1.0/acai-windows-x64.exe"

$packageArgs = @{
    packageName    = $packageName
    fileType       = 'exe'
    url            = $url
    silentArgs     = '/VERYSILENT /SUPPRESSNSGBOXES /NORESTART'
    validExitCodes = @(0)
}

Install-ChocolateyPackage @packageArgs