# Signal Desk API

.NET 11 / FastEndpoints backend for Signal Desk. Includes Scalar, EF Core SQLite, an offline sample catalog, and an opt-in intelligence pipeline using Perplexity Search, TypeSafe Jev, and OpenAI through Microsoft Agent Framework.

## Run locally

Install the stable SDK pinned in `global.json`, then run from this repository:

```sh
dotnet restore SignalDesk.slnx --locked-mode
dotnet run --project src/SignalDesk.Api --launch-profile http
```

On this Mac the stable SDK is already installed under `~/.dotnet`; if your shell still selects Preview 2, prepend that directory to `PATH` for these commands.

- Scalar: <http://localhost:5188/scalar/v1>
- OpenAPI: <http://localhost:5188/openapi/v1.json>
- Catalog: <http://localhost:5188/v1/catalog>
- Readiness: <http://localhost:5188/health/ready>

The default **sample** mode makes no provider calls and needs no credentials. It imports the frontend's actual 14 fictional records, 12 competitor profiles, and seven linked publishers. Do not cite sample stories or financials as reporting.

The API has no authentication: documentation and all API operations are anonymous. Source approval, budget limits, and live-feature configuration remain required. Production database-dependent operations remain unavailable while hosted database integration is deferred; this is a persistence guard, not an authentication requirement.

SQLite files live under `data/runtime/` and are Git-ignored. Sample and live use separate filenames and persisted dataset markers; changing a database's mode in place is rejected. A process-level file lock prevents two local application processes using the same database. EF migrations apply before local readiness; failed migrations stop startup.

## Connect the existing frontend

From the frontend checkout, opt in explicitly:

```sh
VITE_SIGNAL_DESK_API_URL=http://localhost:5188 bun run dev
```

Without that variable, the original mock transport remains selected. The API transport handles ETags/304, errors, cancellation, catalog validation, and relative profile media. Workspace, tray and reader windows use the same source. API-supplied activity is authoritative rather than duplicated by device-local profile diffs.

The Tauri development CSP permits only the local API on port 5188 in addition to its existing origins. A later hosted desktop release must add its exact approved HTTPS API/media origins to the production CSP; no shared application or provider key belongs in `VITE_*` variables.

## HTTP compatibility

- `GET /v1/catalog` returns the existing `{records,competitors,publishers,mode}` shape, without a pagination envelope. It reads stored data; Refresh does not trigger paid research.
- Signals retain linked company/publisher IDs, the seven exact categories, importance 0–100, source paragraphs and stable IDs.
- Profiles include image/overview/capabilities, four financial rows with absent values when unknown, incorporation when supported, and activity. Live begins with unknown facts rather than fictional fixture figures.
- `POST /v1/briefs` accepts `{records,objective,audience,mode}` and returns synchronous `{title,text}`. Accepts 1–20 unique stored record IDs; client-supplied titles/text are not trusted. Framing is `Strategic brief`, `Summarize`, or `Explain`. Objectives are 1–3000 characters; audiences match the desktop's four choices.
- Resource endpoints: `/v1/signals`, `/v1/signals/{id}`, `/v1/competitors`, `/v1/competitors/{id}`, `/v1/competitors/{id}/activity`, `/v1/publishers`, `/v1/publishers/{id}`.
- `/v1/signals` supports competitor/publisher/category/importance/date/text filters, a result limit of 1–100, and a cursor containing the last returned ID. A cursor outside the filtered result set returns 400.
- Existing highlights, bookmarks, notes, folders, notifications, sharing, branding and document exports remain local.

Scalar describes the actual FastEndpoints routes and DTOs. Its optional Scalar Agent feature is disabled. Scalar and OpenAPI are enabled publicly in every environment, including Production, without authentication or a documentation feature flag. Provider credentials are never included.

## AI services and enabling live collection

The fixed catalog is in `AI/AiServiceCatalog.cs` and exposed through anonymous `GET /v1/ai/services`:

- `perplexity-search`: ranked URL/excerpt discovery through **POST /search**, not Sonar or Perplexity's Agent API.
- `typesafe-decisions`: batched Noul, Choice and Score judgments using pinned `jev-1.14.0`.
- `openai-research-planner`: bounded query proposals over approved company/topic scopes.
- `openai-transformer`: structured, evidence-cited signal/profile proposals.
- `openai-briefs`: analysis of selected stored evidence only; no implicit open-web browsing.
- `intelligence-orchestrator`: typed Microsoft Agent Framework stages, with FastEndpoints/EF as durable scheduling/state storage.

Use server-side environment variables or .NET user secrets for `TYPESAFE_API_KEY`, `PERPLEXITY_API_KEY`, and `OPENAI_API_KEY`. Never commit them, print them, or put them in the desktop. Choose account-accessible stable OpenAI model IDs; none is guessed by default.

`appsettings.json` contains the non-secret defaults. For local overrides, use the Git-ignored `src/SignalDesk.Api/appsettings.Local.json` or corresponding environment variables:

```json
{
  "Desk": {
    "Mode": "live",
    "EnableLiveAi": false,
    "EnableCollectors": false,
    "DailyBudgetUsd": 0,
    "DailySearchQueries": 50,
    "DailyDocuments": 50
  },
  "AiServices": {
    "TypeSafeModel": "jev-1.14.0",
    "PlannerModel": "",
    "TransformerModel": "",
    "BriefModel": "",
    "SearchQueryReserveUsd": 0,
    "DecisionReserveUsd": 0,
    "OpenAiReserveUsd": 0
  }
}
```

Before enabling either live flag:

