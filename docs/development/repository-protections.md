# Repository protections

The repository is public and the configured account has repository administrator
permissions. The intended `main` ruleset requires:

- Pull requests with at least one approving review.
- Dismissal of stale approvals after new commits.
- Resolved review conversations.
- The `Quality gate` check, with the branch up to date.
- No deletion or non-fast-forward updates.
- No bypass actors.

Ruleset `Protect main` (ID `25455840`) was installed and read back as active
during setup. The required check is bound to the GitHub Actions integration.
Repository settings are external state; recheck them when diagnosing a merge
block rather than assuming this recorded state remains current.

GitHub rejects self-approval. With both maintainer and PR author using
`shura-mura`, required approval cannot be satisfied. The setup PR remains draft
until a separate author/reviewer identity is arranged by the maintainer.
Do not grant an approval bypass to solve this.

GitHub required-review rules cannot verify an instruction in this chat.
The agent must additionally wait for the maintainer's explicit merge instruction.
Changes to repository rules require the same maintainer authorization.
