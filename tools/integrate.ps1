<#
.SYNOPSIS
  Integrates an application built with the starter kit into the platform repositories.

.DESCRIPTION
  Reads app.manifest.json, copies the application's backend and frontend folders into the two
  platform repos at the same paths, and applies the registrations the platform needs:
    Program.cs                       provisioner + services + background jobs (backend.hostedServices)
    ModulePermissionSeeder.cs        permission codes
    PlatformSchemaBootstrapper.cs    registry row (route, prefix, provisioner key, scope, menu)
    src/components/page              the page pieces, when the platform lacks them
    App.tsx                          imports + nested routes
    Sidebar.tsx                      mapped flag, expand state, menu entry (removes the placeholder)
    auth.ts                          landing route
  Every insertion is idempotent: running twice changes nothing. Anchors not found are reported
  with the exact text to paste by hand. Use -DryRun to see the plan without touching anything.

.EXAMPLE
  .\integrate.ps1 -BackendRepo D:\repos\Primworth_Backend_API -FrontendRepo D:\repos\Primworth_Frontend
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $BackendRepo,
  [Parameter(Mandatory = $true)] [string] $FrontendRepo,
  [string] $Manifest = (Join-Path $PSScriptRoot '..\app.manifest.json'),
  [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$kit = Resolve-Path (Join-Path $PSScriptRoot '..')
$m = Get-Content -Raw -Encoding UTF8 $Manifest | ConvertFrom-Json

function Say($msg) { Write-Host "  $msg" }
function Plan($msg) { if ($DryRun) { Write-Host "  [dry-run] $msg" } else { Write-Host "  $msg" } }

function Copy-Tree([string] $from, [string] $to) {
  if (-not (Test-Path $from)) { Say "skip (not present): $from"; return }
  Plan "copy  $from  ->  $to"
  if ($DryRun) { return }
  New-Item -ItemType Directory -Force -Path $to | Out-Null
  Copy-Item -Path (Join-Path $from '*') -Destination $to -Recurse -Force
}

function Copy-One([string] $from, [string] $to) {
  if (-not (Test-Path $from)) { Say "skip (not present): $from"; return }
  Plan "copy  $from  ->  $to"
  if ($DryRun) { return }
  New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null
  Copy-Item -Path $from -Destination $to -Force
}

# Platform files keep their own format: a BOM only if they had one, CRLF only if they used it.
# (Set-Content in Windows PowerShell adds a BOM and writes CRLF, which turned whole files into a diff.)
function Read-Text([string] $file) {
  $bytes = [IO.File]::ReadAllBytes($file)
  $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
  $text = [Text.Encoding]::UTF8.GetString($bytes)
  if ($bom) { $text = $text.Substring(1) }
  $nl = "`n"; if ($text.Contains("`r`n")) { $nl = "`r`n" }
  return [pscustomobject]@{ Text = $text; Bom = $bom; Nl = $nl; Lines = [string[]]($text -split "`r?`n") }
}

function Write-Text([string] $file, $format, [string] $text) {
  [IO.File]::WriteAllText($file, $text, (New-Object Text.UTF8Encoding($format.Bom)))
}

function Write-Lines([string] $file, $format, [string[]] $lines) { Write-Text $file $format ($lines -join $format.Nl) }

# Text to insert, as lines. Here-strings carry this script's own CRLF, so split on either.
function Split-Lines([string] $text) { return [string[]]($text -split "`r?`n") }

# Insert $text after the first line matching $anchorRegex, unless $marker is already in the file.
function Insert-After([string] $file, [string] $anchorRegex, [string] $text, [string] $marker, [string] $label) {
  if (-not (Test-Path $file)) { Write-Warning "$label - file not found: $file"; return }
  $f = Read-Text $file
  if ($f.Text.Contains($marker)) { Say "$label - already present, skipped"; return }
  $lines = $f.Lines
  $idx = -1
  for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match $anchorRegex) { $idx = $i; break } }
  if ($idx -lt 0) {
    Write-Warning "$label - anchor not found in $file. Paste this by hand:`n$text`n"
    return
  }
  $add = Split-Lines $text
  Plan "edit  $file  (+$($add.Count) lines after line $($idx + 1))  [$label]"
  if ($DryRun) { return }
  $new = @($lines[0..$idx]) + $add + @($lines[($idx + 1)..($lines.Count - 1)])
  Write-Lines $file $f $new
}

