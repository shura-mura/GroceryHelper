# GroceryHelper

A client/server demonstration project focused on test-driven development,
code quality, and justified design decisions.

The development scaffold runs ASP.NET Core/C# and React/TypeScript. TUnit
validates the backend test infrastructure; Playwright connects the browser to a
real backend. Authentication and grocery features are not implemented yet.

## Prerequisites

- Latest stable **.NET 10 SDK** (`global.json` selects the latest installed
  10.0 feature band/patch; preview SDKs and .NET 11 are excluded).
- Node.js **22.23.3**, including npm **10.9.9** (`.node-version`, `package.json`).
- Git. No database, Docker, or external service is required.

Check `dotnet --version`, `node --version`, and `npm --version` against these
requirements. CI installs the latest `10.0.x` SDK; update your local .NET SDK
to follow that channel. If using a user-local .NET installation, add its directory to `PATH`
and set `DOTNET_ROOT` to that directory before running commands.

## Install

From the repository root:

```sh
dotnet restore --locked-mode
cd src/GroceryHelper.Web
npm ci
npx playwright install chromium
```

On Linux, install browser system dependencies with
`npx playwright install --with-deps chromium`.

## Run locally

In a terminal at the repository root:

```sh
dotnet run --project src/GroceryHelper.Api --no-launch-profile -- --environment Development --urls http://127.0.0.1:5080
```

In a second terminal:

```sh
cd src/GroceryHelper.Web
npm run dev
```

Open <http://127.0.0.1:5173>. The scaffold should show **Backend connected**.
Vite proxies `/health` and future `/api` calls to the backend, keeping browser
requests on one origin. The setup readiness probe exists only in Development
and Testing, not Production. It is not an application API or authentication
implementation.

### Local observability

The API writes structured newline-delimited JSON to the console. In Development
it also writes rolling files under `src/GroceryHelper.Api/logs/`; the date and a
sequence number are added to `grocery-helper-.ndjson` as files roll. Files roll
daily and at 10 MB, and seven files are retained. The directory is ignored by
Git.

Each completed request records its HTTP method, route template, status, elapsed
milliseconds, and W3C trace ID. Search for that `TraceId` to correlate events.
Health-probe completions are below the default Information level. Request query
strings, bodies, headers, cookies, raw identifiers, and user-entered values are
not logged.

Development also writes OpenTelemetry traces and metrics to the console. Server
spans use W3C trace context and route templates, while HTTP and runtime metrics
cover request duration and activity, failures, CPU, memory, garbage collection,
and thread-pool behavior. Query strings and raw resource identifiers are not
telemetry attributes. The resource is identified as `GroceryHelper.Api`, with
its build version and the current deployment environment.

Safe defaults live under `Observability` in the API `appsettings` files. Override
individual values with normal ASP.NET Core configuration, such as
`Observability__MinimumLevel=Debug` or
`Observability__File__Path=C:\logs\grocery-helper-.ndjson`. File logging is off
outside Development unless `Observability__File__Enabled=true` is explicitly
set. Trace and metric console export is likewise Development-only unless
`Observability__OpenTelemetry__ConsoleExporter__Enabled=true` is explicitly
set. Testing adds no exporter by default, so integration tests can attach their
own in-memory exporters without periodic or shared output. Graceful shutdown
uses the bounded `Observability__OpenTelemetry__FlushTimeoutMilliseconds`
budget. OpenTelemetry's standard sampler configuration and builder remain the
seams for future parent-based production sampling and OTLP export; neither is
selected here. If the optional file destination is invalid or unavailable, the
API keeps running with console logging and reports the sink diagnostic to
standard error.
Production collectors remain intentionally deferred.

API failures use RFC 9457 Problem Details with the
`application/problem+json` media type. Generic failures use `about:blank`,
validation failures use `urn:grocery-helper:problem:validation-error` with
JSON-pointer keys in `errors`, and every problem includes the active W3C
`traceId`. Exception details remain server-side in every environment. The
contract applies under `/api`; health checks and responses that cannot contain
a body are excluded.

The browser writes OpenTelemetry page-load, same-origin fetch, and sanitized
error spans to browser diagnostics. Its resource is identified as
`GroceryHelper.Web`, with the package/build version and Vite deployment mode;
set `BUILD_VERSION` and `DEPLOYMENT_ENVIRONMENT` when producing a deployment
artifact. W3C trace context is added only to same-origin `/api` requests and the
Development/Testing `/health` probe. Browser diagnostics contain an allowlisted
span summary rather than URLs, query strings, headers, DOM or console content,
credentials, user identity, or grocery data. Cross-origin propagation and a
central browser exporter remain disabled until explicit destinations are
selected. Graceful page shutdown attempts a bounded telemetry flush; abrupt
termination can lose pending signals.

## Validation

From the repository root:

```sh
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --project tests/GroceryHelper.UnitTests --configuration Release --no-build -- --minimum-expected-tests 1 --report-trx
dotnet test --project tests/GroceryHelper.IntegrationTests --configuration Release --no-build -- --minimum-expected-tests 1 --report-trx
cd src/GroceryHelper.Web
npm ci
npm run typecheck
npm run lint
npm run build
npm run test:e2e
```

Playwright requires the Release API build above. It starts and stops a fresh
backend and frontend on OS-assigned ports **for each test**, so tests never reuse
your running development servers. It runs Chromium with two workers and no
automatic retries. Use `npm run test:e2e -- --repeat-each=2` to exercise repeated
parallel startup.

For focused work:

```sh
dotnet test --project tests/GroceryHelper.IntegrationTests -- --treenode-filter "/*/*/HostingTests/*"
```

From the frontend directory, use
`npm run test:e2e -- startup.spec.ts` and `npm run typecheck`.
Open browser diagnostics using `npm run test:e2e:report`.

The current unit suite contains a labelled runtime smoke test, **not business
coverage**. Integration tests exercise readiness and environment isolation.
The browser test proves frontend/backend wiring. Domain tests arrive with their
features. See [setup evidence](docs/development/setup-evidence.md).

## Planned functionality

- One predefined account with authentication and password changes.
- Multiple grocery lists with grocery-item CRUD, quantities, and purchased status.
- Reusable category tags, with name search and category filtering within a list.
- In-memory storage that resets when the backend restarts.

Data will survive browser refresh and reset on backend restart. There are no demo
credentials yet because authentication is not implemented. The current demo is
limited to starting both servers and observing the connection status.

## Development

Read [CONTRIBUTING.md](CONTRIBUTING.md) for TDD, test isolation, design standards,
and the issue-to-PR process. Follow the [feature backlog](docs/development/backlog.md),
starting with authentication.

GitHub Actions builds the backend, runs both TUnit suites, checks and builds the
frontend, and runs Playwright. `Quality gate` succeeds only if all jobs succeed.
The maintainer closes the PR without merging and explicitly confirms it is
approved. Codex then reopens it, verifies required checks, merges it, closes the
linked issue, and safely deletes the source branch. A GitHub approving review is
not required. See [CONTRIBUTING.md](CONTRIBUTING.md) for the complete procedure
and [repository protections](docs/development/repository-protections.md) for
GitHub enforcement.
