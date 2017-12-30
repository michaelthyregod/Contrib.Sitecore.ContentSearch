Set-StrictMode -Version Latest

Properties {
    $rootPath = $null
    $buildPath = $null
    $srcPath = $null
    $outPath = $null
    $SitecoreVersion = $null
    $Version = $null
}

Task Clean -requiredVariables srcPath, outPath -description 'Clean the build' {
    dotnet clean $srcPath    
}

Task Version -requiredVariables Version,SitecoreVersion {
    $script:buildVersion = $Version
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
    dotnet pack $srcPath --configuration Release --no-restore --no-build --output $outPath /property:Version=$script:SitecoreVersion /property:SitecoreVersion=$script:SitecoreVersion /property:VersionPrefix=$script:SitecoreVersion /property:VersionSuffix=""
}

Task Publish -depends Pack {
    #Nothing yet
}

Task default -depends Pack
