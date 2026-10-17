# Tools/unity.ps1 - FishGame wrapper for Unity batchmode runs (M1 interface contract section 11).
# ASCII only. Windows PowerShell 5.1 compatible.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 <command> [-OutDir <abs dir>] [-Filter <regex>] [-TimeoutMinutes <int>]
#   <command> = compile | build | test-edit | test-play | check
#
# OutDir defaults to $env:FG_OUT. It must be absolute, must not contain '..', and must be outside the repo.
# Exit codes: 0 PASS, 1 FAIL, 2 LOCKED, 3 TIMEOUT, 4 usage error.
# The command is checked by hand instead of with ValidateSet, because a ValidateSet failure exits 1, not 4.
param(
    [Parameter(Position = 0)]
    [string]$Command = '',

    [string]$OutDir = $env:FG_OUT,

    [string]$Filter = '',

    [int]$TimeoutMinutes = 5
)

$ProjectRoot = 'C:\Dev\FishGame'
$UnityExe    = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe'
$LockFile    = Join-Path $ProjectRoot 'Temp\UnityLockfile'
$ErrPattern  = 'error CS|Exception|Scripts have compiler errors|Compilation failed'
$Commands    = @('compile', 'build', 'test-edit', 'test-play', 'check')
$UsageLine   = 'usage: unity.ps1 <compile|build|test-edit|test-play|check> -OutDir <absolute scratch dir outside the repo> (or set FG_OUT)'

function Say([string]$Text) {
    [Console]::Out.WriteLine($Text)
}

function Exit-Usage([string]$Detail) {
    Say $UsageLine
    if (-not [string]::IsNullOrEmpty($Detail)) { Say ('usage: ' + $Detail) }
    exit 4
}

function Get-LogText([string]$Log) {
    if (-not (Test-Path -LiteralPath $Log)) { return '' }
    $raw = Get-Content -LiteralPath $Log -Raw -Encoding UTF8
    if ($null -eq $raw) { return '' }
    return [string]$raw
}

function Test-UnityLocked {
    if (-not (Test-Path -LiteralPath $LockFile)) { return $false }
    try {
        $stream = [IO.File]::Open($LockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()
        return $false
    }
    catch {
        return $true
    }
}

function Show-LogExcerpt([string]$Log, [string]$Pattern) {
    if (-not (Test-Path -LiteralPath $Log)) {
        Say ('FG LOG no log file at ' + $Log)
        return
    }
    $shown = 0
    foreach ($line in (Get-Content -LiteralPath $Log -Encoding UTF8)) {
        if ($line -match $Pattern) {
            Say ('FG LOG ' + $line)
            $shown++
            if ($shown -ge 40) { break }
        }
    }
}

function Test-CompileClean([string]$Log) {
    $text = Get-LogText $Log
    return (($text -match 'Exiting batchmode successfully') -and ($text -notmatch 'error CS'))
}

function Invoke-UnityRun([string]$Name, [string]$Extra) {
    $log = Join-Path $OutDir ($Name + '.log')
    if (Test-UnityLocked) {
        return [pscustomobject]@{ Status = 'LOCKED'; ProcId = 0; ExitCode = -1; Log = $log }
    }
    if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log -Force }
    $unityArgs = '-batchmode -nographics -projectPath "' + $ProjectRoot + '" -logFile "' + $log + '" ' + $Extra
    try {
        $proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -NoNewWindow -ErrorAction Stop
    }
    catch {
        Say ('FG LOG could not start Unity: ' + $_.Exception.Message)
        return [pscustomobject]@{ Status = 'DONE'; ProcId = 0; ExitCode = -1; Log = $log }
    }
    $null = $proc.Handle
    $finished = $proc.WaitForExit($TimeoutMinutes * 60 * 1000)
    if (-not $finished) {
        return [pscustomobject]@{ Status = 'TIMEOUT'; ProcId = $proc.Id; ExitCode = -1; Log = $log }
    }
    return [pscustomobject]@{ Status = 'DONE'; ProcId = $proc.Id; ExitCode = $proc.ExitCode; Log = $log }
}

