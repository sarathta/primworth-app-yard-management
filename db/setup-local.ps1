<#
.SYNOPSIS
  Prepares a locally installed PostgreSQL for the starter kit - the no-Docker alternative to
  db/docker-compose.yml.

.DESCRIPTION
  Creates the database and loads db/init/01_platform_core.sql into it: the platform's core
  tables, a sample project and sub-project, grades and equipment. Needs psql on PATH (it ships
  with PostgreSQL; add <PostgreSQL>\bin to PATH if it is not found). You will be prompted for
  the password unless PGPASSWORD is set.

.EXAMPLE
  .\setup-local.ps1
  .\setup-local.ps1 -PgHost localhost -Port 5432 -User postgres -Database primworth_dev
#>
[CmdletBinding()]
param(
  [string] $PgHost = 'localhost',
  [int]    $Port = 5432,
  [string] $User = 'postgres',
  [string] $Database = 'primworth_dev'
)

$ErrorActionPreference = 'Stop'
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
  throw "psql was not found on PATH. Add your PostgreSQL bin folder (e.g. C:\Program Files\PostgreSQL\16\bin) to PATH and retry."
}
$init = Join-Path $PSScriptRoot 'init\01_platform_core.sql'

Write-Host "Creating database '$Database' on ${PgHost}:$Port as '$User' ..."
& psql -h $PgHost -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $Database;"
if ($LASTEXITCODE -ne 0) { throw "CREATE DATABASE failed. If '$Database' already exists, drop it or choose another -Database name; the init script expects an empty database." }

Write-Host "Loading the platform's core tables and sample data ..."
& psql -h $PgHost -p $Port -U $User -d $Database -v ON_ERROR_STOP=1 -q -f $init
if ($LASTEXITCODE -ne 0) { throw "Loading $init failed." }

Write-Host ""
Write-Host "Done. Point the dev host at this database - either edit ConnectionStrings:DBConnection in"
Write-Host "backend/Platform.DevHost/appsettings.json, or set it for the session before 'dotnet run':"
Write-Host ""
Write-Host "  `$env:ConnectionStrings__DBConnection = 'Host=$PgHost;Port=$Port;Database=$Database;Username=$User;Password=<your password>;Include Error Detail=true'"
Write-Host ""
