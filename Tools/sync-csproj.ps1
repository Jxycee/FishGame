# Tools/sync-csproj.ps1 - give a git worktree the Unity-generated C# project files so the csharp-lsp plugin
# (csharp-ls) can navigate code there without a Unity run. ASCII only. Windows PowerShell 5.1 compatible.
#
# Usage (from anywhere):
#   powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\sync-csproj.ps1 [-Target <worktree dir>]
# Target defaults to the current directory.
#
# Copies *.csproj and *.slnx from the main checkout (C:\Dev\FishGame, where Unity generates them) into Target.
# When Target has no Library of its own, references into Library\ (package DLLs, ScriptAssemblies) are rewritten to the
# main checkout's Library, so package types (Netcode, Input System, Newtonsoft) still resolve. Compile items stay
# relative, so the LSP reads Target's own sources. Files added in Target after the last Unity sync are not listed in the
# copied projects; re-run after Unity regenerates them. These files are gitignored: never commit them.
# Exit codes: 0 done (or nothing to do), 1 the main checkout has no generated project files.
param(
    [string]$Target = (Get-Location).Path
)

$Source = 'C:\Dev\FishGame'
$targetFull = [IO.Path]::GetFullPath($Target).TrimEnd('\')
if ($targetFull -ieq $Source) {
    [Console]::Out.WriteLine('sync-csproj: target is the main checkout; nothing to do')
    exit 0
}

$files = @(Get-ChildItem -LiteralPath $Source -File | Where-Object { $_.Extension -eq '.csproj' -or $_.Extension -eq '.slnx' })
if ($files.Count -eq 0) {
    [Console]::Out.WriteLine('sync-csproj: no .csproj/.slnx in ' + $Source + '; open the project once in the Unity editor to generate them')
    exit 1
}

$ownLibrary = Test-Path -LiteralPath (Join-Path $targetFull 'Library\ScriptAssemblies')
$utf8 = New-Object System.Text.UTF8Encoding($true)
foreach ($f in $files) {
    $text = [IO.File]::ReadAllText($f.FullName)
    if (-not $ownLibrary) {
        $text = $text.Replace('>Library\', '>' + $Source + '\Library\').Replace('"Library\', '"' + $Source + '\Library\')
    }
    [IO.File]::WriteAllText((Join-Path $targetFull $f.Name), $text, $utf8)
}

$mode = 'main checkout Library'
if ($ownLibrary) { $mode = 'own Library' }
[Console]::Out.WriteLine('sync-csproj: copied ' + $files.Count + ' files to ' + $targetFull + ' (' + $mode + ')')
exit 0
