# Yard Management — developer starter kit

Build the Yard Management application **without the platform**. This folder runs on its own: a
small stand-in API host, the platform's own UI shell, and a seeded Postgres. Everything you write
here is already in the shape the platform expects, and the yard code is already in it: the generic
data model and native API underneath, the six mSYMS screens on top, exactly as they run in the
platform today. When you hand it back, integration is one script.

The only application code in this kit is Yard Management. The platform's stand-ins are
contracts and a dev host; no other module is here.

```
primworth-app-yard-management/
├── app.manifest.json             describes the app to the platform (routes, menu, permissions, services)
├── backend/
│   ├── PrimworthApp.sln
│   ├── Platform.Sdk/             the platform's contracts (interfaces, envelope, attribute) — read-only
│   ├── Platform.DevHost/         runnable stand-in: dev login, tenant headers, provisioning, Swagger
│   └── Apps.Yard/                ← YOUR CODE. Same folder layout as the platform.
│       ├── Controllers/Yard/     native API (areas, items, work orders) + compat API for the mSYMS screens
│       ├── IRepository/
│       ├── Models/Yard/          material type + attribute validation, zone, item, work order, masters
│       ├── Queries/Yard/         every table (YardDdl.cs) and every SQL statement
│       └── Repository/Yard/      provisioner, schema context, placement rule, repositories
├── frontend/
│   ├── src/api.ts  auth.ts  types.ts          the platform's client contract — read-only
│   ├── src/components/  src/styles/           the platform's shell and page pieces — read-only
│   ├── src/pages/yard/msyms/                  ← the mSYMS screens, contexts, API shim and scoped Tailwind CSS
│   ├── src/pages/yard/*.tsx                   the files the manifest names: MsymsRoot + six screens (re-exports)
│   ├── src/services/yard.ts  src/types/yard.ts   client for the native API
│   ├── tailwind.msyms.js  postcss.config.js   Tailwind confined to the msyms folder
│   └── _platform-style-pages/                 an alternative UI in the platform's own pattern; not wired
├── db/                           Postgres with the platform's core tables and a sample plant + facility
├── sql/                          yard_schema.sql (local DB without the host), rename_slab_yard_to_yard.sql
├── integration-snippets/         the lines integrate.ps1 pastes into the platform, for reference
├── tools/integrate.ps1           copies the app into the platform repos and registers it
└── CHECKLIST.md                  what a reviewer checks before merging
```

## Run it

Prerequisites: .NET 8 SDK (or newer), Node 20+, and PostgreSQL 14+ — either Docker Desktop
(step 1) or a local install (see *Without Docker*).

```powershell
# 1. Database: Postgres on port 5440 with the platform's core tables and a sample plant
docker compose -f db/docker-compose.yml up -d

# 2. API: on startup it provisions the sp_yard_* tables into the sample facility, then serves
dotnet run --project backend/Platform.DevHost -- --environment Development
#    -> http://localhost:9950/swagger

# 3. UI
cd frontend
npm install
npm run dev
#    -> http://localhost:5175   sign in as dev@local / dev
```

Then, in the UI: open **Yard** in the sidebar and pick the facility **Melt Shop 1** when asked
(Yard Management is facility-scoped). You land on the yard overview, which is empty until you
lay the yard out:

1. **Configuration → Yard Topology**: provision a sector, e.g. BAY-A, type Storage, 2 × 3 grid,
   capacity 5. Add one of type Scarfing, one Dispatch and one Shuffling the same way.
2. Slabs arrive from the caster feed in a real plant. Here, post one to the native API from
   Swagger (`POST /api/yard/items`, headers `X-Project-Id: 1`, `X-Sub-Project-Id: 1`):
   ```json
   { "item_code": "SL-1001", "heat_number": "H-27841", "grade_code": "IS 2062 E250", "weight_kg": 24500,
     "attributes": { "width_mm": 1250, "thickness_mm": 220, "cast_time": "2026-10-07T08:00:00" } }
   ```
   The response says where it goes. An attribute the slab type does not define is refused in words.
3. **Operator**: the INBOUND order is on the worklist; confirm the move. The slab appears in its
   pit on the **Yard Overview**, hot for two minutes, then warm, then cold.
4. **Mill Schedule**: the campaign lists it hottest first; Transfer plans the dispatch, digging
   out anything on top of it first. **Dispatch** ships it with a label.
5. **Quality**: a held slab can be released or sent for rework; rework routes it to the Scarfing
   sector with the activity chosen.

### Without Docker

Any PostgreSQL 14+ works. Create the database and load the core tables:

```powershell
.\db\setup-local.ps1 -PgHost localhost -Port 5432 -User postgres -Database primworth_dev
```

