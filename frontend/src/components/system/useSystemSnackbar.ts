import { useCallback, useState } from 'react';

type SnackbarKind = 'success' | 'error' | 'info';

interface SnackbarItem {
  id: number;
  message: string;
  kind: SnackbarKind;
}

let nextId = 0;

export function useSystemSnackbar() {
  const [items, setItems] = useState<SnackbarItem[]>([]);

  const dismiss = useCallback((id: number) => {
    setItems(prev => prev.filter(item => item.id !== id));
  }, []);

  const open = useCallback(
    (message: string, kind: SnackbarKind) => {
      const id = ++nextId;
      const duration = kind === 'error' ? 6000 : 3500;
      setItems(prev => [...prev, { id, message, kind }]);
      window.setTimeout(() => dismiss(id), duration);
    },
    [dismiss],
  );

  const success = useCallback((message: string) => open(message, 'success'), [open]);
  const error = useCallback((message: string) => open(message, 'error'), [open]);
  const info = useCallback((message: string) => open(message, 'info'), [open]);

  return { items, success, error, info, dismiss };
}
