# Aetheria

Aetheria Adventure Park is a demo built for a talk on Umbraco + .NET Aspire. It's a small distributed
app: an Umbraco 18 website, a Redis-backed "Operations API" that tracks live ride wait times and
status, and a background worker that simulates a day at the park — all orchestrated by a .NET Aspire
AppHost.

## Architecture

Five projects under `src/`:

| Project | What it is |
|---|---|
| `Aetheria.AppHost` | The Aspire orchestrator. Declares SQL Server, Redis, and the three services below, wires them together, and adds custom Aspire dashboard commands for driving the demo live. |
| `Aetheria.Web` | The Umbraco 18 CMS site. This **is** the UI — there is no separate front-end app. |
| `Aetheria.Operations.Api` | A minimal API, backed by Redis, that is the source of truth for each attraction's live wait time and status. |
| `Aetheria.WaitTimeSimulator` | A background worker that advances a simulated park clock and nudges wait times toward a time-of-day/popularity/scenario-driven target, pushing updates to the Operations API over HTTP. |
| `Aetheria.ServiceDefaults` | Shared Aspire service-defaults (OpenTelemetry, health checks, service discovery, resilience) referenced by the three runtime projects above. |

Plus two small test projects: `Aetheria.AppHost.Tests` (an Aspire integration smoke test) and
`Aetheria.WaitTimeSimulator.Tests` (unit tests for the wait-time simulation math).

The key thing to understand: **`Aetheria.Web` is the UI that shows data from the API service** —
there's no separate SPA/Blazor front end. Its Umbraco views call `Aetheria.Operations.Api` server-side
(via `ParkOperationsClient`, using Aspire service discovery — `https+http://operations-api`) and render
the live status/wait time inline, next to ordinary CMS-authored content (tagline, description, thrill
level). One page, two data sources, rendered together server-side.

## Running it

Requires Docker (for the SQL Server and Redis containers).

```
dotnet run --project src/Aetheria.AppHost
```

This starts the Aspire dashboard plus all resources. From the dashboard you can reach:
- The Umbraco backoffice and website (`web` resource)
- The Operations API's Scalar API reference at `/scalar` (dev only)
- RedisInsight, linked from the `operations-cache` resource

## Known limitations

- **No version-controlled Umbraco content model.** The `Home` and `Attraction` document types and the
  actual content nodes only exist in the SQL Server data volume — there's no uSync export or unattended
  content seed in source control. A genuinely fresh/empty database means reinstalling Umbraco and
  hand-authoring content in the backoffice. This was an intentional scope cut for the first version, not
  an oversight.
- **Duplicated DTOs and HTTP client logic.** `AttractionOperationalState` and a near-identical
  `ParkOperationsClient` are defined independently in `Aetheria.Operations.Api`, `Aetheria.WaitTimeSimulator`,
  and `Aetheria.Web` rather than living in one shared contracts project. Worth extracting if this grows
  beyond a demo.

## Demo script

A suggested live walkthrough, assuming the database already has its demo content (see "Known
limitations" above — this script deliberately doesn't start from a wiped database).

1. **Start the app**: `dotnet run --project src/Aetheria.AppHost`. While it boots, open the Aspire
   dashboard URL from the console output.
2. **Tour the dashboard**: point out the five resources (`sql`, `operations-cache`, `operations-api`,
   `wait-time-simulator`, `web`), all healthy via their `/health` checks. Open the RedisInsight link on
   `operations-cache` to show the live Redis keys (`aetheria:operations:attraction:*`). Briefly show the
   SQL Server resource (Umbraco's database).
3. **Reset to a known state** (instead of wiping the database): run the **"Reset park operations"**
   command on `operations-api` and **"Reset simulation"** on `wait-time-simulator`. These are custom
   Aspire dashboard commands (`WithHttpCommand` in `AppHost.cs`) hitting real endpoints — a good early
   "here's something Aspire gives you for free" beat.
4. **Show the Umbraco backoffice**: log in, open the `Home` and one `Attraction` content item, point out
   the plain CMS fields (`ParkName`/`Intro`/`Tagline`, and `Description`/`MinimumHeight`/`OperationsId`/
   `ThrillLevel`) — this part is just Umbraco, nothing Aspire-specific yet.
5. **Show the website — Home page**: open the `web` resource's URL. It shows the park name/intro/tagline
   and a list of attractions.
6. **Click into an Attraction page**: point out it renders the static CMS fields *and* a live "Status" +
   "Wait time" pulled from the Operations API at request time — this is the "Umbraco vs. the API-driven
   UI" moment: one page, two data sources, rendered together server-side.
7. **Make it move manually**: back in the Aspire dashboard, run **"Set attraction wait time"** on
   `operations-api` for the attraction you're viewing, then refresh the Attraction page — the number
   updates. Demonstrates service discovery (`https+http://operations-api`) end to end.
8. **Introduce the simulator**: explain `wait-time-simulator` as a background worker that advances a
   simulated park clock and nudges wait times toward a time-of-day/popularity/scenario-driven target.
   Run **"Resume simulation"**, then **"Advance 15 park minutes"** once or twice, refreshing the
   Attraction page between clicks to show it updating on its own.
9. **Change the scenario**: run **"Change park scenario"** → `Peak` (or `Rain`, to show indoor vs.
   outdoor attractions reacting differently), advance again, and show wait times responding to the
   multiplier live.
10. **(Optional/time-permitting) Observability beat**: open the Operations API's `/scalar` page to show
    the raw API surface, then use the `/api/demo/latency` endpoint to inject artificial delay and show
    the resulting slow trace/span in the Aspire dashboard's traces view — ties together OpenTelemetry
    across `Web` → `Operations.Api`.
11. **Wrap up / reset for next run**: "Reset park operations" + "Reset simulation" again to leave the
    app in a clean state for Q&A or a re-run.

## Tests

```
dotnet test
```

- `Aetheria.AppHost.Tests` — an Aspire integration smoke test (`DistributedApplicationTestingBuilder`)
  that boots the whole distributed app and asserts every resource becomes healthy. Requires Docker.
- `Aetheria.WaitTimeSimulator.Tests` — unit tests for the pure wait-time simulation math in
  `SimulationEngine` (time-of-day curve, scenario multipliers, clamping).
