import { type FormEvent, type KeyboardEvent, useEffect, useRef, useState } from 'react';
import {
  createGroceryList as requestCreateGroceryList,
  DuplicateListError,
  getGroceryLists,
  type GroceryList,
  ListValidationError,
  SessionEndedError,
} from './groceryLists';

interface GroceryListsProps {
  onSessionEnded: () => void;
}

type ListOrder = 'ascending' | 'descending';

const listOrderStorageKey = 'grocery-helper:list-order';
const listNameCollator = new Intl.Collator(undefined, {
  sensitivity: 'base',
  numeric: true,
});

function loadListOrder(): ListOrder {
  return localStorage.getItem(listOrderStorageKey) === 'descending'
    ? 'descending'
    : 'ascending';
}

function orderLists(lists: GroceryList[], order: ListOrder) {
  return [...lists].sort((left, right) => {
    const comparison = listNameCollator.compare(left.name, right.name);
    return order === 'ascending' ? comparison : -comparison;
  });
}

export function GroceryListView({ onSessionEnded }: GroceryListsProps) {
  const [lists, setLists] = useState<GroceryList[]>();
  const [selectedListId, setSelectedListId] = useState<string>();
  const [listOrder, setListOrder] = useState<ListOrder>(loadListOrder);
  const initialListOrder = useRef(listOrder).current;
  const [creatingList, setCreatingList] = useState(false);
  const [listName, setListName] = useState('');
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const selectedListElement = useRef<HTMLButtonElement>(null);
  const focusSelectedList = useRef(false);
  const createButton = useRef<HTMLButtonElement>(null);
  const duplicateDialog = useRef<HTMLDialogElement>(null);
  const [duplicate, setDuplicate] = useState<{ id: string; name: string }>();

  useEffect(() => {
    const controller = new AbortController();

    void getGroceryLists(controller.signal)
      .then((loadedLists) => {
        setLists(loadedLists);
        setSelectedListId(orderLists(loadedLists, initialListOrder)[0]?.id);
      })
      .catch((caught: unknown) => {
        if (controller.signal.aborted) return;
        if (caught instanceof SessionEndedError) onSessionEnded();
        else setError('Unable to load grocery lists');
      });

    return () => controller.abort();
  }, [initialListOrder, onSessionEnded]);

  useEffect(() => {
    selectedListElement.current?.scrollIntoView({ block: 'nearest' });
    if (focusSelectedList.current) {
      selectedListElement.current?.focus();
      focusSelectedList.current = false;
    }
  }, [lists, selectedListId]);

  useEffect(() => {
    if (duplicate && !duplicateDialog.current?.open) {
      duplicateDialog.current?.showModal();
    }
  }, [duplicate]);

  function cancelCreation() {
    setCreatingList(false);
    setListName('');
    setError(undefined);
  }

  async function navigateToDuplicate() {
    if (!duplicate) return;

    const targetId = duplicate.id;
    duplicateDialog.current?.close();
    createButton.current?.focus();
    setDuplicate(undefined);
    setCreatingList(false);
    setListName('');
    setError(undefined);
    setNotice(undefined);
    setSubmitting(true);
    try {
      const loadedLists = await getGroceryLists();
      setLists(loadedLists);
      const target = loadedLists.find((list) => list.id === targetId);
      focusSelectedList.current = true;
      if (target) {
        setSelectedListId(target.id);
      } else {
        setSelectedListId(orderLists(loadedLists, listOrder)[0]?.id);
        setNotice('That grocery list is no longer available.');
      }
    } catch (caught) {
      if (caught instanceof SessionEndedError) onSessionEnded();
      else setError('Unable to load grocery lists');
    } finally {
      setSubmitting(false);
    }
  }

  function handleCreationKey(event: KeyboardEvent<HTMLFormElement>) {
    if (event.key === 'Escape' && !duplicate) {
      event.preventDefault();
      cancelCreation();
    }
  }

  function closeDuplicateDialog() {
    duplicateDialog.current?.close();
    setDuplicate(undefined);
    requestAnimationFrame(() => createButton.current?.focus());
  }

  function trapDuplicateDialogFocus(event: KeyboardEvent<HTMLDialogElement>) {
    if (event.key !== 'Tab') return;

    const controls = [...(duplicateDialog.current?.querySelectorAll<HTMLElement>('button') ?? [])];
    const first = controls[0];
    const last = controls.at(-1);
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }

  async function createList(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(undefined);
    try {
      const created = await requestCreateGroceryList(listName);
      setLists((existingLists) => [...(existingLists ?? []), created]);
      setSelectedListId(created.id);
      setCreatingList(false);
      setListName('');
    } catch (caught) {
      if (caught instanceof SessionEndedError) {
        onSessionEnded();
      } else if (caught instanceof DuplicateListError) {
        setDuplicate({
          id: caught.existingListId,
          name: caught.existingListName ?? listName.trim().replace(/[ \t]+/g, ' '),
        });
      } else if (caught instanceof ListValidationError) {
        setError(caught.message);
      } else {
        setError('Unable to create the grocery list');
      }
    } finally {
      setSubmitting(false);
    }
  }

  function changeListOrder(order: ListOrder) {
    localStorage.setItem(listOrderStorageKey, order);
    setListOrder(order);
  }

  const orderedLists = lists ? orderLists(lists, listOrder) : undefined;

  return (
    <section aria-labelledby="grocery-lists-heading">
      <h2 id="grocery-lists-heading">Grocery lists</h2>
      {!lists && !error && <p>Loading grocery lists</p>}
      {lists?.length === 0 && <p>No grocery lists yet.</p>}
      {lists && lists.length > 0 && (
        <label>
          List order
          <select
            value={listOrder}
            onChange={(event) => changeListOrder(event.target.value as ListOrder)}
          >
            <option value="ascending">A–Z</option>
            <option value="descending">Z–A</option>
          </select>
        </label>
      )}
      {orderedLists && orderedLists.length > 0 && (
        <ul>
          {orderedLists.map((list) => {
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
      {lists && !creatingList && (
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
          <button ref={createButton} disabled={submitting} type="submit">Create</button>
          <button disabled={submitting} type="button" onClick={cancelCreation}>
            Cancel
          </button>
          {error && <p role="alert">{error}</p>}
          {duplicate && (
            <dialog
              ref={duplicateDialog}
              aria-labelledby="duplicate-list-heading"
              onKeyDown={trapDuplicateDialogFocus}
              onCancel={(event) => {
                event.preventDefault();
                event.stopPropagation();
                closeDuplicateDialog();
              }}
            >
              <h3 id="duplicate-list-heading">Grocery list already exists</h3>
              <p>A grocery list named &quot;{duplicate.name}&quot; already exists.</p>
              <button autoFocus type="button" onClick={closeDuplicateDialog}>OK</button>
              <button type="button" onClick={() => { void navigateToDuplicate(); }}>
                Navigate
              </button>
            </dialog>
          )}
        </form>
      )}
      {!creatingList && error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
    </section>
  );
}
