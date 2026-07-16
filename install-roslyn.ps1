# Downloads the official netstandard2.0 Roslyn C# compiler assemblies from NuGet
# and installs them into Assets/Plugins/Roslyn for use as a Unity runtime/editor plugin.
# Run this once api.nuget.org is reachable:  ! powershell -ExecutionPolicy Bypass -File install-roslyn.ps1

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot
$dest = Join-Path $proj 'Assets\Plugins\Roslyn'
$tmp  = Join-Path $env:TEMP ('roslyn_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
New-Item -ItemType Directory -Path $dest -Force | Out-Null

# package : version : dllName-to-extract-from-lib/netstandard2.0
$pkgs = @(
  'microsoft.codeanalysis.common:4.9.2:Microsoft.CodeAnalysis.dll',
  'microsoft.codeanalysis.csharp:4.9.2:Microsoft.CodeAnalysis.CSharp.dll',
  'system.collections.immutable:8.0.0:System.Collections.Immutable.dll',
  'system.reflection.metadata:8.0.0:System.Reflection.Metadata.dll',
  'system.runtime.compilerservices.unsafe:6.0.0:System.Runtime.CompilerServices.Unsafe.dll',
  'system.memory:4.5.5:System.Memory.dll',
  'system.buffers:4.5.1:System.Buffers.dll',
  'system.numerics.vectors:4.5.0:System.Numerics.Vectors.dll',
  'system.threading.tasks.extensions:4.5.4:System.Threading.Tasks.Extensions.dll',
  'microsoft.bcl.asyncinterfaces:8.0.0:Microsoft.Bcl.AsyncInterfaces.dll',
  'system.text.encoding.codepages:8.0.0:System.Text.Encoding.CodePages.dll'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($p in $pkgs) {
  $name, $ver, $dll = $p.Split(':')
  $url  = "https://api.nuget.org/v3-flatcontainer/$name/$ver/$name.$ver.nupkg"
  $nupkg = Join-Path $tmp "$name.$ver.zip"
  Write-Host "Downloading $name $ver ..."
  Invoke-WebRequest -Uri $url -OutFile $nupkg -UseBasicParsing

  $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg)
  try {
    $entry = $zip.Entries | Where-Object { $_.FullName -eq "lib/netstandard2.0/$dll" } | Select-Object -First 1
    if (-not $entry) { throw "lib/netstandard2.0/$dll not found in $name.$ver" }
    $outFile = Join-Path $dest $dll
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $outFile, $true)
    Write-Host "  -> $dll ($([math]::Round($entry.Length/1KB)) KB)"
  } finally { $zip.Dispose() }
}

Remove-Item $tmp -Recurse -Force
Write-Host ""
Write-Host "Done. Installed DLLs:" -ForegroundColor Green
Get-ChildItem $dest -Filter *.dll | ForEach-Object { "  {0,-45} {1,8:N0} KB" -f $_.Name, ($_.Length/1KB) }
Write-Host ""
Write-Host "Switch to the Unity Editor to let it import the new plugins." -ForegroundColor Yellow