# Insert $text before the first line matching $anchorRegex, unless $marker is already in the file.
# -AboveDocComment: before the /// comment lines that sit directly above the anchor.
function Insert-Before([string] $file, [string] $anchorRegex, [string] $text, [string] $marker, [string] $label, [switch] $AboveDocComment) {
  if (-not (Test-Path $file)) { Write-Warning "$label - file not found: $file"; return }
  $f = Read-Text $file
  if ($f.Text.Contains($marker)) { Say "$label - already present, skipped"; return }
  $lines = $f.Lines
  $idx = -1
  for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match $anchorRegex) { $idx = $i; break } }
  if ($idx -lt 0) {
    Write-Warning "$label - anchor not found in $file. Paste this by hand:`n$text`n"
    return
  }
  if ($AboveDocComment) { while ($idx -gt 0 -and $lines[$idx - 1] -match '^\s*///') { $idx-- } }
  $add = Split-Lines $text
  Plan "edit  $file  (+$($add.Count) lines before line $($idx + 1))  [$label]"
  if ($DryRun) { return }
  $head = @(); if ($idx -gt 0) { $head = $lines[0..($idx - 1)] }
  $new = $head + $add + @($lines[$idx..($lines.Count - 1)])
  Write-Lines $file $f $new
}

# Replace the first line matching $regex with $replacement (single line).
function Replace-Line([string] $file, [string] $regex, [string] $replacement, [string] $label) {
  if (-not (Test-Path $file)) { return }
  $f = Read-Text $file
  $lines = $f.Lines
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $regex) {
      if ($lines[$i] -eq $replacement) { Say "$label - already as required"; return }
      Plan "edit  $file  (line $($i + 1))  [$label]"
      if (-not $DryRun) { $lines[$i] = $replacement; Write-Lines $file $f $lines }
      return
    }
  }
}

# Remove an inert placeholder block: {has<X>Mapped && ( ... )} where the NavLink goes to "#".
function Remove-PlaceholderBlock([string] $file, [string] $flag, [string] $label) {
  if (-not (Test-Path $file)) { return }
  $f = Read-Text $file
  $content = $f.Text
  $pattern = '(?s)\r?\n[ \t]*\{' + [regex]::Escape($flag) + ' && \(\s*<NavLink\s+to="#".*?</NavLink>\s*\)\}'
  if ($content -notmatch $pattern) { Say "$label - no placeholder block, nothing to remove"; return }
  Plan "edit  $file  (remove placeholder block)  [$label]"
  if (-not $DryRun) {
    $content = ([regex]$pattern).Replace($content, '', 1)
    Write-Text $file $f $content
  }
}

$name   = $m.name
$pascal = $m.codeName
$short  = $m.shortName
$camel  = $short.Substring(0,1).ToLower() + $short.Substring(1)
$folder = $m.backend.folder
$feFolder = $m.frontend.folder
$appDir = Join-Path $kit "backend\$($m.backend.project)"
$feSrc  = Join-Path $kit 'frontend\src'

Write-Host ""
Write-Host "Integrating '$name' ($($m.key), scope $($m.scope), prefix $($m.tablePrefix))"
Write-Host "  backend  -> $BackendRepo"
Write-Host "  frontend -> $FrontendRepo"
Write-Host ""

# -- 1. copy backend folders (same paths, same namespaces) ------------------------------------
Write-Host "Backend files"
foreach ($sub in @('Controllers', 'Queries', 'Models', 'Repository', 'Services')) {
  Copy-Tree (Join-Path $appDir "$sub\$folder") (Join-Path $BackendRepo "$sub\$folder")
}
Copy-Tree (Join-Path $appDir 'IRepository') (Join-Path $BackendRepo 'IRepository')

# -- 2. copy frontend files -------------------------------------------------------------------
Write-Host "Frontend files"
Copy-Tree (Join-Path $feSrc "pages\$feFolder") (Join-Path $FrontendRepo "src\pages\$feFolder")
Copy-One  (Join-Path $feSrc "services\$($m.frontend.serviceFile)") (Join-Path $FrontendRepo "src\services\$($m.frontend.serviceFile)")
Copy-One  (Join-Path $feSrc "types\$($m.frontend.typesFile)")      (Join-Path $FrontendRepo "src\types\$($m.frontend.typesFile)")

# The page pieces apps share with the platform (src/components/page). The platform keeps one copy for every
# app: a file it lacks is copied; a file it has in another version is kept and reported, so an app built
# with an older kit never overwrites newer pieces.
$pageSrc = Join-Path $feSrc 'components\page'
$pageDst = Join-Path $FrontendRepo 'src\components\page'
if (Test-Path $pageSrc) {
  foreach ($f in Get-ChildItem -File $pageSrc) {
    $target = Join-Path $pageDst $f.Name
    if (-not (Test-Path $target)) { Copy-One $f.FullName $target }
    elseif ((Get-FileHash $f.FullName).Hash -ne (Get-FileHash $target).Hash) {
      Write-Warning "components/page/$($f.Name) differs from the platform's copy; the platform's is kept. Compare by hand if this app needs the kit's version."
    }
  }
}

