# Separate observability signals

GroceryHelper will treat structured logs, distributed traces, and metrics as
distinct signals rather than representing all telemetry as Serilog events.
Application code will use the .NET logging, activity, and meter abstractions;
Serilog will route structured logs, while OpenTelemetry will instrument and
export traces and metrics. This keeps business code independent of telemetry
vendors and preserves each signal's semantics.

The demo will write structured logs to the console and rolling local files, and
will expose traces and metrics through development console exporters. Browser
telemetry will propagate W3C trace context and remain in browser diagnostics
until a central collector is selected. Production destinations such as Seq,
Azure Application Insights, or an OTLP-compatible collector are configuration
choices, not dependencies of the demonstration.
