import { useEffect, useMemo, useState } from 'react';
import { getUserData } from '../../auth';
import { AddButton, DialogButtons, FilterCard, IconAction, Modal, PageHeader, SearchField, SelectField, SortTh, sortRows, TableCard, TextField, type Sort } from '../../components/page';
import { useConfirm } from '../../components/page/useConfirm';
import SystemMultiSelect from '../../components/system/SystemMultiSelect';
import { useProjectContext } from '../../contexts/ProjectContext';
import { useYardSelection, type SelectedYard } from '../../contexts/YardSelectionContext';
import { createUser, deleteUser, listUsers, setUserActive, updateUser, type ManagedUser, type UserInput } from '../../services/users';
import { getYardTypes } from '../../services/yard';
import type { YardTypeOption } from '../../types/yard';

type SortKey = 'name' | 'email' | 'role' | 'status';

const EMPTY: UserInput = {
  first_name: '',
  last_name: '',
  email: '',
  phone: '',
  password: '',
  is_admin: false,
  is_active: true,
  yard_type_ids: [],
};

export default function Users(): React.JSX.Element {
  const { subProjectId, subProjects, setSubProject } = useProjectContext();
  const { replaceAssigned } = useYardSelection();
  const { confirm, confirmElement } = useConfirm();
  const [users, setUsers] = useState<ManagedUser[]>([]);
  const [yardTypes, setYardTypes] = useState<YardTypeOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [yardsLoading, setYardsLoading] = useState(false);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<Sort<SortKey>>({ field: 'name', dir: 'asc' });
  const [editing, setEditing] = useState<ManagedUser | null | undefined>(undefined);
  const [form, setForm] = useState<UserInput>(EMPTY);
  const [formError, setFormError] = useState('');
  const [busy, setBusy] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    if (subProjectId || subProjects.length === 0) return;
    const first = subProjects[0];
    setSubProject(String(first.id), first.name, 'Yard Management');
  }, [subProjectId, subProjects, setSubProject]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listUsers()
      .then(res => {
        if (cancelled) return;
        if (!res.success || !res.data) {
          setUsers([]);
          setError(res.message || 'Could not load users.');
        } else {
          setError('');
          setUsers(res.data);
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [reloadToken]);

  useEffect(() => {
    if (!subProjectId) return;
    let cancelled = false;
    setYardsLoading(true);
    getYardTypes()
      .then(res => {
        if (cancelled) return;
        setYardTypes(res.success && res.data ? [...res.data].sort((a, b) => a.yard_type_id - b.yard_type_id) : []);
      })
      .finally(() => { if (!cancelled) setYardsLoading(false); });
    return () => { cancelled = true; };
  }, [subProjectId]);

  const yardOptions = useMemo(
    () => yardTypes.map(yard => ({ id: yard.yard_type_id, name: yard.yard_type_name })),
    [yardTypes],
  );

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    const matched = users.filter(user => {
      if (!q) return true;
      const name = `${user.first_name} ${user.last_name}`.toLowerCase();
      const yards = user.yards.map(yard => yard.yard_type_name).join(' ').toLowerCase();
      return name.includes(q) || user.email.toLowerCase().includes(q) || yards.includes(q) || (user.phone ?? '').toLowerCase().includes(q);
    });
    return sortRows(matched, sort, (user, key) => {
      if (key === 'name') return `${user.first_name} ${user.last_name}`;
      if (key === 'email') return user.email;
      if (key === 'role') return user.is_admin ? 'Admin' : 'User';
      return user.is_active ? 'Active' : 'Inactive';
    });
  }, [users, search, sort]);

  const openCreate = () => {
    setEditing(null);
    setForm(EMPTY);
    setFormError('');
  };

  const openEdit = (user: ManagedUser) => {
    setEditing(user);
    setForm({
      first_name: user.first_name,
      last_name: user.last_name,
      email: user.email,
      phone: user.phone ?? '',
      password: '',
      is_admin: user.is_admin,
      is_active: user.is_active,
      yard_type_ids: user.yards.map(yard => yard.yard_type_id),
    });
    setFormError('');
  };

  const rememberSelf = (saved: ManagedUser) => {
    const data = getUserData();
    if (!data || Number(data.id) !== Number(saved.id)) return;
    data.first_name = saved.first_name;
    data.last_name = saved.last_name;
    data.email = saved.email;
    data.is_admin = saved.is_admin;
    data.is_super_admin = saved.is_admin;
    data.role_name = saved.is_admin ? 'Admin' : 'User';
    data.yards = saved.yards;
    if (!saved.is_admin) {
      data.permissions = ['YRDL', 'YRDD', 'YRDO', 'YRDQ', 'YRDU', 'YRDA', 'YRDH'].map(permission_code => ({ permission_code }));
    }
    sessionStorage.setItem('userData', JSON.stringify(data));
    replaceAssigned(saved.yards as SelectedYard[]);
  };

  const save = async () => {
    const first = form.first_name.trim();
    const last = form.last_name.trim();
    const email = form.email.trim();
    if (!first) { setFormError('A first name is required.'); return; }
    if (!last) { setFormError('A last name is required.'); return; }
    if (!email.includes('@') || email.includes(' ')) { setFormError('A valid email is required.'); return; }
    if (!editing && form.password.trim().length < 3) { setFormError('Password must be at least 3 characters.'); return; }
    if (editing && form.password.trim().length > 0 && form.password.trim().length < 3) {
      setFormError('Password must be at least 3 characters.');
      return;
    }
    if (form.yard_type_ids.length === 0) { setFormError('Choose at least one yard.'); return; }

    setBusy(true);
    setFormError('');
    const input: UserInput = { ...form, first_name: first, last_name: last, email, phone: form.phone.trim() };
    const res = editing ? await updateUser(editing.id, input) : await createUser(input);
    setBusy(false);
    if (!res.success || !res.data) {
      setFormError(res.message || 'Could not save the user.');
      return;
    }
    rememberSelf(res.data);
    setEditing(undefined);
    setReloadToken(n => n + 1);
  };

  const toggleActive = async (user: ManagedUser) => {
    const next = !user.is_active;
    const ok = await confirm({
      title: next ? 'Activate user' : 'Deactivate user',
      message: next
        ? `${user.first_name} ${user.last_name} will be able to sign in again.`
        : `${user.first_name} ${user.last_name} will not be able to sign in.`,
      confirmLabel: next ? 'Activate' : 'Deactivate',
      danger: !next,
    });
    if (!ok) return;
    const res = await setUserActive(user.id, next);
    if (!res.success) {
      setError(res.message || 'Could not update the user.');
      return;
    }
    setReloadToken(n => n + 1);
  };

  const remove = async (user: ManagedUser) => {
    if (user.is_admin) return;
    const ok = await confirm({
      title: 'Delete user',
      message: `Delete ${user.first_name} ${user.last_name}? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!ok) return;
    const res = await deleteUser(user.id);
    if (!res.success) {
      setError(res.message || 'Could not delete the user.');
      return;
    }
    setReloadToken(n => n + 1);
  };

  const selfId = Number(getUserData()?.id);
  const dialogOpen = editing !== undefined;

  return (
    <>
      <PageHeader
        title="Users"
        onReload={() => setReloadToken(n => n + 1)}
        actions={<AddButton label="Add user" onClick={openCreate} />}
      />
      <FilterCard onReset={search ? () => setSearch('') : undefined}>
        <SearchField label="Search" value={search} onChange={setSearch} placeholder="Name, email, or yard" />
      </FilterCard>
      {error && <p className="pg-hint pg-hint--error">{error}</p>}
      <TableCard
        loading={loading}
        hasRows={visible.length > 0}
        emptyIcon="group"
        emptyTitle={search ? 'No users match' : 'No users yet'}
        emptyMessage={search ? 'Try a different name, email, or yard.' : 'Add a user and assign the yards they can open.'}
      >
        <table className="data-table">
          <thead>
            <tr>
              <SortTh label="Name" field="name" sort={sort} onSort={setSort} />
              <SortTh label="Email" field="email" sort={sort} onSort={setSort} />
              <th>Phone</th>
              <SortTh label="Role" field="role" sort={sort} onSort={setSort} />
              <th>Yards</th>
              <SortTh label="Status" field="status" sort={sort} onSort={setSort} />
              <th />
            </tr>
          </thead>
          <tbody>
            {visible.map(user => {
              const mine = Number(user.id) === selfId;
              return (
                <tr key={user.id}>
                  <td>{user.first_name} {user.last_name}</td>
                  <td>{user.email}</td>
                  <td>{user.phone || '—'}</td>
                  <td>{user.is_admin ? 'Admin' : 'User'}</td>
                  <td title={user.yards.map(yard => yard.yard_type_name).join(', ')}>
                    {user.yards.length ? user.yards.map(yard => yard.yard_type_name).join(', ') : '—'}
                  </td>
                  <td>{user.is_active ? 'Active' : 'Inactive'}</td>
                  <td>
                    <div className="row-actions" style={{ opacity: 1 }}>
                      <IconAction icon="edit" title="Edit user" onClick={() => openEdit(user)} />
                      <IconAction
                        icon={user.is_active ? 'person_off' : 'person_add'}
                        title={user.is_active ? 'Deactivate user' : 'Activate user'}
                        variant={user.is_active ? 'warn' : 'primary'}
                        disabled={mine && user.is_active}
                        disabledReason="You cannot deactivate your own account."
                        onClick={() => toggleActive(user)}
                      />
                      <IconAction
                        icon="delete"
                        title="Delete user"
                        variant="danger"
                        disabled={user.is_admin}
                        disabledReason="An administrator cannot be deleted."
                        onClick={() => remove(user)}
                      />
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </TableCard>

      {dialogOpen && (
        <Modal
          title={editing ? 'Edit user' : 'Add user'}
          icon={editing ? 'edit' : 'person_add'}
          size="md"
          footer={(
            <DialogButtons
              onCancel={() => { if (!busy) setEditing(undefined); }}
              onSave={save}
              saveLabel={editing ? 'Save' : 'Add user'}
              busy={busy}
            />
          )}
        >
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px 16px' }}>
            <TextField label="First name *" value={form.first_name} onChange={first_name => setForm(f => ({ ...f, first_name }))} />
            <TextField label="Last name *" value={form.last_name} onChange={last_name => setForm(f => ({ ...f, last_name }))} />
            <TextField label="Email *" type="email" value={form.email} onChange={email => setForm(f => ({ ...f, email }))} />
            <TextField label="Phone" value={form.phone} onChange={phone => setForm(f => ({ ...f, phone }))} />
            <TextField
              label={editing ? 'New password' : 'Password *'}
              type="password"
              autoComplete="new-password"
              value={form.password}
              onChange={password => setForm(f => ({ ...f, password }))}
              hint={editing ? <span className="pg-hint">Leave blank to keep the current password.</span> : undefined}
            />
            <SelectField
              label="Role *"
              value={form.is_admin ? 'admin' : 'user'}
              options={[{ value: 'user', label: 'User' }, { value: 'admin', label: 'Admin' }]}
              onChange={value => setForm(f => ({ ...f, is_admin: value === 'admin' }))}
            />
            <div style={{ gridColumn: '1 / -1' }}>
              <SystemMultiSelect
                label="Yards *"
                options={yardOptions}
                selectedIds={form.yard_type_ids}
                onChange={ids => setForm(f => ({ ...f, yard_type_ids: ids.map(Number) }))}
                loading={yardsLoading}
                placeholder="Choose yards"
                searchable
              />
            </div>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, gridColumn: '1 / -1' }}>
              <input
                type="checkbox"
                checked={form.is_active}
                onChange={e => setForm(f => ({ ...f, is_active: e.target.checked }))}
              />
              Active — inactive people cannot sign in
            </label>
          </div>
          {formError && <p className="pg-hint pg-hint--error">{formError}</p>}
        </Modal>
      )}
      {confirmElement}
    </>
  );
}
