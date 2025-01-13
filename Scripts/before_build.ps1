function Replace-Version($path, $version){
    $filePath = "$path\SolutionAssemblyInfo.cs"
    $content = Get-Content -Path $filePath
    $newContent = $content -replace "0.0.0.0", "$version"
    Set-Content -Path $filePath -Value $newContent
    $content = Get-Content -Path $filePath
    Write-Output "#################################"
    Write-Output $content -ForegroundColor Green
    Write-Output "#################################"
}

#$p = ".\"
#$v = $env:flowVersion
#Replace-Version $p $v

nuget restore
