# Repository protections

The repository is public and the configured account has repository administrator
permissions. The `main` ruleset requires:

- Pull requests with zero required approving reviews.
- Resolved review conversations.
- The `Quality gate` check, with the branch up to date.
- No deletion or non-fast-forward updates.
- No bypass actors.

Ruleset `Protect main` (ID `25455840`) was read back as active on 2026-10-21,
after the maintainer changed the required approval count from one to zero.
The required check is bound to the GitHub Actions integration.
Repository settings are external state; recheck them when diagnosing a merge
block rather than assuming this recorded state remains current.

The author and maintainer may use the same account. A separate reviewer identity
is no longer a prerequisite for merging.

GitHub rules do not enforce this project's closure-and-confirmation process.
Before merging, the agent must observe maintainer closure and explicit PR
approval, then follow the reopening, check verification, merge, issue completion,
and branch cleanup procedure in `CONTRIBUTING.md`.
Changes to repository rules require the same maintainer authorization.
