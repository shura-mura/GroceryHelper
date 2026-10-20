# Development workflow

## Issue to merge

1. Read `AGENTS.md` and the originating GitHub issue. Confirm acceptance criteria
   and public test seams before implementation.
2. Branch from current `main`, using a short-lived branch such as
   `feature/2-authentication`. Deliver one small vertical slice per issue and PR.
3. Follow red-green-refactor: write one behavior test, verify the expected failure,
   implement enough to pass, then improve the design with tests green. A compiler
   or environment error is not evidence of a failing behavior test.
4. Run relevant individual tests and type checking regularly. Run the full
   [validation commands](README.md#validation) before requesting review.
5. Review both standards and the originating spec. Resolve findings before
   requesting the maintainer's review.
6. Open a PR using the template. Record actual red/green evidence, design choices,
   limitations, and the issue it closes. Do not invent evidence for setup files
   that have no behavior to test.
7. Wait for both signals: the maintainer closes the PR without merging, and
   explicitly confirms that the PR is approved. Check the PR's closed state;
   a GitHub approving review is not required. Closure alone is not approval.
8. Once both signals are present, reopen the same PR, mark it ready if necessary,
   and verify required checks and repository rules for the current head. Merge
   through the PR. The maintainer's explicit approval authorizes this sequence;
   no additional merge confirmation is needed. Never enable automatic merging.
9. Verify that GitHub reports the PR as merged before closing the linked issue
   as completed (if GitHub has not already closed it). Then delete the PR's
   source branch on GitHub. Delete the local source branch only after switching
   to updated `main` and confirming no uncommitted or unmerged work would be lost.

Approval applies to the reviewed changes. If changes are added after approval,
obtain approval for the updated PR before merging. Do not close the PR yourself
to manufacture the maintainer's closure signal. A PR already marked merged
needs only completion verification and safe cleanup, not another merge.

`Quality gate` requires backend build/tests, frontend type checking/lint/build,
and Playwright to succeed. Keep its name stable: repository rules refer to it.
The bootstrap commit was the sole direct-to-main exception.

## Review accounts

The author and maintainer may use the same GitHub account. A separate reviewer
account is not required, and PRs need not remain draft for that reason.
See [repository protections](docs/development/repository-protections.md).

## Design and code standards

- Organize behavior around authentication, lists, and items as they arrive.
- Keep business rules independent of HTTP and storage details. Introduce a
  boundary when tests or a concrete alternative implementation justify it.
- Favor cohesive modules and plain types; avoid speculative repositories,
  generic service layers, CQRS, or factories without a demonstrated need.
- Validate externally supplied data at its public boundary; test observable
  behavior rather than private methods or internal call sequences.
- Use nullable reference types and TypeScript strict mode. Fix warnings rather
  than suppressing them globally.
- Keep credentials and machine-specific configuration out of source control.
- Capture consequential tradeoffs in `docs/adr/`; read relevant ADRs before changes.

## Tests and isolation

- Every backend unit and integration test must have explicit `// Arrange`,
  `// Act`, and `// Assert` sections, in that order. Put setup, the behavior
  under test, and outcome checks in their respective sections.
- **TUnit unit:** business rules through public interfaces, without HTTP. The
  initial runtime smoke test validates harness execution only; it is not domain
  coverage. Add meaningful tests as domain behavior is introduced.
- **TUnit integration:** a fresh `WebApplicationFactory` per test. Exercise HTTP
  behavior and the real application composition. Tests may run concurrently.
- **Playwright:** each test starts its own backend process and Vite server on
  OS-assigned ports, with its own Vite cache and browser context. The backend is real, never an
  API mock. Processes stop in fixture teardown; developer servers are never reused.
  This also isolates the future single account, password changes, and in-memory data.
- Keep data test-owned and deterministic. New persistence or shared external
  services must preserve isolation before tests can use them.
- No public test-reset endpoints or shared administrator credentials.
- Use role/name locators and web-first assertions. No fixed sleeps or retries
  that conceal flaky behavior. CI retains traces, screenshots, and server logs
  for failures.
- Both TUnit suites require at least one executed test. Playwright fails on zero
  discovered tests and rejects committed `test.only` in CI.

## Dependency changes

Tool versions live in `global.json`, `.node-version`, and the frontend
`package.json`. Commit NuGet `packages.lock.json` files and npm `package-lock.json`.
Use `dotnet restore --locked-mode` and `npm ci` to reproduce dependencies.
Use the latest stable .NET 10 SDK: locally `global.json` selects the latest
installed 10.0 feature band/patch; CI installs `10.0.x`. Preview SDKs and .NET 11
are excluded. Keep the local SDK updated; roll-forward selects installed SDKs
and does not download them. If an SDK update changes the restore graph, review
and regenerate the lockfiles rather than disabling locked restore in CI.
Update other pins and locks together in a reviewed PR; CI actions are pinned to commits.
