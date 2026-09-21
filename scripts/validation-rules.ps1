# Funções puras: saída zero do scanner não substitui a validação da evidência produzida.
function Assert-NuGetAudit {
    param([Parameter(Mandatory)][hashtable]$Report, [Parameter(Mandatory)][string[]]$ExpectedProjects)
    if ($Report.version -ne 1 -or $Report.parameters -ne '--vulnerable --include-transitive' -or
        @($Report.sources).Count -ne 1 -or $Report.sources[0] -ne 'https://api.nuget.org/v3/index.json') {
        throw 'Unexpected NuGet audit schema, scope or source.'
    }
    $actual = @($Report.projects | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace($_.path)) { throw 'Missing project identity in NuGet audit.' }
        [IO.Path]::GetFullPath($_.path).Replace('\', '/')
    })
    $expected = @($ExpectedProjects | ForEach-Object { [IO.Path]::GetFullPath($_).Replace('\', '/') })
    if ($actual.Count -ne $expected.Count -or @($actual | Sort-Object -Unique).Count -ne $actual.Count -or
        @(Compare-Object $expected $actual -CaseSensitive:(!$IsWindows)).Count -ne 0) {
        throw 'NuGet audit did not cover every solution project exactly once.'
    }
    Assert-AuditTree $Report
}

function Assert-AuditTree {
    param($Node)
    if ($Node -is [Collections.IDictionary]) {
        foreach ($key in $Node.Keys) {
            if ($key -in @('logs', 'vulnerabilities', 'topLevelPackages', 'transitivePackages') -and @($Node[$key]).Count -gt 0) {
                # Não reproduzir mensagens arbitrárias do scanner: o relatório local preserva o diagnóstico.
                # Com --vulnerable, qualquer pacote retornado bloqueia, inclusive se seus detalhes vierem incompletos.
                throw 'NuGet reported diagnostics or vulnerable dependencies; inspect the local audit artifact.'
            }
            Assert-AuditTree $Node[$key]
        }
    } elseif ($Node -is [Collections.IEnumerable] -and $Node -isnot [string]) {
        foreach ($item in $Node) { Assert-AuditTree $item }
    }
}

function Assert-TestResults {
    param([Parameter(Mandatory)][xml[]]$Reports, [Parameter(Mandatory)][string[]]$ExpectedAssemblies)
    $seen = [Collections.Generic.List[string]]::new()
    $passed = 0
    foreach ($report in $Reports) {
        $summary = $report.TestRun.ResultSummary
        $counters = $summary.Counters
        $results = @($report.TestRun.Results.UnitTestResult)
        foreach ($required in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
            if (-not $counters.HasAttribute($required)) { throw 'Missing required test counter.' }
        }
        if ($summary.outcome -ne 'Completed' -or [int]$counters.total -le 0 -or
            [int]$counters.executed -ne [int]$counters.total -or [int]$counters.passed -ne [int]$counters.total -or
            $results.Count -ne [int]$counters.total -or @($results | Where-Object outcome -ne 'Passed').Count -ne 0) {
            throw 'Tests are missing, failed, skipped or incomplete.'
        }
        foreach ($counter in $counters.Attributes) {
            if ($counter.Name -notin @('total', 'executed', 'passed', 'completed') -and [int]$counter.Value -ne 0) {
                throw 'Test run contains a non-success counter.'
            }
        }
        $assemblies = @($report.TestRun.TestDefinitions.UnitTest.TestMethod.codeBase |
            ForEach-Object { [IO.Path]::GetFileName($_) } | Sort-Object -Unique)
        if ($assemblies.Count -ne 1) { throw 'Each TRX must identify exactly one test assembly.' }
        $seen.Add($assemblies[0])
        $passed += [int]$counters.passed
    }
    if ($seen.Count -ne $ExpectedAssemblies.Count -or @($seen | Sort-Object -Unique).Count -ne $seen.Count -or
        @(Compare-Object $ExpectedAssemblies $seen).Count -ne 0) {
        throw 'Test evidence does not cover every test project exactly once.'
    }
    return $passed
}
