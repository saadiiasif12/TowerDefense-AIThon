# GUID dependency-closure copier: pulls named MagicArsenal assets + everything they
# reference (materials/textures/shaders/scripts/audio) from the local Particles-Library
# into this project, preserving relative paths and GUIDs. Skips files already imported.
param()

$lib = "D:\GameDevelopment\Assets\ParticlesLibrary\Particles-Library\Assets\MagicArsenal"
$dst = "D:\GameDevelopment\Projects\TowerDefense-AIThon\Assets\TowerDefense\ThirdParty\MagicArsenal"

$roots = @(
    "$lib\Effects\Prefabs\Projectiles\Fire\FireProjectileNormal.prefab",
    "$lib\Effects\Prefabs\Projectiles\Fire\FireImpactMega.prefab",
    "$lib\Effects\Prefabs\Pillar Blast\LightningPillarBlast.prefab",
    "$lib\Effects\Prefabs\SphereBlast\V2\FrostSphereBlastV2.prefab",
    "$lib\Effects\Prefabs\Aura\Legacy\Frost\GroundFrost.prefab",
    "$lib\Effects\Sound\Impact\fireimpact.wav",
    "$lib\Effects\Sound\Impact\lightningimpact.wav"
)

Write-Host "Indexing library GUIDs..."
$guidToPath = @{}
Get-ChildItem $lib -Recurse -Filter "*.meta" | ForEach-Object {
    $m = Select-String -Path $_.FullName -Pattern "^guid: ([0-9a-f]{32})" | Select-Object -First 1
    if ($m) { $guidToPath[$m.Matches[0].Groups[1].Value] = $_.FullName.Substring(0, $_.FullName.Length - 5) }
}
Write-Host ("Indexed {0} guids" -f $guidToPath.Count)

# GUIDs already in the project (skip re-copy)
$have = @{}
Get-ChildItem $dst -Recurse -Filter "*.meta" -ErrorAction SilentlyContinue | ForEach-Object {
    $m = Select-String -Path $_.FullName -Pattern "^guid: ([0-9a-f]{32})" | Select-Object -First 1
    if ($m) { $have[$m.Matches[0].Groups[1].Value] = $true }
}

$queue = New-Object System.Collections.Queue
$seen = @{}
foreach ($r in $roots) { $queue.Enqueue($r) }

$copied = 0
while ($queue.Count -gt 0) {
    $file = $queue.Dequeue()
    if ($seen.ContainsKey($file)) { continue }
    $seen[$file] = $true
    if (-not (Test-Path $file)) { Write-Host "MISSING: $file"; continue }

    # Copy file + meta (skip if this guid already imported)
    $metaPath = "$file.meta"
    $guidLine = Select-String -Path $metaPath -Pattern "^guid: ([0-9a-f]{32})" | Select-Object -First 1
    $guid = if ($guidLine) { $guidLine.Matches[0].Groups[1].Value } else { $null }
    $rel = $file.Substring($lib.Length)
    $target = Join-Path $dst $rel

    if ($guid -and $have.ContainsKey($guid)) {
        # already imported - crawl only (deps may still be new)
    }
    else {
        $targetDir = Split-Path $target -Parent
        if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force $targetDir | Out-Null }
        Copy-Item $file $target -Force
        Copy-Item $metaPath "$target.meta" -Force
        $copied++
        Write-Host ("COPY {0}" -f $rel)
    }

    # Crawl text-based assets for guid references
    $ext = [System.IO.Path]::GetExtension($file).ToLower()
    if ($ext -in ".prefab", ".mat", ".asset", ".controller") {
        $content = Get-Content $file -Raw
        $refs = [regex]::Matches($content, "guid: ([0-9a-f]{32})") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
        foreach ($g in $refs) {
            if ($guidToPath.ContainsKey($g)) { $queue.Enqueue($guidToPath[$g]) }
        }
    }
}
Write-Host ("DONE - {0} new files copied" -f $copied)
