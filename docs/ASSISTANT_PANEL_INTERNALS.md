# Assistant panel internals — Feelin' Lucky, Epicor, Salesforce, and the AI merge path

Source of truth: working tree on `bluebrick-assistant-slice1-foundation` (Sep 2026).
Live-probed against the Prod bridge `:17178` where noted.

## 1. Feelin' Lucky (`ClsLucky.FeelingLucky`, `Forms/FrmPane.cs:624`)

Entry: `cmbLkySrch` text + Enter/button → `ClsLucky.FeelingLucky(this, cmbLkySrch.Text)`.

| Build | Source | Query | Cap | Open path |
|---|---|---|---|---|
| Lab (`LAB_BUILD`) | `VaultWorkspaceFactory.Current.Search(sSearch, 12)` — local lab vault index | free text over indexed Id/FileName/Directory | 12 | `PdmFile` → `OpenLocalFile` → `swApp.OpenDoc6` on the resolved local path |
| Prod | EPDM COM: `EdmVault5.LoginAuto("_PDMVault")` → `IEdmSearch8` | `FileName LIKE %.SLD%` AND (`Description` OR `Number` OR `DocumentNumber` OR `PartNumber` OR `FileName` CONTAINS sSearch) | 12, deduped | `PdmFile` → `GetFileCopy` → `swApp.OpenDoc6` (silent); insert-mode drags into assemblies |

No HTTP, no API key, no database server. Variables live in PDM data cards (`ClsEnums.EnumPDMVars`). Thumbnails via `GetThumbnail3`, fallback `Resource1.thumb`. All progress/errors go to the status strip (`SetStat`).

Assistant equivalent (already built): `search_local_vault` (Lab vault index) and `search_pdm` (read-only PDM gateway, no login attempted — requires an already-authenticated PDM session plus `AssistantTools.EnablePdmSearch` + `Pdm.AllowAssistantReadOnlySearch`). Live probe: `search_pdm` currently reports disabled-by-config with that exact reason.

## 2. Epicor Part Search (`ClsEpicor.EpiSearch`)

Entry: part/description fields + Enter/button → `ClsEpicor.EpiSearch(this, sPart, sDesc)` → rows in `lstEPSResults`.

- Transport: direct `System.Data.SqlClient` — hardcoded `Server=EPICOR10;Database=Vira;Trusted_Connection=True` (Windows auth; only works on corp network as the user).
- Query: `EpicorProd.Erp.Part` LEFT JOIN summed `PartWhse` (`Company = N'VIRAINS'`), `PartNum LIKE %part%` AND `PartDescription LIKE %desc%`, `ORDER BY PartNum`. Returns PartNum / Description / IUM / OnHand / Allocated.
- Siblings in `ClsEpicor.cs`: `UsageSearch` (part→customers), `QuoteAttach`, `ProductCat` (category dropdown source), `TaskSearch`, `OppEmail` — same connection pattern.
- No REST endpoints; the "API" is T-SQL over the trusted connection.

Assistant equivalent (already built): `search_epicor` (`Agent/AssistantToolService.cs`) — same read-only shape, but the connection string comes from the `AssistantTools.EpicorConnectionStringEnvironmentVariable` env var (default `BLUEBRICK_EPICOR_CONNECTION_STRING`), and both the flag and the var must be present or the tool reports disabled with the reason. Live probe: currently disabled — env var is missing on this machine. To test the merge: set that env var (e.g. to the same `Server=EPICOR10;…` value) and restart SOLIDWORKS; no code change needed.

## 3. Salesforce (`Archived/ClsSalesForce.cs` — archived, panel vestigial)

The task-pane Salesforce block (login as Chris Dault, Opp/Cust/Due/User fields) is UI shell only: `Forms/FrmPane.cs` handlers are commented out (`//var sf = new ClsSalesForce…`, `//todo: implement for salesforce`, "Not yet implemented" message boxes, "Salesforce integration archived."). There is **no** `search_salesforce` assistant tool — the scope registry names it, but the catalog has no descriptor, so it always resolves unavailable. Merge gap: a Salesforce tool would need a new authenticated API design (OAuth via `FrmOpts` `txtSfAccess`/`txtSfRefresh` fields), not an adaptation of existing code.

## 4. How the merge already works (no custom search needed)

Chat Search button (`AssistantWeb/src/App.tsx:handleSearch`) posts `{type:"search", message, scopeId}` → host → `POST /assistant/tool` → `AssistantToolService.ExecuteAsync` → scope-gated (`AssistantScopeRegistry`: local_vault / pdm / epicor / both_all) → typed result cards back in the thread. Live probe: `search_local_vault` returns `ok` + audit receipt end to end. The 8-tool catalog (`/assistant/tools`) carries `Enabled` + `UnavailableReason` per tool; scope chips render both, so a disabled Epicor/PDM explains itself instead of failing silently.

## 5. Roadmap: semantic detail-understanding search

Not built. The path the user described (bolt screw + nuts → correct sizes/finishes/types available in PDM/SOLIDWORKS) needs, in order: (a) PDM metadata export (PartNumber/Description/Finish/Material columns — the `search_pdm` gateway returns metadata already), (b) an embedding index over that export (new; nothing in-tree), (c) a `search_semantic` tool descriptor following the `search_pdm` pattern (policy + receipts + scope), (d) UI exposure via the existing scope-chip mechanism. Do not reimplement keyword search — extend the tool catalog.
