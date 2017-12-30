#Requires -Version 5.0

$script:InformationPreference = 'Continue'
$ModuleRoot = New-Item (Join-Path $PSScriptRoot Modules) -ItemType Directory -Force

Function InstallModule {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Name,
        [string]$Version,
        [string]$Repository = 'PSGallery',
        [string]$BasePath = $ModuleRoot
    )

    if(!$Version) {
        Write-Host "Looking for latest version of $Name"
        $found = Find-Module -Name $Name -Repository $Repository
        $Version = $found.Version.ToString()
        Write-Host "Found version $Version"
    }


    # Get the latest version (based on minimum version) and skip installation if it already exists
    $installPath = [IO.Path]::Combine($BasePath, $Name, $Version)
    $parent = [IO.Directory]::GetParent($installPath)
    if(-not(Test-Path $installPath)) {
        # Clear out parent dir in case of other versions
        if(Test-Path $parent) {
            Remove-Item $parent -Recurse -Force
        }

        New-Item $parent -ItemType Directory | Out-Null

        # Get via powershell gallery
        Write-Information "Downloading module: $Name - $Version"
        Save-Module $Name -Path $BasePath -RequiredVersion $Version -Force -Repository $Repository
    } else {
        Write-Host "Module $Name - ($Version) already installed"
    }

    Get-ChildItem -Path $installPath -File -Recurse | Unblock-File
    Join-Path $installPath "$Name.psd1"
}

# Make sure we have the nuget provider up to date
Install-PackageProvider NuGet -Force | Out-Null


@{
    Modules = @(
        (InstallModule Psake 4.7.0)
    )
}

