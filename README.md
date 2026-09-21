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

## Umbraco content setup

The Umbraco content model isn't in source control (see "Known limitations"), so on a fresh database you
have to create it by hand in the backoffice. The Razor views and the rest of the app rely on the exact
**aliases** below, so keep them as written.

1. **Install Umbraco.** On first boot the installer asks for an admin user; use whatever you like.
2. **Create two document types** (Settings → Document Types). ModelsBuilder runs in `InMemoryAuto`
   mode, so no code generation step is needed.

   **Home** — alias `home`, template `Home`, allowed at the content root, allowed child type: Attraction.

   | Property name | Alias | Suggested editor |
   |---|---|---|
   | Park Name | `parkName` | Text string |
   | Tagline | `tagline` | Text string |
   | Intro | `intro` | Textarea |

   **Attraction** — alias `attraction`, template `Attraction`.

   | Property name | Alias | Suggested editor | Notes |
   |---|---|---|---|
   | Tagline | `tagline` | Text string | |
   | Description | `description` | Textarea | |
   | Minimum Height | `minimumHeight` | Numeric (whole number) | Centimetres |
   | Thrill Level | `thrillLevel` | Text string (or Dropdown) | The demo uses `Family`, `Thrilling`, `Extreme` |
   | Operations ID | `operationsId` | Text string | Must match the attraction's ID in the Operations API (see below) |

   The editors are suggestions that match the model types (`string`/`int`); only the aliases are
   load-bearing.

   The views already exist in `src/Aetheria.Web/Views/` (`Home.cshtml`, `Attraction.cshtml`). Create the
   templates in the backoffice with the aliases `Home` and `Attraction` so they bind to those files, then
   run `git status` to make sure the backoffice didn't overwrite them (restore with `git checkout` if it did).
3. **Create the content tree.** The node name drives the URL, so use exactly these names.

   ```
   Aetheria Adventure Park        (Home)
   ├── Clockwork Citadel          (Attraction)
   ├── Stormwing                  (Attraction)
   ├── Deepwood Expedition        (Attraction)
   └── Orbitfall                  (Attraction)
   ```

   Home: Park Name `Aetheria Adventure Park`, Tagline `Where your adventures begin`, Intro
   `Welcome to Aetheria Adventure Park!`

   | Node | Operations ID | Tagline | Min. height | Thrill level |
   |---|---|---|---|---|
   | Clockwork Citadel | `clockwork-citadel` | Time is running out. | 130 | Thrilling |
   | Stormwing | `stormwing` | Ride the storm. | 130 | Extreme |
   | Deepwood Expedition | `deepwood-expedition` | Not every path through the forest should be followed. Or should it? | 100 | Family |
   | Orbitfall | `orbitfall` | Gravity is optional. | 140 | Extreme |

   Description: Clockwork Citadel uses *"Enter the abandoned workshop of legendary clockmaker Aurelius
   Vale, where centuries-old machinery has mysteriously begun moving again."* The other three are empty in
   the current demo, so write your own or leave them blank.
4. **Publish everything** (Save and Publish, with descendants), then open the site. Each attraction page
   should show a live status and wait time. If it says "Live operational information unavailable", the
   Operations ID doesn't match a ride in the Operations API, or the API is down.

## Adding another ride

A new ride touches both Umbraco and the Operations API, and its ID has to line up everywhere. `Aetheria.Web`
itself needs no code changes: the Home page lists every Attraction node it finds, and the Attraction view
works for any Operations ID.

1. **Umbraco:** create a new Attraction node under Home, fill in the fields, and set **Operations ID** to a
   lowercase kebab-case ID (for example `moonlight-carousel`). Publish it.
2. **Operations API (required):** add an entry with the same ID to `SeedData` in
   `src/Aetheria.Operations.Api/AttractionOperationsRepository.cs` (ID, display name, starting wait time,
   `"Open"`). There is no endpoint for creating rides, and the update endpoints return 404 for unknown IDs,
   so this can't be done at runtime.
3. **Reseed Redis:** seeding only runs when Redis holds no attractions, so an existing Redis volume won't
   pick up the new ride by itself. Restart the AppHost and run the **"Reset park operations"** dashboard
   command (it clears the keys and reseeds), or delete the `aetheria-operations-redis-data` volume.
4. **Aspire dashboard command (needed for the demo):** add the ride to the options of the
   **"Set attraction wait time"** command in `src/Aetheria.AppHost/AppHost.cs`, as
   `new KeyValuePair<string, string>("moonlight-carousel", "Moonlight Carousel")`. Without it the command
   can't target the ride (the API itself still works through Scalar).
5. **Simulator profile (optional):** add the ride to `AttractionProfiles` in
   `src/Aetheria.WaitTimeSimulator/SimulationEngine.cs` with a `Popularity` (1.0 is average) and whether it
   is `IsIndoor`. The simulator discovers new rides from the API on its own, but without a profile it treats
   the ride as average popularity and outdoor, so it will react to the `Rain` scenario like an outdoor ride.

Only rides with status `Open` are simulated and shown with a wait time. The status can be changed with
`PUT /api/attractions/{id}/status` (`Open`, `Closed`, `Temporarily Closed` or `Delayed Opening`).

The ID appears in four places, and it has to be identical in each:

