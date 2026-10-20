# GroceryHelper

A client/server demonstration project for grocery-list management, focused on
test-driven development, code quality, and justified design decisions.

The planned stack is ASP.NET Core/C# with TUnit and React/TypeScript with
Playwright. The application and development infrastructure are not implemented
yet; they will be delivered through reviewed pull requests.

## Planned functionality

- One predefined account with authentication and password changes.
- Multiple grocery lists with grocery-item CRUD, quantities, and purchased status.
- Reusable category tags, with name search and category filtering within a list.
- In-memory storage that resets when the backend restarts.

## Repository status

This initial commit establishes the repository and the approved agent
configuration. Solution scaffolding, test infrastructure, CI, and development
documentation will be implemented in a separate session through a GitHub issue
and pull request.

After this bootstrap, changes reach `main` only after manual GitHub PR approval
by the maintainer and an explicit instruction to merge. Passing checks alone does
not authorize a merge.
