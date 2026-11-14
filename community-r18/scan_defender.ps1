$ErrorActionPreference='Stop'
$package=Join-Path $env:GITHUB_WORKSPACE 'package'
$report=Join-Path $package 'Defender-Scan.txt'
('Microsoft Defender scan UTC: '+[DateTime]::UtcNow.ToString('o')) | Set-Content -LiteralPath $report -Encoding UTF8
$service=Get-Service WinDefend
if($service.Status-ne 'Running'){Start-Service WinDefend; $service.WaitForStatus('Running',[TimeSpan]::FromSeconds(20))}
# Use the native platform executable, not the PowerShell/CIM update provider.
$platform=Join-Path $env:ProgramData 'Microsoft/Windows Defender/Platform'
$scanner=@(Get-ChildItem -LiteralPath $platform -Directory | Where-Object {$version=$null;[Version]::TryParse(($_.Name -split '-')[0],[ref]$version)} | Sort-Object {[Version](($_.Name -split '-')[0])} -Descending | ForEach-Object {Join-Path $_.FullName 'MpCmdRun.exe'} | Where-Object {Test-Path -LiteralPath $_})[0]
if(-not $scanner){throw 'Native Defender executable unavailable'}
('Scanner: '+$scanner) | Add-Content -LiteralPath $report
function Invoke-PackageDefender([string[]]$arguments,[string]$label){
 $stdout=Join-Path $env:RUNNER_TEMP ($label+'-stdout.txt');$stderr=Join-Path $env:RUNNER_TEMP ($label+'-stderr.txt')
 $p=Start-Process -FilePath $scanner -ArgumentList $arguments -PassThru -NoNewWindow -RedirectStandardOutput $stdout -RedirectStandardError $stderr
 $null=$p.Handle
 if(-not $p.WaitForExit(180000)){Stop-Process -Id $p.Id -Force;throw ($label+' timed out')}
 foreach($path in @($stdout,$stderr)){if(Test-Path $path){Get-Content -LiteralPath $path | ForEach-Object {Write-Host $_;Add-Content -LiteralPath $report -Value $_ -Encoding UTF8}}}
 ('Exit code '+$label+': '+$p.ExitCode) | Add-Content -LiteralPath $report
 if($p.ExitCode-ne 0){throw ($label+' failed with exit code '+$p.ExitCode)}
}
Invoke-PackageDefender @('-SignatureUpdate','-MMPC') 'definition-update'
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Defender\Signature Updates' | Select-Object AVSignatureVersion,EngineVersion,SignaturesLastUpdated | Format-List | Out-String | Add-Content -LiteralPath $report
('DLL SHA256: '+(Get-FileHash (Join-Path $package 'version.dll') -Algorithm SHA256).Hash) | Add-Content -LiteralPath $report
Invoke-PackageDefender @('-Scan','-ScanType','3','-File',('"'+$package+'"'),'-DisableRemediation') 'package-scan'
Write-Host 'PASS: native Defender definition update and complete package scan'