or by hand: `createdb primworth_dev` then `psql -d primworth_dev -f db/init/01_platform_core.sql`.
Then point the dev host at it, in `backend/Platform.DevHost/appsettings.json`
(`ConnectionStrings:DBConnection`) or for the session:

```powershell
$env:ConnectionStrings__DBConnection = 'Host=localhost;Port=5432;Database=primworth_dev;Username=postgres;Password=...;Include Error Detail=true'
dotnet run --project backend/Platform.DevHost -- --environment Development
```

The init script expects an empty database; to start over, drop it and run the setup again.

### Dev user and permissions

The dev user is a super admin: every permission passes. To test the gates, set
`Dev:Permissions` in `backend/Platform.DevHost/appsettings.json` to a list of codes, for example
`["YRDL", "YRDD", "YRDO"]` to see what an operator without Quality or Administration sees.
The seven codes: YRDL the app, YRDD overview, YRDO operator, YRDQ quality, YRDU release a hold,
YRDA administration, YRDH mill and dispatch. `Dev:Projects` does the same for project access.

## What is already built, and where to go on from it

| Done | Where | Next step |
|---|---|---|
| Fourteen `sp_yard_*` tables, SLAB material type and the rule book seeded | `Queries/Yard/YardDdl.cs`, `Repository/Yard/YardProvisioner.cs` | Add tables for endpoints, decision models, audits as the phases call for them; always `IF NOT EXISTS`. |
| Facility resolution with access check | `Repository/Yard/YardSchemaContext.cs` | Nothing. Every controller already goes through it. |
| Ingest with attribute validation, then allocation (product-mix routing when the module is on) | `Repository/Yard/YardItemRepository.cs`, `YardPlacement.cs` | A webhook receiver maps a feed payload into `YardItemInput` and calls `CreateItemAsync`. |
| Retrieval that digs only in a stacking area; rework to a Scarfing sector | `Repository/Yard/YardPlacement.cs` | The editable decision graph plugs into `Choose`. |
| The move: lock, index = max + 1, repack, log | `Repository/Yard/YardWorkOrderRepository.cs` | Nothing for racks and floors; pyramids later. |
| The six mSYMS screens on the compat API | `Controllers/Yard/YardCompat*.cs`, `frontend/src/pages/yard/msyms/` | Phase 1: words from the material type. Phase 2: rack and floor drawings. Audits and the simulator from the spike. |

Read the **Yard Management Developer Guide** (PDF alongside the FRD) for the phases: it says
what to build in which order and how to prove a slab plant sees no change.

## The eight rules

1. **Your tables are created by your provisioner**, idempotently, in the facility schema. No
   `project_id` or `sub_project_id` columns; the schema is the scope. Never in `public`.
2. **One database.** No connection string of your own; `DapperContext` is given to you.
3. **Dapper and SQL.** No Entity Framework.
4. **Context comes from the platform.** Project and facility from the `X-Project-Id` and
   `X-Sub-Project-Id` headers (the API client sends them), user from the token. Read them only
   through `YardSchemaContext`, which checks `IProjectAccessGuard` first.
5. **Use platform masters; never duplicate them.** Grades = `gm_grade`, facilities = sub projects,
   equipment = `sp_equipment_instance`, people = `sys_users`. Add only what the platform lacks.
6. **Declare permissions** in the manifest; enforce with `[RequiresPermission("CODE")]`.
7. **UI lives in the shell.** New pages are built from the pieces in `src/components/page` and
   `src/components/system`, call the API through `authGet/authPost/authPut/authDelete`, and use no
   Tailwind. The mSYMS screens are the one exception: their Tailwind is confined to `.msyms-root`
   and they call the compat API through `symsApi.ts`. Keep it that way; do not spread either.
8. **Runs inside the site EXE.** No external service. Timer work is a `BackgroundService` under
   `Services/Yard/`, visiting one facility schema at a time.

Naming: tables `sp_yard_<singular>`; keys `<entity>_id bigint GENERATED ALWAYS AS IDENTITY`;
audit `created_by/created_on/updated_by/updated_on`; flags `is_active`, `is_deleted`; constraints
`pk_ fk_ uq_ ck_`; permission codes `YRD?`; routes `api/yard/<resource>`; C# prefix `Yard`.

## Handing it back

1. Run `CHECKLIST.md` against your code.
2. Send the folder without `bin/`, `obj/`, `node_modules/`.
3. Primworth runs `tools/integrate.ps1 -BackendRepo … -FrontendRepo …`, restarts the API, and
   maps Yard Management to a facility. On a site that already runs Slab Yard,
   `sql/rename_slab_yard_to_yard.sql` runs first and the old `slab-yard` folders come out.
