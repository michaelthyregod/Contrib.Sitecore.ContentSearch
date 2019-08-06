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
	$slnFile = "$srcPath"
	$slnPath = [System.IO.Path]::GetDirectoryName($slnFile)
	$objFolders = $null
	$objFolders = Get-ChildItem -Path "$slnPath" -Include "*project.assets.json" -File -Recurse

	foreach($objFolder in $objFolders)
	{
		if(($objFolder -ne $null) -and ($objFolder.FullName -ne $null))
		{
			$objFolderDirectory = $objFolder.FullName
			if(Test-Path -Path $objFolderDirectory)
			{
				Write-Host "polle" -ForegroundColor Green
				Remove-Item -Path $objFolderDirectory -Recurse -Force
			}
		}
	}
    dotnet clean "$srcPath" --configuration "Release" --verbosity q
	dotnet clean "$srcPath" --configuration "Debug" --verbosity q
}

Task Version -requiredVariables BuildVersion, Version,SitecoreVersion {
	$script:Version = $Version
    $script:buildVersion = $BuildVersion
    $script:SitecoreVersion = $SitecoreVersion
}

Task Build -depends Clean,Version -requiredVariables srcPath {
    Assert ($script:buildVersion) "Buildversion has not been set"
    Assert ($script:SitecoreVersion) "SitecoreVersion has not been set"
    dotnet restore "$srcPath" --force
	dotnet build "$srcPath" --configuration "Release" /property:Version=$script:buildVersion /property:SitecoreVersion=$script:SitecoreVersion
}

Task Pack -depends Build -requiredVariables srcPath, outPath {
    Assert ($script:buildVersion) "Buildversion has not been set"
    Assert ($script:SitecoreVersion) "SitecoreVersion has not been set"
    if(!(Test-Path -Path $outPath))
    {
        New-Item -Path $outPath -type directory -Force
    }
    dotnet pack "$srcPath" --configuration "Release" --no-restore --no-build --output $outPath /property:Version=$script:Version /property:SitecoreVersion=$script:SitecoreVersion /property:VersionPrefix=$script:Version /property:VersionSuffix=""
}

Task Publish -depends Pack -requiredVariables outPath {
    #Nothing yet
    if(([string]::IsNullOrEmpty($NugetApiKey) -eq $false) -and ([string]::IsNullOrEmpty($NugetSource) -eq $false))
	{
		if(Test-Path -Path $outPath)
		{
			Get-ChildItem -path $outPath -Recurse -Include "*.$Version.nupkg" | ForEach-Object { 
				dotnet nuget push $_.FullName --source "$NugetSource" --api-key "$NugetApiKey" --disable-buffering --no-symbols --force-english-output
			
			}
		}
	}
}

Task default -depends Publish
