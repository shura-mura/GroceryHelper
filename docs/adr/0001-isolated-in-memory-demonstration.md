# Isolated in-memory demonstration

GroceryHelper demonstrates TDD and application design, rather than durable data
storage. The agreed application will keep its account and grocery data in memory:
browser refresh preserves data, while backend restart restores deterministic
demo defaults, including the demo password. A database would add migration and
operational work without advancing this assessment; introduce durable storage
only through a later decision.

Integration tests therefore own an application host, and each browser test owns
an entire backend process. Isolating only browser contexts would leave tests
sharing the single account and mutable data, making parallel password and list
tests unreliable. This costs additional process startup time in exchange for
deterministic tests without reset endpoints.
