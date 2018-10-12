Set-StrictMode -Version Latest

Properties {
    $rootPath = $null
    $buildPath = $null
    $srcPath = $null
    $outPath = $null
    $SitecoreVersion = $null
	$BuildVersion = $null
    $Version = $null
    $NugetApiKey = $null
    $NugetSource = "https://api.nuget.org/v3/index.json"
}

Task Clean -requiredVariables srcPath, outPath -description 'Clean the build' {
    dotnet clean $srcPath    
}

Task Version -requiredVariables BuildVersion, Version,SitecoreVersion {
	$script:Version = $Version
    $script:buildVersion = $BuildVersion
    $script:SitecoreVersion = $SitecoreVersion
}

Task Build -depends Clean,Version -requiredVariables srcPath {
    Assert ($script:buildVersion) "Buildversion has not been set"
    Assert ($script:SitecoreVersion) "SitecoreVersion has not been set"
    dotnet build $srcPath --configuration Release /property:Version=$script:buildVersion /property:SitecoreVersion=$script:SitecoreVersion
}

Task Pack -depends Build -requiredVariables srcPath, outPath {
    Assert ($script:buildVersion) "Buildversion has not been set"
    Assert ($script:SitecoreVersion) "SitecoreVersion has not been set"
    if(!(Test-Path -Path $outPath))
    {
        New-Item -Path $outPath -type directory -Force
    }
    dotnet pack $srcPath --configuration Release --no-restore --no-build --output $outPath /property:Version=$script:Version /property:SitecoreVersion=$script:SitecoreVersion /property:VersionPrefix=$script:Version /property:VersionSuffix=""
}

Task Publish -depends Pack -requiredVariables outPath,NugetApiKey, NugetSource {
    #Nothing yet
    Assert ($NugetApiKey) "Nuget API key not set"
    Assert ($NugetSource) "Nuget source not set"
    if(Test-Path -Path $outPath)
    {
        Get-ChildItem -path $outPath -Recurse -Include "*.$Version.nupkg" | ForEach-Object { 
            dotnet nuget push $_.FullName --source "$NugetSource" --api-key "$NugetApiKey" --disable-buffering --no-symbols --force-english-output
        
        }
    }
}

Task default -depends Publish