function Invoke-Compile {
    $run = Invoke-UnityRun 'compile' '-quit'
    switch ($run.Status) {
        'LOCKED'  { Say 'FG RESULT compile LOCKED (close the Unity editor, then rerun)'; return 2 }
        'TIMEOUT' { Say ('FG RESULT compile TIMEOUT pid=' + $run.ProcId + ' log=' + $run.Log); return 3 }
    }
    if (($run.ExitCode -eq 0) -and (Test-CompileClean $run.Log)) {
        Say ('FG RESULT compile PASS log=' + $run.Log)
        return 0
    }
    Show-LogExcerpt $run.Log $ErrPattern
    Say ('FG RESULT compile FAIL log=' + $run.Log)
    return 1
}

function Invoke-Build {
    $run = Invoke-UnityRun 'build' '-quit -executeMethod FishGame.EditorTools.ProjectBuilder.BuildAll'
    switch ($run.Status) {
        'LOCKED'  { Say 'FG RESULT build LOCKED (close the Unity editor, then rerun)'; return 2 }
        'TIMEOUT' { Say ('FG RESULT build TIMEOUT pid=' + $run.ProcId + ' log=' + $run.Log); return 3 }
    }
    $text = Get-LogText $run.Log
    if ($text -match 'FG BUILDALL FAIL') {
        foreach ($line in (Get-Content -LiteralPath $run.Log -Encoding UTF8)) {
            if ($line -match '^FG BUILDALL ERROR ') { Say $line }
        }
        Show-LogExcerpt $run.Log $ErrPattern
        Say ('FG RESULT build FAIL log=' + $run.Log)
        return 1
    }
    if (($run.ExitCode -eq 0) -and (Test-CompileClean $run.Log) -and ($text -match 'FG BUILDALL PASS')) {
        Say ('FG RESULT build PASS log=' + $run.Log)
        return 0
    }
    Show-LogExcerpt $run.Log $ErrPattern
    Say ('FG RESULT build FAIL log=' + $run.Log)
    return 1
}

function Invoke-Tests([string]$Name, [string]$Platform, [string]$XmlName) {
    $xml = Join-Path $OutDir $XmlName
    if (Test-Path -LiteralPath $xml) { Remove-Item -LiteralPath $xml -Force }
    # Never pass -quit with -runTests.
    $extra = '-runTests -testPlatform ' + $Platform + ' -testResults "' + $xml + '" -dev-profile fgtest'
    if (-not [string]::IsNullOrEmpty($Filter)) { $extra = $extra + ' -testFilter "' + $Filter + '"' }
    $run = Invoke-UnityRun $Name $extra
    switch ($run.Status) {
        'LOCKED'  { Say ('FG RESULT ' + $Name + ' LOCKED (close the Unity editor, then rerun)'); return 2 }
        'TIMEOUT' { Say ('FG RESULT ' + $Name + ' TIMEOUT pid=' + $run.ProcId + ' log=' + $run.Log); return 3 }
    }

    $total = 0; $passed = 0; $failed = 0; $skipped = 0
    if (Test-Path -LiteralPath $xml) {
        try {
            [xml]$doc = Get-Content -LiteralPath $xml -Raw -Encoding UTF8
            $runNode = $doc.SelectSingleNode('/test-run')
            if ($null -ne $runNode) {
                $total = [int]$runNode.GetAttribute('total')
                $passed = [int]$runNode.GetAttribute('passed')
                $failed = [int]$runNode.GetAttribute('failed')
                $skipped = [int]$runNode.GetAttribute('skipped')
                foreach ($case in $doc.SelectNodes('//test-case[@result="Failed"]')) {
                    $message = ''
                    $messageNode = $case.SelectSingleNode('failure/message')
                    if ($null -ne $messageNode) { $message = [string]$messageNode.InnerText }
                    $firstLine = ($message.Trim() -split "`r?`n")[0]
                    Say ('FG FAILED ' + $case.GetAttribute('fullname') + ' :: ' + $firstLine)
                }
            }
        }
        catch {
            Say ('FG LOG could not parse ' + $xml)
        }
    }

    $logText = Get-LogText $run.Log
    $completed = $logText -match 'Test run completed\. Exiting with code 0'
    $completedAny = $logText -match 'Test run completed\. Exiting with code'
    if (($total -gt 0) -and ($failed -eq 0) -and $completed) {
        Say ('FG RESULT ' + $Name + ' PASS total=' + $total + ' passed=' + $passed + ' failed=0 skipped=' + $skipped + ' log=' + $run.Log)
        return 0
    }
    if ($total -eq 0) {
        Show-LogExcerpt $run.Log $ErrPattern
        Say ('FG RESULT ' + $Name + ' FAIL total=0 (no tests matched or compile errors) log=' + $run.Log)
        return 1
    }
    $reason = ''
    if (-not $completedAny) { $reason = ' (no completion line in log)' }
    Say ('FG RESULT ' + $Name + ' FAIL total=' + $total + ' passed=' + $passed + ' failed=' + $failed + ' skipped=' + $skipped + ' log=' + $run.Log + $reason)
    return 1
}

