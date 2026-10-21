# Use RFC 9457 Problem Details for API errors

Application-generated 4xx and 5xx responses under `/api` use RFC 9457 Problem Details so clients have one safe error contract and can correlate it to the W3C server trace; health checks and responses that cannot contain a body remain outside this contract. Generic failures use `about:blank`, while validation failures use the stable `urn:grocery-helper:problem:validation-error` type and an `errors` object whose keys are machine-addressable JSON pointers. Every problem contains `type`, `title`, `status`, and `traceId`; `detail` is included only when safe and actionable, and `instance` only when a genuine occurrence URI exists.

## Consequences

The API deliberately returns `application/problem+json` even when a client's `Accept` header excludes JSON, prioritizing one machine-readable failure contract over content negotiation for errors. Enforcing the contract for body-bearing failures requires an API-scoped custom writer and buffers `/api` response bodies before they are sent; that memory cost is accepted for this small demonstration, while streaming would require a future endpoint-level error discipline instead. Restricting the middleware and writer to `/api` keeps health checks and other non-API responses on their existing behavior.
