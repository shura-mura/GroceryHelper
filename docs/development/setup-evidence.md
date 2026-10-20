# Setup validation evidence

These observations were collected while implementing issue #1.

| Seam | Before | After |
| --- | --- | --- |
| API readiness | `Test_host_exposes_a_readiness_probe` failed: expected HTTP 200, received 404. | Adding the readiness endpoint made the test pass with body `Healthy`. |
| Environment boundary | `Production_does_not_expose_the_setup_probe` failed: expected HTTP 404, received 200. | Restricting registration to Development/Testing made both integration tests pass. Each test owns a distinct host and they can run concurrently. |
| Browser/API wiring | `startup.spec.ts` failed because the status element did not exist. | The React scaffold calls the real backend probe and displays `Backend connected`; the test passed. |
| TUnit harness guard | Running the one-test unit suite with `--minimum-expected-tests 2` exited with code 9. | With the correct floor of 1, the runtime smoke test passed. This is harness validation, not a business TDD cycle. |

Reproduce focused tests using the commands in README. CI publishes backend
reports and Playwright diagnostics. Failures retain browser traces/screenshots
and the actual backend log.

The setup intentionally contains no business rules, authentication, persistence
adapter, or grocery UI. A nonempty smoke suite proves execution and wiring, not
coverage of future acceptance criteria.