# -- 3. Program.cs - provisioner + services ---------------------------------------------------
Write-Host "Registrations"
$reg = @("// -- $name ($short) - $($m.scope.ToLower())-scoped --",
         "builder.Services.AddScoped<IApplicationProvisioner, $($m.backend.provisionerType)>();")
foreach ($pair in $m.backend.services) { $reg += "builder.Services.AddScoped<$($pair[0]), $($pair[1])>();" }
# Background jobs: one instance, resolvable by type (for status endpoints) and started with the host.
foreach ($job in @($m.backend.hostedServices)) {
    if (-not $job) { continue }
    $reg += "builder.Services.AddSingleton<$job>();"
    $reg += "builder.Services.AddHostedService(sp => sp.GetRequiredService<$job>());"
}
Insert-After (Join-Path $BackendRepo 'Program.cs') 'AddScoped<IApplicationProvisioner,' ($reg -join "`n") $m.backend.provisionerType 'Program.cs'

# -- 4. ModulePermissionSeeder.cs - permission codes ------------------------------------------
$perm = @("        // -- $name --")
foreach ($p in $m.permissions) { $perm += "        (`"$($p[0])`", `"$($p[1])`")," }
$seeder = Join-Path $BackendRepo 'Services\Permissions\ModulePermissionSeeder.cs'
# anchor: the line that closes the Permissions array - the first "    };" after the array starts
$seederLines = @(); if (Test-Path $seeder) { $seederFile = Read-Text $seeder; $seederLines = $seederFile.Lines }
$start = -1; $close = -1
for ($i = 0; $i -lt $seederLines.Count; $i++) {
  if ($start -lt 0 -and $seederLines[$i] -match 'Permissions\s*=') { $start = $i; continue }
  if ($start -ge 0 -and $seederLines[$i] -match '^\s*\};\s*$') { $close = $i; break }
}
if ($close -ge 0) {
  if ($seederFile.Text.Contains("(`"$($m.permissionCode)`"")) { Say "ModulePermissionSeeder.cs - already present, skipped" }
  else {
    Plan "edit  $seeder  (+$($perm.Count) lines before line $($close + 1))  [permissions]"
    if (-not $DryRun) {
      $new = @($seederLines[0..($close - 1)]) + $perm + @($seederLines[$close..($seederLines.Count - 1)])
      Write-Lines $seeder $seederFile $new
    }
  }
} else { Write-Warning "ModulePermissionSeeder.cs - Permissions array not found. Add:`n$($perm -join "`n")" }

# -- 5. PlatformSchemaBootstrapper.cs - the registry row --------------------------------------
$navJson = ($m.frontend.nav | ForEach-Object {
  "{""icon"":""$($_.icon)"",""label"":""$($_.label)"",""route_path"":""$($_.route_path)"",""permission_code"":""$($_.permission_code)""}"
}) -join ','
$scopeName = $m.scope.ToLower()
if ($scopeName -eq 'subproject') { $scopeName = 'sub project' }
# The app's own one-time step, as a method above RegisterSlabYardApplication and called right after it.
$boot = @"
        /// <summary>
        /// One-time registration of $name as an integrated application: ${scopeName}-scoped, tables
        /// provisioned by $($m.backend.provisionerType.Split('.')[-1]). Gated on provisioner_key IS NULL like the
        /// Slab Yard registration, so after the first boot the row belongs to the admin.
        /// </summary>
        private static void Register${pascal}Application(IDbConnection connection, IDbTransaction tx)
        {
            var updated = connection.Execute(@"
                UPDATE public.application
                   SET is_integrated        = TRUE,
                       route_path           = '$($m.frontend.routeBase)',
                       permission_code      = '$($m.permissionCode)',
                       module_table_prefix  = '$($m.tablePrefix)',
                       provisioner_key      = '$($m.provisionerKey)',
                       nav_children         = @nav_children::jsonb,
                       application_scope_id = COALESCE(
                           (SELECT scope_id FROM public.application_scope
                             WHERE LOWER(TRIM(scope)) = '$scopeName' LIMIT 1),
                           application_scope_id)
                 WHERE is_deleted = FALSE
                   AND provisioner_key IS NULL
                   AND LOWER(TRIM(application_name)) = '$($name.ToLower())';",
                new { nav_children = @"[$($navJson.Replace('"', '""'))]" }, tx);
            if (updated > 0)
                LoggerHelper.LogSystemEvent("[PlatformSchemaBootstrapper] Registered '$name' as an integrated application.");
        }

"@
$bootFile = Join-Path $BackendRepo 'Services\PlatformSchemaBootstrapper.cs'
Insert-Before $bootFile 'private static void RegisterSlabYardApplication\(' $boot "provisioner_key      = '$($m.provisionerKey)'" 'PlatformSchemaBootstrapper.cs registration' -AboveDocComment
Insert-After $bootFile '^\s*RegisterSlabYardApplication\(connection, tx\);' "                Register${pascal}Application(connection, tx);" "Register${pascal}Application(connection, tx);" 'PlatformSchemaBootstrapper.cs call'

# -- 6. App.tsx - imports + routes ------------------------------------------------------------
$appTsx = Join-Path $FrontendRepo 'src\App.tsx'
$imports = @("import ${pascal}Root from './pages/$feFolder/$($m.frontend.root)';")
# One import per page component: two routes (the app's home and its first page) may show the same page.
foreach ($c in @($m.frontend.routes | ForEach-Object { $_.component } | Select-Object -Unique)) { $imports += "import $c from './pages/$feFolder/$c';" }
# after the last existing page import
$lastImport = '^import .* from ''\./pages/'
$lines = @(); if (Test-Path $appTsx) { $appFile = Read-Text $appTsx; $lines = $appFile.Lines }
$li = -1; for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match $lastImport) { $li = $i } }
if ($li -ge 0 -and -not ((Get-Content -Raw $appTsx).Contains("import ${pascal}Root"))) {
  Plan "edit  $appTsx  (+$($imports.Count) import lines after line $($li + 1))  [App.tsx imports]"
  if (-not $DryRun) {
    $new = @($lines[0..$li]) + $imports + @($lines[($li + 1)..($lines.Count - 1)])
    Write-Lines $appTsx $appFile $new
  }
} elseif ($li -lt 0) { Write-Warning "App.tsx - no page imports found; add:`n$($imports -join "`n")" } else { Say "App.tsx imports - already present" }

$routeBase = $m.frontend.routeBase.TrimStart('/')
$routes = @("          {/* $name - $scopeName-scoped; ${pascal}Root renders child pages via <Outlet />. */}",
            "          <Route path=""$routeBase"" element={<${pascal}Root />}>")
foreach ($r in $m.frontend.routes) {
  if ($r.path -eq '') { $routes += "            <Route index element={<$($r.component) />} />" }
  else { $routes += "            <Route path=""$($r.path)"" element={<$($r.component) />} />" }
}
$routes += "          </Route>"
# before the </Route> that closes the authenticated shell, i.e. the last "</Route>" before "</Routes>"
$lines = @(); if (Test-Path $appTsx) { $appFile = Read-Text $appTsx; $lines = $appFile.Lines }
$ri = -1; $rs = -1
for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^\s*</Routes>') { $rs = $i; break } }
if ($rs -gt 0) { for ($i = $rs - 1; $i -ge 0; $i--) { if ($lines[$i] -match '^\s*</Route>\s*$') { $ri = $i; break } } }
if ($ri -ge 0 -and -not ((Get-Content -Raw $appTsx).Contains("path=""$routeBase"""))) {
  Plan "edit  $appTsx  (+$($routes.Count) route lines before line $($ri + 1))  [App.tsx routes]"
  if (-not $DryRun) {
    $new = @($lines[0..($ri - 1)]) + $routes + @($lines[$ri..($lines.Count - 1)])
    Write-Lines $appTsx $appFile $new
  }
} elseif ($ri -lt 0) { Write-Warning "App.tsx - route block anchor not found; add:`n$($routes -join "`n")" } else { Say "App.tsx routes - already present" }

# -- 7. Sidebar.tsx - mapped flag, expand state, entry; drop the placeholder ------------------
$sidebar = Join-Path $FrontendRepo 'src\components\Sidebar.tsx'
$flag = "has${pascal}Mapped"
# Project: the project's own apps. SubProject: the current selection. Both: either — mapped to the
# project (plant-wide) or to the selected facility.
$mappedExpr = switch ($m.scope) {
  'Project' { "isProjectAppMapped('$name')" }
  'Both'    { "isProjectAppMapped('$name') || isAppMapped('$name')" }
  default   { "isAppMapped('$name')" }
}
$nameRx = [regex]::Escape($name)
Replace-Line $sidebar "^\s*const $flag = is(Project)?AppMapped\('$nameRx'\)( \|\| isAppMapped\('$nameRx'\))?;" "  const $flag = $mappedExpr;" 'Sidebar.tsx mapped flag'
Insert-After $sidebar "const \[slabYardExpanded, setSlabYardExpanded\]" "  const [${camel}Expanded, set${short}Expanded] = useState(false);" "${camel}Expanded" 'Sidebar.tsx expand state'
if (-not ((Get-Content -Raw $sidebar).Contains("const $flag ="))) {
  Insert-After $sidebar "const hasKnowledgeManagementSystemMapped" "  const $flag = $mappedExpr;" "const $flag" 'Sidebar.tsx mapped flag (new)'
}
Remove-PlaceholderBlock $sidebar $flag 'Sidebar.tsx placeholder'
$items = @()
foreach ($n in $m.frontend.nav) { $items += "              { to: '$($n.route_path)', icon: '$($n.icon)', label: '$($n.label)', perm: '$($n.permission_code)' }," }
$entry = @("          {",
           "            key: '$camel',",
           "            home: '$($m.frontend.routeBase)',",
           "            perm: '$($m.permissionCode)',",
           "            mapped: $flag,",
           "            label: '$name',",
           "            icon: '$($m.icon)',",
           "            match: /^\$([regex]::Escape($m.frontend.routeBase))/,",
           "            open: ${camel}Expanded,",
           "            toggle: () => set${short}Expanded(v => !v),",
           "            items: [") + $items + @("            ],", "          },")
Insert-Before $sidebar "^\s*key: 'mms',\s*$" ($entry -join "`n") "key: '$camel'," 'Sidebar.tsx entry'
# Insert-Before matched the "key: 'mms'," line; the entry must go before the "{" that precedes it.
# Fix up: if the entry landed between "{" and "key: 'mms'", move that "{" above the entry.
if (-not $DryRun -and (Test-Path $sidebar)) {
  $f = Read-Text $sidebar; $c = $f.Text; $nl = $f.Nl
  $bad = "{$nl          {$nl            key: '$camel',"
  if ($c.Contains($bad)) {
    $c = $c.Replace($bad, "{$nl            key: '$camel',")
    $c = $c.Replace("          },$nl            key: 'mms',", "          },$nl          {$nl            key: 'mms',")
    Write-Text $sidebar $f $c
  }
}

# -- 8. auth.ts - landing route ---------------------------------------------------------------
# Two shapes in the platform's history: a candidates list (homeRouteForUser, the spike) and an if-chain
# (getDefaultLandingRoute, Staging_Frontend). The app's line goes right after Slab Yard's in either.
$authTs  = Join-Path $FrontendRepo 'src\auth.ts'
$landing = $m.frontend.routeBase
$code    = $m.permissionCode
if ((Test-Path $authTs) -and (Read-Text $authTs).Text.Contains("if (hasPermission('SYMS')) return '/slab-yard';")) {
  Insert-After $authTs "if \(hasPermission\('SYMS'\)\) return '/slab-yard';" "  if (hasPermission('$code')) return '$landing';" "hasPermission('$code')) return" 'auth.ts landing route'
} else {
  Insert-After $authTs "\['SYMS', '/slab-yard'\]," "    ['$code', '$landing']," "['$code'," 'auth.ts landing route'
}

# -- 9. what remains manual -------------------------------------------------------------------
Write-Host ""
Write-Host "Next steps"
Say "1. Build the backend:  dotnet build   (new files compile against the platform unchanged)"
Say "2. Restart the API once: the bootstrapper registers the app, the seeder adds its permissions."
Say "3. Map '$name' to a project in the platform UI - the provisioner creates its tables."
Say "4. Grant $($m.permissionCode) (and the module codes) to a role."
Write-Host ""
Say "For a database that will not restart soon (staging), the registry row as SQL:"
Write-Host @"
UPDATE public.application
   SET is_integrated = TRUE, route_path = '$($m.frontend.routeBase)', permission_code = '$($m.permissionCode)',
       module_table_prefix = '$($m.tablePrefix)', provisioner_key = '$($m.provisionerKey)',
       application_scope_id = (SELECT scope_id FROM public.application_scope WHERE LOWER(TRIM(scope)) = '$scopeName'),
       nav_children = '[$navJson]'::jsonb
 WHERE LOWER(TRIM(application_name)) = '$($name.ToLower())' AND provisioner_key IS NULL;
"@
