export interface Session {
  username: string;
}

interface CsrfResponse {
  token: string;
}

interface ProblemResponse {
  detail?: string;
}

export type SignInResult =
  | { authenticated: true; session: Session }
  | { authenticated: false; message: string };

export async function initializeAuthentication(signal: AbortSignal): Promise<{
  token: string;
  session?: Session;
}> {
  const csrfResponse = await fetch('/api/auth/csrf', { signal });
  if (!csrfResponse.ok) throw new Error('CSRF bootstrap failed');
  const csrf = await csrfResponse.json() as CsrfResponse;
  return { token: csrf.token, session: await getSession(signal) };
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
  return { authenticated: true, session };
}

async function getSession(signal?: AbortSignal): Promise<Session | undefined> {
  const response = await fetch('/api/auth/session', { signal });
  if (response.status === 401) return undefined;
  if (!response.ok) throw new Error('Session lookup failed');
  return await response.json() as Session;
}
