# Use RFC 9457 for API errors

Every application-generated 4xx or 5xx response under `/api` will use RFC 9457
Problem Details with the `application/problem+json` media type. Generic HTTP
failures will use `about:blank`; application-specific failures will introduce
stable, documented problem types only when needed. Central enforcement keeps
the public contract consistent across validation, authentication, routing, and
unhandled failures.

Problem responses will preserve the HTTP status and required response headers,
and will include a `traceId` extension that correlates with server telemetry.
Client-correctable failures may expose safe structured details, while unexpected
failures remain generic and disclose no exception or sensitive data. `/health`
is an operational probe rather than an application API and is outside this
contract.