| Where | What |
|---|---|
| Umbraco | Attraction node → **Operations ID** |
| `AttractionOperationsRepository.cs` | `SeedData` entry ID |
| `AppHost.cs` | Option key in the "Set attraction wait time" command |
| `SimulationEngine.cs` | `AttractionProfiles` key (optional) |

## Known limitations

- **No version-controlled Umbraco content model.** The `Home` and `Attraction` document types and the
  actual content nodes only exist in the SQL Server data volume — there's no uSync export or unattended
  content seed in source control. A genuinely fresh/empty database means reinstalling Umbraco and
  hand-authoring content in the backoffice (see "Umbraco content setup" above). This was an intentional
  scope cut for the first version, not an oversight.
- **Attraction IDs are repeated by hand.** The same ID lives in Umbraco, the Operations API seed data, the
  AppHost dashboard command and the simulator profiles (see "Adding another ride"), with nothing checking
  that they agree.
- **Duplicated DTOs and HTTP client logic.** `AttractionOperationalState` and a near-identical
  `ParkOperationsClient` are defined independently in `Aetheria.Operations.Api`, `Aetheria.WaitTimeSimulator`,
  and `Aetheria.Web` rather than living in one shared contracts project. Worth extracting if this grows
  beyond a demo.

## Demo script

A suggested live walkthrough, assuming the database already has its demo content (see "Known
limitations" above — this script deliberately doesn't start from a wiped database). The order is
deliberate: the simulator only starts near the end, so until then nothing changes on its own and the
traces list stays quiet.

### Before the demo

1. **Start the app**: `dotnet run --project src/Aetheria.AppHost`, and open the Aspire dashboard URL from
   the console output.
2. **Reset park conditions**: run **"Reset park operations"** on `operations-api` (restores the seeded wait
   times and statuses, and clears any artificial latency) and **"Reset simulation"** on
   `wait-time-simulator` (09:00, Normal scenario, paused). Leave the simulator paused.
3. **Warm up**: load the Home page and one Attraction page once, so the first request on stage isn't a
   slow cold start. Then clear the traces view in the dashboard if you want a clean list.

### The demo

1. **Tour the dashboard**: point out the five resources (`sql`, `operations-cache`, `operations-api`,
   `wait-time-simulator`, `web`), all healthy via their `/health` checks. Open the RedisInsight link on
   `operations-cache` to show the live Redis keys (`aetheria:operations:attraction:*`) — despite the name,
   this Redis is the source of truth for wait times, not a cache. Briefly show the SQL Server resource
   (Umbraco's database).
2. **Show the Umbraco backoffice**: log in, open the `Home` and one `Attraction` content item, and point
   out the plain CMS fields (`ParkName`/`Intro`/`Tagline`, and `Description`/`MinimumHeight`/
   `OperationsId`/`ThrillLevel`). This part is just Umbraco, nothing Aspire-specific yet.
3. **Show the website**: open the `web` resource's URL for the Home page, then click into an Attraction.
   It renders the static CMS fields *and* a live status and wait time pulled from the Operations API at
   request time — the "Umbraco vs. the API-driven UI" moment: one page, two data sources, rendered
   together server-side.
4. **Baseline trace**: reload the Attraction page, then open **Traces** in the dashboard and find the
   request. Expand it to show the web request, its outgoing call to `operations-api`, and the API's own
   handling of it (the custom spans `Load live attraction operations` in `web` and
   `Load attraction operations` in `operations-api`). Note the short durations.
5. **Add latency**: run **"Set API latency"** on `operations-api` with the default 2000 ms. Reload the
   Attraction page (it now takes about two seconds), and open the new trace. The whole request is slower,
   and the time is spent in the API's request span, before its inner `Load attraction operations` span.
   Keep the value well under 10 seconds: the HTTP client's standard resilience handler times out and
   retries slow calls.
6. **Clear the latency**: run **"Clear API latency"**, reload, and show the fast trace again.
7. **Make it move manually**: run **"Set attraction wait time"** on `operations-api` for the attraction
   you're viewing, then refresh the Attraction page — the number updates. This shows service discovery
   (`https+http://operations-api`) working end to end.
8. **Introduce the simulator**: explain `wait-time-simulator` as a background worker that advances a
   simulated park clock and nudges wait times toward a time-of-day/popularity/scenario-driven target.
   Run **"Resume simulation"**, or **"Advance 15 park minutes"** a couple of times for manual control,
   and refresh the Attraction page between clicks to show it updating on its own. The traces view now
   fills with the simulator's calls to the API.
9. **Change the scenario**: run **"Change park scenario"** → `Peak` (or `Rain`, to show indoor vs. outdoor
   attractions reacting differently), advance again, and show wait times responding to the multiplier
   live.
10. **(Optional) Show the API directly**: open the Operations API's `/scalar` page to show the raw API
    surface.
11. **Wrap up**: run **"Reset park operations"** and **"Reset simulation"** again to leave the app in a
    clean state for Q&A or a re-run.

## Tests

```
dotnet test
```

- `Aetheria.AppHost.Tests` — an Aspire integration smoke test (`DistributedApplicationTestingBuilder`)
  that boots the whole distributed app and asserts every resource becomes healthy. Requires Docker.
- `Aetheria.WaitTimeSimulator.Tests` — unit tests for the pure wait-time simulation math in
  `SimulationEngine` (time-of-day curve, scenario multipliers, clamping).
