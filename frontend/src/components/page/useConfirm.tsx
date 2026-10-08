// Ask before a delete or another hard-to-undo action, with the platform's SystemConfirmDialog, and get
// the answer back:  const ok = await confirm({ title: 'Delete Item', message: 'Delete X? This cannot be
// undone.', confirmLabel: 'Delete', danger: true });  Render {confirmElement} once in the page. Saves do
// not ask (the platform's rule); the button names the action, never Yes / No.
import { useCallback, useState, type ReactNode } from 'react';
import SystemConfirmDialog from '../system/SystemConfirmDialog';

export interface ConfirmOptions {
  title: string;
  message: ReactNode;
  confirmLabel: string;
  danger?: boolean;
}

export function useConfirm() {
  const [pending, setPending] = useState<{ options: ConfirmOptions; resolve: (ok: boolean) => void } | null>(null);

  const confirm = useCallback((options: ConfirmOptions) => new Promise<boolean>(resolve => setPending({ options, resolve })), []);

  const answer = (ok: boolean) => {
    pending?.resolve(ok);
    setPending(null);
  };

  const confirmElement = pending && (
    <SystemConfirmDialog title={pending.options.title} message={pending.options.message} confirmLabel={pending.options.confirmLabel}
                         danger={pending.options.danger} onConfirm={() => answer(true)} onCancel={() => answer(false)} />
  );

  return { confirm, confirmElement };
}