# ---- entry point ----
if ($Commands -notcontains $Command) {
    Exit-Usage ("unknown or missing command '" + $Command + "'")
}
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    Exit-Usage 'no -OutDir given and FG_OUT is not set'
}
if ((-not [IO.Path]::IsPathRooted($OutDir)) -or $OutDir.Contains('..')) {
    Exit-Usage ('OutDir must be absolute and must not contain ..: ' + $OutDir)
}
$outFull  = [IO.Path]::GetFullPath($OutDir).TrimEnd('\')
$rootFull = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')
if ($outFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or $outFull.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)) {
    Exit-Usage ('OutDir must be outside the repo: ' + $OutDir)
}
if (-not (Test-Path -LiteralPath $UnityExe)) {
    Exit-Usage ('Unity editor not found at ' + $UnityExe)
}
$OutDir = $outFull
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$code = 1
# After a check PASS, record the tested commit for the merge gate (.claude/hooks/guard.sh).
function Write-PassMarker {
    try {
        $sha = & git -C $ProjectRoot rev-parse HEAD 2>$null
        $common = & git -C $ProjectRoot rev-parse --git-common-dir 2>$null
        if ([string]::IsNullOrEmpty($sha) -or [string]::IsNullOrEmpty($common)) {
            Say 'FG MARKER skipped (git rev-parse failed)'
            return
        }
        $sha = ([string]$sha).Trim()
        $common = ([string]$common).Trim()
        if (-not [IO.Path]::IsPathRooted($common)) { $common = Join-Path $ProjectRoot $common }
        $dir = Join-Path $common 'fg-check-pass'
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Set-Content -LiteralPath (Join-Path $dir $sha) -Value (Get-Date -Format o) -Encoding ASCII
        $dirty = & git -C $ProjectRoot status --porcelain --untracked-files=no 2>$null
        if ($dirty) { Say ('FG MARKER ' + $sha + ' (uncommitted changes were present; the marker covers the commit only)') }
        else { Say ('FG MARKER ' + $sha) }
    }
    catch {
        Say ('FG MARKER failed: ' + $_.Exception.Message)
    }
}

switch ($Command) {
    'compile'   { $code = Invoke-Compile }
    'build'     { $code = Invoke-Build }
    'test-edit' { $code = Invoke-Tests 'test-edit' 'EditMode' 'editmode.xml' }
    'test-play' { $code = Invoke-Tests 'test-play' 'PlayMode' 'playmode.xml' }
    'check' {
        $code = Invoke-Build
        if ($code -eq 0) { $code = Invoke-Tests 'test-edit' 'EditMode' 'editmode.xml' }
        if ($code -eq 0) { $code = Invoke-Tests 'test-play' 'PlayMode' 'playmode.xml' }
        switch ($code) {
            0       { Say 'FG RESULT check PASS'; Write-PassMarker }
            2       { Say 'FG RESULT check LOCKED (close the Unity editor, then rerun)' }
            3       { Say 'FG RESULT check TIMEOUT' }
            default { Say 'FG RESULT check FAIL' }
        }
    }
}
exit $code
