$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation-rules.ps1')
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/Orbis.Api/Orbis.Api.csproj'))
$checked = 0
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'An invalid validation artifact was accepted.' }
    $script:checked++
}
function New-Audit {
    return @{ version=1; parameters='--vulnerable --include-transitive'; sources=@('https://api.nuget.org/v3/index.json'); projects=@(@{path=$project}) }
}
Assert-NuGetAudit (New-Audit) @($project)
$checked++
Assert-Rejected { $audit=New-Audit; $audit.projects=@(); Assert-NuGetAudit $audit @($project) }
Assert-Rejected { $audit=New-Audit; $audit.projects+=@{path=$project}; Assert-NuGetAudit $audit @($project) }
Assert-Rejected { $audit=New-Audit; $audit.parameters='--vulnerable'; Assert-NuGetAudit $audit @($project) }
Assert-Rejected { $audit=New-Audit; $audit.version=2; Assert-NuGetAudit $audit @($project) }
Assert-Rejected { $audit=New-Audit; $audit.sources=@(); Assert-NuGetAudit $audit @($project) }
Assert-Rejected { $audit=New-Audit; $audit.logs=@(@{level='warning'; message='feed unavailable'}); Assert-NuGetAudit $audit @($project) }
Assert-Rejected { $audit=New-Audit; $audit.projects[0].logs=@(@{level='error'}); Assert-NuGetAudit $audit @($project) }
foreach ($kind in @('topLevelPackages', 'transitivePackages')) {
    Assert-Rejected {
        $audit=New-Audit
        $audit.projects[0].frameworks=@(@{ framework='net10.0'; $kind=@(@{id='Synthetic.Test'; vulnerabilities=@(@{severity='Low'; advisoryurl='https://example.invalid/test'})}) })
        Assert-NuGetAudit $audit @($project)
    }
    Assert-Rejected {
        $audit=New-Audit
        $audit.projects[0].frameworks=@(@{ framework='net10.0'; $kind=@(@{id='Synthetic.IncompleteFinding'}) })
        Assert-NuGetAudit $audit @($project)
    }
}
# Fixtures mínimos e sintéticos de estrutura, nunca relatórios apresentados como execução da aplicação.
$trxText = '<TestRun><Results><UnitTestResult outcome="Passed"/></Results><TestDefinitions><UnitTest><TestMethod codeBase="Example.Tests.dll"/></UnitTest></TestDefinitions><ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0"/></ResultSummary></TestRun>'
if ((Assert-TestResults @([xml]$trxText) @('Example.Tests.dll')) -ne 1) { throw 'Valid test evidence was rejected.' }
$checked++
Assert-Rejected { Assert-TestResults @([xml]$trxText) @('Other.Tests.dll') }
Assert-Rejected { Assert-TestResults @([xml]$trxText, [xml]$trxText) @('Example.Tests.dll', 'Other.Tests.dll') }
Assert-Rejected { Assert-TestResults @([xml]$trxText.Replace('passed="1"', 'passed="0"')) @('Example.Tests.dll') }
Assert-Rejected { Assert-TestResults @([xml]$trxText.Replace('notExecuted="0"', 'notExecuted="1"')) @('Example.Tests.dll') }
Assert-Rejected { Assert-TestResults @([xml]$trxText.Replace('outcome="Passed"', 'outcome="NotExecuted"')) @('Example.Tests.dll') }
Assert-Rejected { Assert-TestResults @([xml]$trxText.Replace('outcome="Completed"', 'outcome="Aborted"')) @('Example.Tests.dll') }
Assert-Rejected { Assert-TestResults @([xml]$trxText.Replace('total="1"', 'total="2"')) @('Example.Tests.dll') }
Assert-Rejected { Assert-TestResults @([xml]$trxText.Replace('failed="0"', '')) @('Example.Tests.dll') }
Write-Output "$checked validation-rule cases passed. These are not application/load tests."
