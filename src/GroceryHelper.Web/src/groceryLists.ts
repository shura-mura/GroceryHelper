import { getCsrfToken } from './authentication';

export interface GroceryList {
  id: string;
  name: string;
}

interface ValidationProblem {
  errors?: Record<string, string[]>;
  existingListId?: string;
}

export class SessionEndedError extends Error {}

export class ListValidationError extends Error {}

export class DuplicateListError extends ListValidationError {
  constructor(message: string, public readonly existingListId: string) {
    super(message);
  }
}

export async function getGroceryLists(signal?: AbortSignal): Promise<GroceryList[]> {
  const response = await fetch('/api/lists', { signal });
  if (response.status === 401) throw new SessionEndedError();
  if (!response.ok) throw new Error('List lookup failed');
  return await response.json() as GroceryList[];
}

export async function createGroceryList(
  name: string,
): Promise<GroceryList> {
  const csrfToken = await getCsrfToken();
  const response = await fetch('/api/lists', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': csrfToken,
    },
    body: JSON.stringify({ name }),
  });
  if (response.status === 401) throw new SessionEndedError();
  if (response.status === 400) {
    const problem = await response.json() as ValidationProblem;
    const message = problem.errors?.['/name']?.[0] ?? 'Unable to create the grocery list';
    if (problem.existingListId) {
      throw new DuplicateListError(message, problem.existingListId);
    }
    throw new ListValidationError(message);
  }
  if (!response.ok) throw new Error('List creation failed');
  return await response.json() as GroceryList;
}
