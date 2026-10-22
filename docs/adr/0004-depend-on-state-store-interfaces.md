# Depend on interfaces for stateful stores

Application components resolve stateful stores through narrow interfaces, while
dependency injection owns their concrete singleton implementations. This keeps
HTTP and authentication code independent of the current in-memory storage
choice and permits focused substitutes without introducing a generic repository
or service layer; concrete stores remain available for direct business-rule
tests.
