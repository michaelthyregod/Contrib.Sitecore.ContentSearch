#Requires -Version 5.0

param(
    [string[]]$Tasks = @('default'),
    [string[]]$NugetApiKey = '',
	[string]$patchVersion='1'
)

$init = Join-Path $PSScriptRoot init.ps1
$initInfo = & $init

# Prepare args for initial state
$originalModulePath = $env:PSModulePath
$infoPref = $global:InformationPreference
try {
    $env:PSModulePath += ";$PSScriptRoot\Modules"
    $global:InformationPreference = "Continue"
    $initInfo.Modules | ForEach-Object {
        Write-Information "Importing $_"
        Import-Module $_ -Force
    }

    # Invoke the psake build

    $sitecoreVersions = @("8.2.160729","8.2.161115","8.2.161221","8.2.170407","8.2.170614","8.2.170728","8.2.171121", "8.2.180406","9.0.171002","9.0.171219","9.0.180604")

    $rootPath = Join-Path $PSScriptRoot ..
    $buildPath = Join-Path $rootPath build
    $srcPath = Join-Path $rootPath src
    $outPath = Join-Path $rootPath output

    if(Test-Path -Path $outPath)
    {
        Remove-Item -Path $outPath -Recurse -Force
    }

    foreach($sitecoreVersion in $sitecoreVersions)
    {
        $props = @{
            rootPAth = $buildPath
            buildPath = $buildPath
            srcPath = $srcPath
            outPath = $outPath
            Version = "$sitecoreVersion.$patchVersion"
			BuildVersion = "1.0.0.0"
            SitecoreVersion = $sitecoreVersion
            NugetApiKey = $NugetApiKey
        }
        
        Invoke-Psake (Join-Path $PSScriptRoot build.psake.ps1) -tasklist $tasks -Properties $props -nologo
    }
    exit !$psake.build_success
} catch {
    throw $_
}
finally {
    $env:PSModulePath = $originalModulePath
    $global:InformationPreference = $infoPref
}
