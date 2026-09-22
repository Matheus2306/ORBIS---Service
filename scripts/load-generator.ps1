function Assert-LoadGeneratorInputs([object[]]$Inputs, [string]$PinsRoot) {
    $expected = @('toolchain.json', 'go.mod', 'go.sum')
    if ($Inputs.Count -ne $expected.Count) { throw 'Generator build input manifest is incomplete.' }
    foreach ($name in $expected) {
        $entry = @($Inputs | Where-Object { $_.file -ceq $name })
        if ($entry.Count -ne 1 -or $entry[0].sha256 -cne (Get-FileHash -LiteralPath (Join-Path $PinsRoot $name) -Algorithm SHA256).Hash) {
            throw 'Generator build inputs changed; rebuild the pinned tool before validation.'
        }
    }
}

function Get-VerifiedLoadGenerator {
    $projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $pinsRoot = Join-Path $projectRoot 'performance-tests/vegeta'
    $pins = Get-Content -LiteralPath (Join-Path $pinsRoot 'toolchain.json') -Raw | ConvertFrom-Json
    $toolsRoot = Join-Path $projectRoot '.artifacts/load-generator'
    $manifest = Get-Content -LiteralPath (Join-Path $toolsRoot 'current.json') -Raw -ErrorAction Stop | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.status -cne 'passed' -or $manifest.version -cne $pins.version) { throw 'Run scripts/build-load-generator.ps1 before validation.' }
    Assert-LoadGeneratorInputs $manifest.inputs $pinsRoot
    foreach ($name in @('binary', 'scanner')) {
        $path = [IO.Path]::GetFullPath((Join-Path $projectRoot $manifest.$name))
        if (-not $path.StartsWith($toolsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Generator artifact escaped its local directory.' }
        $expected = if ($name -eq 'binary') { $pins.binarySha256 } else { $pins.scannerSha256 }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $expected) { throw 'Generator artifact checksum mismatch.' }
        $manifest.$name = $path
    }
    return $manifest
}
