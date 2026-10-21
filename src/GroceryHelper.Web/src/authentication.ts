export interface Session {
  username: string;
}

interface CsrfResponse {
  token: string;
}

interface ProblemResponse {
  detail?: string;
}

const sessionMarker = 'GroceryHelper.SessionKnown';

export function clearKnownSession(): void {
  localStorage.removeItem(sessionMarker);
}

export type SignInResult =
  | { authenticated: true; session: Session }
  | { authenticated: false; message: string };

export async function initializeAuthentication(signal: AbortSignal): Promise<{
  token: string;
  session?: Session;
  sessionEnded: boolean;
}> {
  const token = await getCsrfToken(signal);
  const session = await getSession(signal);
  const sessionEnded = !session && localStorage.getItem(sessionMarker) === 'true';
  if (session) {
    localStorage.setItem(sessionMarker, 'true');
  } else {
    localStorage.removeItem(sessionMarker);
  }

  return { token, session, sessionEnded };
}

export async function signIn(
  username: string,
  password: string,
  csrfToken: string,
): Promise<SignInResult> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': csrfToken,
    },
    body: JSON.stringify({ username, password }),
  });
  if (!response.ok) {
    const problem = await response.json() as ProblemResponse;
    return {
      authenticated: false,
      message: problem.detail ?? 'Unable to sign in',
    };
  }

  const session = await getSession();
  if (!session) throw new Error('Session lookup failed');
  localStorage.setItem(sessionMarker, 'true');
  return { authenticated: true, session };
}

export async function logOutEverywhere(): Promise<void> {
  const csrfToken = await getCsrfToken();
  const response = await fetch('/api/auth/logout', {
    method: 'POST',
    headers: { 'X-CSRF-TOKEN': csrfToken },
  });
  if (!response.ok) throw new Error('Logout failed');
  clearKnownSession();
}

export async function getCsrfToken(signal?: AbortSignal): Promise<string> {
  const response = await fetch('/api/auth/csrf', { signal });
  if (!response.ok) throw new Error('CSRF bootstrap failed');
  const csrf = await response.json() as CsrfResponse;
  return csrf.token;
}

async function getSession(signal?: AbortSignal): Promise<Session | undefined> {
  const response = await fetch('/api/auth/session', { signal });
  if (response.status === 401) return undefined;
  if (!response.ok) throw new Error('Session lookup failed');
  return await response.json() as Session;
}