1. Approve a real evaluation/recurring spend cap, configure credentials and model roles, and review provider retention/data handling. Only public source evidence and explicitly requested analyst objectives should be sent; local notes/highlights are not sent.
2. Set positive conservative per-call USD reservations and the UTC daily budget. Reservations include failed/unknown calls and are **estimates, not a provider billing guarantee**. Size them against the selected models' current input/output limits and prices and set provider-account spending limits too.
3. Review sources and authorize search access and article capture separately. In live mode all 98 publishers from `data/sources.json` import as unapproved; a homepage is not a working feed or redistribution license.
4. Enable live AI, then collectors. Startup remains local-only. Use Scalar to approve a source and queue a controlled first run.

Example source approval body for `PATCH /v1/sources/{id}`:

```json
{
  "url": "https://spacenews.com",
  "kind": "search",
  "approved": true,
  "allowArticleCapture": false,
  "rightsAcknowledgement": "Replace with the actual approval, permitted use, and reviewer reference."
}
```

This example is **not** a claim that source access/redistribution has been approved. `allowArticleCapture: false` retains discovery metadata only. RSS requires a verified same-publisher feed URL. Restricted/social/video/paywall sources remain off unless an authorized path is established; no login/paywall bypass or browser scraper is included.

## Collection and review

`POST /v1/ingestion/runs` with `{"sourceId":"spacenews"}` returns 202 plus a run ID/Location. Inspect `/v1/ingestion/runs/{id}` and retry terminal failed runs via `POST /v1/ingestion/runs/{id}/retry`. These operations are anonymous but still require collection/source/budget configuration; they are not frontend Refresh.

The scheduler checks due approved sources every minute. Search sources are due daily and feeds hourly, subject to the daily query/document limits. Query plans contain at most three queries per source; Perplexity returns at most 20 combined web results. Sources rotate by due time. Search has a two-day overlap and a bounded initial lookback; coverage is best-effort, not all web reporting.

Pipeline:

1. OpenAI proposes source-scoped queries; code fixes domain/date filters and bounds query count.
2. Perplexity or a verified feed supplies candidates. Persist request IDs and discoveries before processing.
3. Approved direct capture validates HTTPS host, DNS/public destinations, redirects, size/time limits and strips executable/navigation HTML. XML external entities are prohibited.
4. TypeSafe screens relevance and company association, chooses a domain with a companion fit judgment, and rates commercial significance.
5. OpenAI proposes summary and exact-source-cited facts. Code validates IDs, schema and quotations; TypeSafe checks support/contradiction.
6. Publish an attributed signal or hold for review. Profile fact proposals always require human approval. Importance is a versioned ranking heuristic, not a calibrated probability of truth.

Original paragraphs and published revisions remain immutable for saved highlights. Missing full text or a supported publication timestamp does not cause invented article content/time. Such evidence stays pending review. Financial amounts and legal facts are not independently verified just because a model says they are supported.

Inspect proposals/evidence with `GET /v1/review/items`; decide through `POST /v1/review/items/{id}/decision` with `{"accept":true,"note":"Actual reviewer rationale"}`. Acceptance, published signals/facts, and the audit decision commit together. Do not accept an incomplete or unsupported proposal; unavailable timestamps require corrected evidence, not a guessed time.

Permanent bad article destinations/access failures/oversize pages are quarantined per hit. Budget/rate-limited jobs defer until the next UTC day; other failures have bounded attempts and remain inspectable rather than deleted. Provider failures never become a negative relevance judgment or fictional live result.

## Cost, recovery, and audit boundaries

`GET /v1/ai/executions` exposes sanitized execution/usage/reservation state, not cached response bodies or keys. Completed evidence calls are cached by model/prompt/question/input hash. Analyst briefs are not cached in the database.

An interrupted or timed-out request may have been billed upstream. `reserved`, `dispatched`, or `failed-or-unknown` executions **block automatic replay of the same request**. Reconcile with the provider, then explicitly authorize another possibly billable attempt through `POST /v1/ai/executions/{id}/allow-retry` with a detailed note, after its timeout window. The previous reservation is not refunded.

The ledger is an application safeguard, not a substitute for provider spending limits, exact billing reconciliation, or a distributed transaction. Cached public search/decision/transform responses and approved evidence remain in the local database; default HTTP logs do not contain provider payloads/keys. Define retention/encryption policy before sensitive or production usage.

For local backup, stop collection and use SQLite's backup API (or a closed database), not a file copy of an actively written WAL database. Keep backups outside version control. Do not automatically delete an EF migration lock following an interrupted migration; establish that no migration process is running and follow EF/SQLite recovery guidance first.

## Validation

```sh
dotnet test SignalDesk.slnx
dotnet format SignalDesk.slnx --verify-no-changes --no-restore
dotnet build SignalDesk.slnx -c Release --no-restore
dotnet publish src/SignalDesk.Api -c Release --no-build -o artifacts/api
```

Tests use isolated real SQLite files, HTTP/provider fakes, and no paid APIs. Coverage includes frontend relationships, Scalar/OpenAPI, conditional reads, one-record briefs, source import, SSRF/XXE guards, uncertain decisions, workflow publication, review atomicity, dataset separation, anonymous access, provider request shapes, budget caps and ambiguous-outcome replay protection. Live precision/recall, cost and latency evaluations still require approved representative data and a paid test budget.

## Azure status

The existing `he-ai-poc-test` deployment workflow/secret reference is preserved. Its build now restores locked dependencies, audits packages, checks formatting, runs offline tests and explicitly publishes the API project. No local database or local configuration file is published.

Hosted database selection/provisioning remains **deferred**. Authentication is not part of this API. Scalar and OpenAPI are public in Production. In Production, liveness works but readiness and `/v1` data operations return 503; the API never silently creates an ephemeral/network-share SQLite database. Do not treat a successful CD deployment as a production intelligence service until the hosted EF provider, migration/data-import strategy, secure connectivity, backups, concurrency and operational evaluations are completed.
