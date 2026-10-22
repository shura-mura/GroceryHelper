import { type FormEvent, type KeyboardEvent, useEffect, useRef, useState } from 'react';
import {
  createGroceryList as requestCreateGroceryList,
  getGroceryLists,
  type GroceryList,
  ListValidationError,
  SessionEndedError,
} from './groceryLists';

interface GroceryListsProps {
  onSessionEnded: () => void;
}

export function GroceryListView({ onSessionEnded }: GroceryListsProps) {
  const [lists, setLists] = useState<GroceryList[]>();
  const [selectedListId, setSelectedListId] = useState<string>();
  const [creatingList, setCreatingList] = useState(false);
  const [listName, setListName] = useState('');
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const selectedListElement = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const controller = new AbortController();

    void getGroceryLists(controller.signal)
      .then((loadedLists) => {
        setLists(loadedLists);
        setSelectedListId(loadedLists[0]?.id);
      })
      .catch((caught: unknown) => {
        if (controller.signal.aborted) return;
        if (caught instanceof SessionEndedError) onSessionEnded();
        else setError('Unable to load grocery lists');
      });

    return () => controller.abort();
  }, [onSessionEnded]);

  useEffect(() => {
    selectedListElement.current?.scrollIntoView({ block: 'nearest' });
  }, [selectedListId]);

  function cancelCreation() {
    setCreatingList(false);
    setListName('');
    setError(undefined);
  }

  function handleCreationKey(event: KeyboardEvent<HTMLFormElement>) {
    if (event.key === 'Escape') {
      event.preventDefault();
      cancelCreation();
    }
  }

  async function createList(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(undefined);
    try {
      const created = await requestCreateGroceryList(listName);
      setLists([created]);
      setSelectedListId(created.id);
      setCreatingList(false);
      setListName('');
    } catch (caught) {
      if (caught instanceof SessionEndedError) {
        onSessionEnded();
      } else if (caught instanceof ListValidationError) {
        setError(caught.message);
      } else {
        setError('Unable to create the grocery list');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section aria-labelledby="grocery-lists-heading">
      <h2 id="grocery-lists-heading">Grocery lists</h2>
      {!lists && !error && <p>Loading grocery lists</p>}
      {lists?.length === 0 && <p>No grocery lists yet.</p>}
      {lists && lists.length > 0 && (
        <ul>
          {lists.map((list) => {
            const selected = list.id === selectedListId;
            return (
              <li key={list.id}>
                <button
                  aria-current={selected ? 'true' : undefined}
                  ref={selected ? selectedListElement : undefined}
                  type="button"
                  onClick={() => setSelectedListId(list.id)}
                >
                  {list.name}
                </button>
              </li>
            );
          })}
        </ul>
      )}
      {lists?.length === 0 && !creatingList && (
        <button type="button" onClick={() => setCreatingList(true)}>
          Create a grocery list
        </button>
      )}
      {creatingList && (
        <form
          onKeyDown={handleCreationKey}
          onSubmit={(event) => { void createList(event); }}
        >
          <label>
            List name
            <input
              autoFocus
              value={listName}
              onChange={(event) => setListName(event.target.value)}
            />
          </label>
          <button disabled={submitting} type="submit">Create</button>
          <button disabled={submitting} type="button" onClick={cancelCreation}>
            Cancel
          </button>
          {error && <p role="alert">{error}</p>}
        </form>
      )}
      {!creatingList && error && <p role="alert">{error}</p>}
    </section>
  );
}
