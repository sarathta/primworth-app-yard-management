# Merge checklist — Yard Management

A reviewer ticks every line. Any "no" blocks the merge.

## Database
- [ ] Every table is created by `YardProvisioner.ProvisionAsync` with `CREATE TABLE IF NOT EXISTS` — running provisioning twice changes nothing.
- [ ] Every table name starts with `sp_yard_` and is singular.
- [ ] Every table has `pk_`, audit columns (`created_by/created_on/updated_by/updated_on`) and `is_deleted`; reference tables also have `is_active`.
- [ ] Every `*_id` column is a primary key, a foreign key, or documented as a cross-schema reference validated in code (`assigned_user_id`, `performed_by` → `public.sys_users`).
- [ ] No table duplicates a platform master: no grades, facilities, equipment, users, roles or projects inside `sp_yard_*`. Grades are `gm_grade.grade_code`.
- [ ] Seeds use `INSERT … WHERE NOT EXISTS`, never `DELETE` / `TRUNCATE`.
- [ ] Every table has a `COMMENT ON TABLE` in the `{"n":"…","d":"…"}` format so it reads properly in Masters.
- [ ] `DescribeMastersUiRules` lists every table: material types editable, operational tables not.
- [ ] Every item-specific field lives in `attributes` and is declared in the material type's `attribute_defs` with a type and a unit. No new slab-only column.
- [ ] The unique index on `(position_id, stack_index)` is untouched.

## Backend
- [ ] Every controller is under `Controllers/Yard/`, routed `api/yard/…`, derives from `YardControllerBase`, and carries `[Authorize]` and `[RequiresPermission(...)]`.
- [ ] No `[AllowAnonymous]` except a webhook receiver that authenticates with its own per-endpoint token.
- [ ] Schema comes from `IYardSchemaContext`; no controller or repository reads `X-Project-Id` / `X-Sub-Project-Id` itself. Sub-project only: no fallback to the project schema.
- [ ] No second `DbContext`, no connection string, no Entity Framework.
- [ ] SQL lives in `Queries/Yard/`; the only interpolation is `{schema}`.
- [ ] Any write that moves an item runs in one transaction and goes through `YardWorkOrderRepository.CompleteAsync`; nothing else sets `position_id` or `stack_index`.
- [ ] Any decision about where an item goes is in `YardPlacement`; nothing else raises an INBOUND or DIGGING order.
- [ ] Storage style is read from the area, never assumed: digging only when `YardStorageStyle.Digs(style)`.
- [ ] Failures return real status codes (`400`, `404`, `409`, `500`) — never `200` with `Success=false`. Exception text is logged, not returned.
- [ ] No `.Result` / `.Wait()`; no empty `catch`. No file over 500 lines.
- [ ] Registration is the lines in the `Program.cs` snippet — nothing else touched.
- [ ] Permission codes exist in `ModulePermissionSeeder` and match what the frontend checks.

## Frontend
- [ ] All pages under `src/pages/yard/`, all API calls in `src/services/yard.ts`, all shapes in `src/types/yard.ts`.
- [ ] Only `authGet/authPost/authPut/authDelete` from `src/api.ts` are used — no `fetch`, no `axios`, no hand-built headers.
- [ ] No word for the material is typed into a screen: "Slab", "Coil", "Slab ID", "Pit" all come from `useYardWords()`.
- [ ] Fields for an item come from the material type's `attribute_defs`, not from a fixed form.
- [ ] Pages use the platform's components and stylesheet classes only (`components/page`, `components/system`) — no Tailwind, no second UI library.
- [ ] `PageHeader` with the Add button top right, a `TableCard` with sortable headers and per-row `IconAction`s, `Modal` forms with Cancel left / Save right; `useConfirm` only before deletes and dispatches.
- [ ] Routes nested under the single `yard` route in `App.tsx`; one sidebar entry gated on the mapped application and `YRDL`, matched with `isAppMapped` (sub-project).
- [ ] `YardRoot` shows the "select a sub project" notice when no facility is selected and remounts the pages when it changes.
- [ ] No `any`, no `@ts-ignore`, no `console.log`, no hardcoded URLs. No file over 800 lines.

## Repo hygiene
- [ ] No secrets, dumps, `.env` or build output in the diff.
- [ ] Provisioned a fresh facility end-to-end on a clean database: an area added, an item ingested and allocated, the INBOUND order completed, the item dispatched with the dig orders expected. Screenshot attached.
- [ ] On a facility that already ran Slab Yard: `sql/rename_slab_yard_to_yard.sql` applied, then the "slab plant sees no change" checklist from the Yard Management Developer Guide passes.
