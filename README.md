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
