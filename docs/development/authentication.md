# Authentication design

Clarification of [issue #1](https://github.com/shura-mura/GroceryHelper/issues/1).
The consolidated scope was confirmed and published to issue #1 on 2026-10-20.

## Confirmed decisions

- Keep one predefined demo account, without registration or additional accounts.
- Backend restart restores the documented demo password and invalidates every
  session. Password changes last only until the next restart, consistent with
  [the in-memory demonstration decision](../adr/0001-isolated-in-memory-demonstration.md).
- Sessions expire eight hours after login. Activity does not extend that deadline.
- Provide explicit logout. Logout invalidates all sessions for the account,
  including sessions in other browsers.
- Password changes require the current password, invalidate all sessions, and
  return the user to login.
- Multiple simultaneous sessions are allowed. Logging in does not invalidate
  existing sessions.
- A browser discovers expiration or invalidation on its next API request. It
  then clears displayed grocery data and returns to login. Immediate
  cross-browser notifications are outside the demo scope.
- Passwords contain 8–128 characters, may include spaces, and require no
  particular character classes. Treat passwords exactly as entered, without
  trimming. The predefined demo password follows the same limits.
- Failed login displays "Invalid username or password" for either incorrect
  credential and leaves existing sessions unchanged.
- A password change with an incorrect current password, an invalid new password,
  or a new password identical to the current password is rejected with a
  specific validation message. The password and all sessions remain unchanged.
- Expiration or invalidation discards unsaved edits when the browser returns to
  login. The login screen explains that the session ended and unsaved edits were
  discarded. Do not replay changes automatically after login.
- Label logout "Log out everywhere". It takes effect immediately without a
  confirmation dialog.
- The predefined username is `demo`; the reset password is `GroceryDemo1!`.
  Document both in the README when authentication is implemented. Trim
  surrounding username whitespace and match usernames case-insensitively;
  password matching remains exact.
- Sessions survive browser closure, subject to their original eight-hour
  deadline and invalidation. There is no separate "Remember me" option.
- Login throttling and account lockout are outside this local demo's scope.
  Reconsider these protections before public deployment.
- Permit unauthenticated login and a CSRF-token bootstrap endpoint. The latter
  grants no access to account or grocery data. All other application API
  endpoints require authentication.
- Require CSRF validation for login, logout, password changes, and grocery
  mutations.
- Support HTTP on localhost with HttpOnly, SameSite cookies and CSRF protection.
  Require Secure cookies when served over HTTPS. Persist the session cookie
  only until the session's original eight-hour deadline.
- Logout with an expired or invalidated session clears local login state and
  returns to login without revoking other valid sessions. Revoking all sessions
  requires a currently authenticated session.

## Existing requirements retained

- Sessions are managed by the server.
- Store passwords as hashes.

## Review status

No product questions remain open. Issue #1 contains the agreed Authentication
scope. This document records design decisions; it does not claim authentication
has been implemented.
