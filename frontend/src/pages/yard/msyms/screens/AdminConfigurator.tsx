import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { Trash2, Plus, Edit2, Check, X, ChevronDown, ChevronRight, Save, Shield, Layers, Database, ScrollText, FlaskConical, Ruler, Package, ExternalLink } from 'lucide-react';
import { symsFetch, symsError } from '../symsApi';
import { hasPermission } from '../../../../auth';
import { useYardMaterial } from '../../components/MaterialGate';
import { FORMATION, formationsFor, type StorageStyle } from '../../formations';

// ─── Types ───────────────────────────────────────────────────────────────
type Tab = 'zones' | 'master_data' | 'erp_modules' | 'activity_log';

// ─── Sub-component: Toggle Switch ────────────────────────────────────────
const Toggle: React.FC<{ value: boolean; onChange: (v: boolean) => void; color?: string }> = ({ value, onChange, color = 'bg-accent' }) => (
    <button
        onClick={() => onChange(!value)}
        className={`relative inline-flex h-[28px] w-[56px] shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 focus:outline-none ${value ? color : 'bg-[#2a2a2a]'}`}
    >
        <span className={`pointer-events-none inline-block h-[24px] w-[24px] transform rounded-full bg-white shadow ring-0 transition duration-200 ${value ? 'translate-x-[28px]' : 'translate-x-0'}`} />
    </button>
);

// ─── Sub-component: Section Header ───────────────────────────────────────
const SectionHeader: React.FC<{ icon: React.ReactNode; title: string; sub?: string }> = ({ icon, title, sub }) => (
    <div className="flex items-center mb-6 pb-4 border-b border-[#222]">
        <div className="text-accent mr-3">{icon}</div>
        <div>
            <h3 className="text-lg font-black uppercase tracking-widest text-white">{title}</h3>
            {sub && <p className="text-xs text-secondary mt-0.5">{sub}</p>}
        </div>
    </div>
);

// ─── Sub-component: Tag Pill ──────────────────────────────────────────────
const Tag: React.FC<{ label: string; onRemove?: () => void; color?: string }> = ({ label, onRemove, color = 'bg-accent/20 text-accent border-accent/30' }) => (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider border ${color}`}>
        {label}
        {onRemove && <button onClick={onRemove} className="ml-1.5 hover:text-danger"><X size={10} /></button>}
    </span>
);

// ─── Grade Master Panel ───────────────────────────────────────────────────
//
// Read-only, deliberately.
//
// Grades are the platform's own master data — gm_grade, shared by recipes, product mix
// routing and the mill. Slab Yard had its own create/edit/delete form for them, posting to
// endpoints that never existed here, and had it worked it would have been a second place to
// define the same thing. So the list stays, because routing rules are read against it and
// somebody configuring a yard needs to see what is there, and editing goes where grades
// actually live.
const GradeMasterPanel: React.FC<{ token: string | null }> = ({ token }) => {
    const [grades, setGrades] = useState<any[]>([]);

    const fetch_ = useCallback(async () => {
        const r = await symsFetch('http://localhost:4000/api/masters/grades', { headers: { Authorization: `Bearer ${token}` } });
        if (r.ok) setGrades(await r.json());
    }, [token]);

    useEffect(() => { fetch_(); }, [fetch_]);

    return (
        <div>
            <SectionHeader icon={<FlaskConical size={20} />} title="Grade Master"
                sub="The steel grades this facility recognises. Shared with recipes and mill scheduling, so they are maintained once for the whole platform." />

            <div className="overflow-x-auto mb-6">
                <table className="w-full text-sm">
                    <thead>
                        <tr className="text-[10px] uppercase tracking-widest text-secondary border-b border-[#222]">
                            <th className="text-left py-2 pr-4">Code</th>
                            <th className="text-left py-2 pr-4">Name</th>
                            <th className="text-left py-2 pr-4">Description</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-[#1a1a1a]">
                        {grades.map((g: any) => (
                            <tr key={g.id ?? g.code} className="hover:bg-[#0d0d0d] transition-colors">
                                <td className="py-3 pr-4 font-mono text-accent text-xs">{g.code}</td>
                                <td className="py-3 pr-4 text-white font-bold text-xs">{g.name}</td>
                                <td className="py-3 pr-4 text-secondary text-xs">{g.description || '—'}</td>
                            </tr>
                        ))}
                        {grades.length === 0 && (
                            <tr><td colSpan={3} className="py-8 text-center text-secondary text-xs italic">
                                No grades defined for this facility yet.
                            </td></tr>
                        )}
                    </tbody>
                </table>
            </div>

            <Link to="/data-management/global-master/data/gm_grade"
                className="inline-flex items-center gap-2 px-4 py-2 text-[10px] font-bold uppercase tracking-widest rounded border border-[#333] text-secondary hover:text-white hover:border-accent transition-colors">
                <ExternalLink size={12} /> Add or edit grades in Global Master
            </Link>
        </div>
    );
};

// ─── Dimension Profile Panel ──────────────────────────────────────────────
const DimensionPanel: React.FC<{ token: string | null }> = ({ token }) => {
    const [dims, setDims] = useState<any[]>([]);
    const [form, setForm] = useState({ name: '', length_mm: '', width_mm: '', thickness_mm: '', tolerance_pct: '0.5' });
    const [editId, setEditId] = useState<string | null>(null);
    const [showForm, setShowForm] = useState(false);
    const [error, setError] = useState('');

    const fetch_ = useCallback(async () => {
        const r = await symsFetch('http://localhost:4000/api/masters/dimensions', { headers: { Authorization: `Bearer ${token}` } });
        if (r.ok) setDims(await r.json());
    }, [token]);
    useEffect(() => { fetch_(); }, [fetch_]);

    const save = async () => {
        setError('');
        const body = { name: form.name, length_mm: form.length_mm, width_mm: form.width_mm, thickness_mm: form.thickness_mm, tolerance_pct: form.tolerance_pct };
        const method = editId ? 'PUT' : 'POST';
        const url = editId ? `http://localhost:4000/api/masters/dimensions/${editId}` : 'http://localhost:4000/api/masters/dimensions';
        const r = await symsFetch(url, { method, headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (r.ok) { setShowForm(false); setEditId(null); setForm({ name: '', length_mm: '', width_mm: '', thickness_mm: '', tolerance_pct: '0.5' }); fetch_(); }
        else { const d = await r.json(); setError(d.error); }
    };

    const startEdit = (d: any) => {
        setForm({ name: d.name, length_mm: String(d.length_mm), width_mm: String(d.width_mm), thickness_mm: String(d.thickness_mm), tolerance_pct: String(d.tolerance_pct) });
        setEditId(d.id); setShowForm(true);
    };

    return (
        <div>
            <SectionHeader icon={<Ruler size={20} />} title="Dimension Profiles" sub="Define standard slab dimension profiles with physical tolerances for routing validation." />
            <div className="overflow-x-auto mb-6">
                <table className="w-full text-sm">
                    <thead>
                        <tr className="text-[10px] uppercase tracking-widest text-secondary border-b border-[#222]">
                            <th className="text-left py-2 pr-4">Profile Name</th>
                            <th className="text-left py-2 pr-4">Length (mm)</th>
                            <th className="text-left py-2 pr-4">Width (mm)</th>
                            <th className="text-left py-2 pr-4">Thickness (mm)</th>
                            <th className="text-left py-2 pr-4">Tolerance ±%</th>
                            <th className="text-right py-2">Actions</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-[#1a1a1a]">
                        {dims.map(d => (
                            <tr key={d.id} className="hover:bg-[#0d0d0d] transition-colors">
                                <td className="py-3 pr-4 text-white font-bold text-xs">{d.name}</td>
                                <td className="py-3 pr-4 font-mono text-accent text-xs">{d.length_mm.toLocaleString()}</td>
                                <td className="py-3 pr-4 font-mono text-blue-300 text-xs">{d.width_mm.toLocaleString()}</td>
                                <td className="py-3 pr-4 font-mono text-warning text-xs">{d.thickness_mm.toLocaleString()}</td>
                                <td className="py-3 pr-4 text-secondary text-xs">± {d.tolerance_pct}%</td>
                                <td className="py-3 text-right">
                                    <div className="flex items-center justify-end gap-2">
                                        <button onClick={() => startEdit(d)} className="p-1.5 rounded hover:bg-[#222] text-secondary hover:text-white"><Edit2 size={13} /></button>
                                        <button onClick={async () => { await symsFetch(`http://localhost:4000/api/masters/dimensions/${d.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${token}` } }); fetch_(); }} className="p-1.5 rounded hover:bg-danger/20 text-secondary hover:text-danger"><Trash2 size={13} /></button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                        {dims.length === 0 && <tr><td colSpan={6} className="py-8 text-center text-secondary text-xs italic">No dimension profiles configured.</td></tr>}
                    </tbody>
                </table>
            </div>
            <div className="border border-[#222] rounded-lg overflow-hidden">
                <button onClick={() => { setShowForm(!showForm); setEditId(null); setForm({ name: '', length_mm: '', width_mm: '', thickness_mm: '', tolerance_pct: '0.5' }); }}
                    className="w-full flex items-center justify-between px-5 py-3 text-xs font-bold uppercase tracking-widest text-accent hover:bg-[#0d0d0d] transition-colors">
                    <span className="flex items-center"><Plus size={14} className="mr-2" />{editId ? 'Edit Profile' : 'Add Dimension Profile'}</span>
                    {showForm ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                </button>
                {showForm && (
                    <div className="p-5 bg-[#0B0C10] border-t border-[#222] space-y-4">
                        {error && <div className="text-danger text-xs bg-danger/10 border border-danger/30 rounded px-3 py-2">{error}</div>}
                        <div>
                            <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Profile Name *</label>
                            <input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="e.g. Standard HDR-8500"
                                className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white text-sm focus:border-accent focus:outline-none placeholder-[#444]" />
                        </div>
                        <div className="grid grid-cols-3 gap-4">
                            {([['length_mm', 'Length (mm)', 'e.g. 8500'], ['width_mm', 'Width (mm)', 'e.g. 1200'], ['thickness_mm', 'Thickness (mm)', 'e.g. 220']] as [string, string, string][]).map(([field, label, ph]) => (
                                <div key={field}>
                                    <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">{label} *</label>
                                    <input type="number" min="0" value={(form as any)[field]} onChange={e => setForm({ ...form, [field]: e.target.value })} placeholder={ph}
                                        className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white font-mono text-sm focus:border-accent focus:outline-none placeholder-[#444]" />
                                </div>
                            ))}
                        </div>
                        <div>
                            <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Physical Tolerance (%)</label>
                            <div className="flex items-center gap-4">
                                <input type="range" min="0" max="5" step="0.1" value={parseFloat(form.tolerance_pct)} onChange={e => setForm({ ...form, tolerance_pct: e.target.value })} className="flex-1 accent-accent" />
                                <span className="text-white font-mono text-sm w-16 text-center">± {form.tolerance_pct}%</span>
                            </div>
                            <p className="text-[10px] text-secondary mt-1">Slabs within ±{form.tolerance_pct}% of these dimensions will match this profile during routing.</p>
                        </div>
                        <div className="flex gap-3 pt-2">
                            <button onClick={save} className="flex items-center px-5 py-2 bg-accent text-background font-bold text-xs uppercase tracking-widest rounded hover:opacity-80">
                                <Save size={13} className="mr-2" />{editId ? 'Save Changes' : 'Create Profile'}
                            </button>
                            <button onClick={() => { setShowForm(false); setEditId(null); }} className="px-5 py-2 border border-[#333] text-secondary text-xs uppercase tracking-widest rounded hover:text-white">Cancel</button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

// ─── Product Mix Panel ────────────────────────────────────────────────────
const ProductMixPanel: React.FC<{ token: string | null }> = ({ token }) => {
    const [mixes, setMixes] = useState<any[]>([]);
    const [grades, setGrades] = useState<any[]>([]);
    const [dims, setDims] = useState<any[]>([]);
    const [form, setForm] = useState({ name: '', description: '', allowed_grades: [] as string[], allowed_dimensions: [] as string[], target_zone_type: 'STORAGE', priority: '0' });
    const [editId, setEditId] = useState<string | null>(null);
    const [showForm, setShowForm] = useState(false);
    const [error, setError] = useState('');

    const fetchAll = useCallback(async () => {
        const [r1, r2, r3] = await Promise.all([
            symsFetch('http://localhost:4000/api/masters/productmix', { headers: { Authorization: `Bearer ${token}` } }),
            symsFetch('http://localhost:4000/api/masters/grades', { headers: { Authorization: `Bearer ${token}` } }),
            symsFetch('http://localhost:4000/api/masters/dimensions', { headers: { Authorization: `Bearer ${token}` } })
        ]);
        if (r1.ok) setMixes(await r1.json());
        if (r2.ok) setGrades(await r2.json());
        if (r3.ok) setDims(await r3.json());
    }, [token]);
    useEffect(() => { fetchAll(); }, [fetchAll]);

    const toggleGrade = (code: string) => setForm(f => ({ ...f, allowed_grades: f.allowed_grades.includes(code) ? f.allowed_grades.filter(g => g !== code) : [...f.allowed_grades, code] }));
    const toggleDim = (id: string) => setForm(f => ({ ...f, allowed_dimensions: f.allowed_dimensions.includes(id) ? f.allowed_dimensions.filter(d => d !== id) : [...f.allowed_dimensions, id] }));

    const save = async () => {
        setError('');
        const method = editId ? 'PUT' : 'POST';
        const url = editId ? `http://localhost:4000/api/masters/productmix/${editId}` : 'http://localhost:4000/api/masters/productmix';
        const r = await symsFetch(url, { method, headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, body: JSON.stringify(form) });
        if (r.ok) { setShowForm(false); setEditId(null); setForm({ name: '', description: '', allowed_grades: [], allowed_dimensions: [], target_zone_type: 'STORAGE', priority: '0' }); fetchAll(); }
        else { const d = await r.json(); setError(d.error); }
    };

    const ZONE_COLORS: Record<string, string> = { STORAGE: 'text-accent border-accent/30 bg-accent/10', SCARFING: 'text-warning border-warning/30 bg-warning/10', DISPATCH: 'text-blue-300 border-blue-400/30 bg-blue-400/10', SHUFFLING: 'text-purple-300 border-purple-400/30 bg-purple-400/10' };

    return (
        <div>
            <SectionHeader icon={<Package size={20} />} title="Product Mix Rules" sub="Define which grade + dimension combinations route to which zone types. Used when Hard Constraints module is ON." />
            <div className="space-y-3 mb-6">
                {mixes.map(m => (
                    <div key={m.id} className={`bg-primary border rounded-xl p-4 ${m.is_active ? 'border-[#333]' : 'border-[#222] opacity-50'}`}>
                        <div className="flex justify-between items-start">
                            <div className="flex-1">
                                <div className="flex items-center gap-3 mb-2">
                                    <span className="font-black text-white text-sm uppercase tracking-wider">{m.name}</span>
                                    <Tag label={`P${m.priority}`} color="bg-[#222] text-secondary border-[#333]" />
                                    <Tag label={m.target_zone_type} color={ZONE_COLORS[m.target_zone_type] || 'bg-[#222] text-secondary border-[#333]'} />
                                </div>
                                {m.description && <p className="text-xs text-secondary mb-2">{m.description}</p>}
                                <div className="flex flex-wrap gap-1.5">
                                    <span className="text-[10px] text-secondary uppercase tracking-widest mr-1">Grades:</span>
                                    {m.allowed_grades.map((g: string) => <Tag key={g} label={g} color="bg-accent/10 text-accent border-accent/30" />)}
                                    <span className="text-[10px] text-secondary uppercase tracking-widest mx-1">·</span>
                                    <span className="text-[10px] text-secondary uppercase tracking-widest mr-1">Dims:</span>
                                    {m.allowed_dimensions.map((id: string) => {
                                        const d = dims.find(x => x.id === id);
                                        return <Tag key={id} label={d ? d.name : id.slice(-6)} color="bg-blue-400/10 text-blue-300 border-blue-400/30" />;
                                    })}
                                </div>
                            </div>
                            <div className="flex items-center gap-2 ml-4 shrink-0">
                                <button onClick={() => { setForm({ name: m.name, description: m.description || '', allowed_grades: m.allowed_grades, allowed_dimensions: m.allowed_dimensions, target_zone_type: m.target_zone_type, priority: String(m.priority) }); setEditId(m.id); setShowForm(true); }} className="p-1.5 rounded hover:bg-[#222] text-secondary hover:text-white"><Edit2 size={13} /></button>
                                <button onClick={async () => { await symsFetch(`http://localhost:4000/api/masters/productmix/${m.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${token}` } }); fetchAll(); }} className="p-1.5 rounded hover:bg-danger/20 text-secondary hover:text-danger"><Trash2 size={13} /></button>
                            </div>
                        </div>
                    </div>
                ))}
                {mixes.length === 0 && <div className="py-8 text-center text-secondary text-xs italic border border-dashed border-[#222] rounded-xl">No product mix rules defined. Add one below.</div>}
            </div>
            <div className="border border-[#222] rounded-lg overflow-hidden">
                <button onClick={() => { setShowForm(!showForm); setEditId(null); setForm({ name: '', description: '', allowed_grades: [], allowed_dimensions: [], target_zone_type: 'STORAGE', priority: '0' }); }}
                    className="w-full flex items-center justify-between px-5 py-3 text-xs font-bold uppercase tracking-widest text-accent hover:bg-[#0d0d0d] transition-colors">
                    <span className="flex items-center"><Plus size={14} className="mr-2" />{editId ? 'Edit Mix Rule' : 'Define Product Mix Rule'}</span>
                    {showForm ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                </button>
                {showForm && (
                    <div className="p-5 bg-[#0B0C10] border-t border-[#222] space-y-5">
                        {error && <div className="text-danger text-xs bg-danger/10 border border-danger/30 rounded px-3 py-2">{error}</div>}
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Mix Name *</label>
                                <input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="e.g. Offshore Structural Package A"
                                    className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white text-sm focus:border-accent focus:outline-none placeholder-[#444]" />
                            </div>
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Description</label>
                                <input value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} placeholder="Optional"
                                    className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white text-sm focus:border-accent focus:outline-none placeholder-[#444]" />
                            </div>
                        </div>
                        <div className="grid grid-cols-2 gap-6">
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-2 font-semibold">Allowed Grades</label>
                                <div className="space-y-1.5 max-h-40 overflow-y-auto pr-1">
                                    {grades.filter(g => g.is_active).map(g => (
                                        <label key={g.id} className="flex items-center gap-2 cursor-pointer group">
                                            <input type="checkbox" checked={form.allowed_grades.includes(g.code)} onChange={() => toggleGrade(g.code)} className="accent-accent" />
                                            <span className="font-mono text-xs text-accent">{g.code}</span>
                                            <span className="text-[10px] text-secondary group-hover:text-white transition-colors">{g.name}</span>
                                        </label>
                                    ))}
                                    {grades.filter(g => g.is_active).length === 0 && <p className="text-xs text-secondary italic">Add grades in the Grade Master section first.</p>}
                                </div>
                            </div>
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-2 font-semibold">Allowed Dimension Profiles</label>
                                <div className="space-y-1.5 max-h-40 overflow-y-auto pr-1">
                                    {dims.map(d => (
                                        <label key={d.id} className="flex items-center gap-2 cursor-pointer group">
                                            <input type="checkbox" checked={form.allowed_dimensions.includes(d.id)} onChange={() => toggleDim(d.id)} className="accent-accent" />
                                            <span className="text-xs text-blue-300">{d.name}</span>
                                            <span className="text-[10px] text-secondary">{d.length_mm}×{d.width_mm}×{d.thickness_mm}</span>
                                        </label>
                                    ))}
                                    {dims.length === 0 && <p className="text-xs text-secondary italic">Add dimension profiles first.</p>}
                                </div>
                            </div>
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Target Zone Type</label>
                                <select value={form.target_zone_type} onChange={e => setForm({ ...form, target_zone_type: e.target.value })}
                                    className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white text-sm focus:border-accent focus:outline-none">
                                    <option value="STORAGE">STORAGE</option>
                                    <option value="SCARFING">SCARFING</option>
                                    <option value="SHUFFLING">SHUFFLING</option>
                                    <option value="DISPATCH">DISPATCH</option>
                                </select>
                            </div>
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Routing Priority (higher = first matched)</label>
                                <input type="number" min="0" max="100" value={form.priority} onChange={e => setForm({ ...form, priority: e.target.value })}
                                    className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white font-mono text-sm focus:border-accent focus:outline-none" />
                            </div>
                        </div>
                        <div className="flex gap-3 pt-2">
                            <button onClick={save} className="flex items-center px-5 py-2 bg-accent text-background font-bold text-xs uppercase tracking-widest rounded hover:opacity-80">
                                <Save size={13} className="mr-2" />{editId ? 'Save Changes' : 'Create Mix Rule'}
                            </button>
                            <button onClick={() => { setShowForm(false); setEditId(null); }} className="px-5 py-2 border border-[#333] text-secondary text-xs uppercase tracking-widest rounded hover:text-white">Cancel</button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

// ─── Rework Activity Panel ────────────────────────────────────────────────
const ReworkActivityPanel: React.FC<{ token: string | null }> = ({ token }) => {
    const [activities, setActivities] = useState<any[]>([]);
    const [form, setForm] = useState({ name: '', description: '' });
    const [editId, setEditId] = useState<string | null>(null);
    const [showForm, setShowForm] = useState(false);
    const [error, setError] = useState('');

    const fetchAll = useCallback(async () => {
        const r = await symsFetch('http://localhost:4000/api/masters/rework', { headers: { Authorization: `Bearer ${token}` } });
        if (r.ok) setActivities(await r.json());
    }, [token]);
    useEffect(() => { fetchAll(); }, [fetchAll]);

    const save = async () => {
        setError('');
        const method = editId ? 'PUT' : 'POST';
        const url = editId ? `http://localhost:4000/api/masters/rework/${editId}` : 'http://localhost:4000/api/masters/rework';
        const r = await symsFetch(url, { method, headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, body: JSON.stringify(form) });
        if (r.ok) { setShowForm(false); setEditId(null); setForm({ name: '', description: '' }); fetchAll(); }
        else { const d = await r.json(); setError(d.error); }
    };

    return (
        <div>
            <SectionHeader icon={<Package size={20} />} title="Rework Activities" sub="Define allowed rework activities for Quality Control workflows." />
            <div className="space-y-3 mb-6">
                {activities.map(a => (
                    <div key={a.id} className={`bg-primary border rounded-xl p-4 ${a.is_active ? 'border-[#333]' : 'border-[#222] opacity-50'}`}>
                        <div className="flex justify-between items-start">
                            <div className="flex-1">
                                <div className="font-black text-white text-sm uppercase tracking-wider mb-1">{a.name}</div>
                                {a.description && <p className="text-xs text-secondary">{a.description}</p>}
                            </div>
                            <div className="flex items-center gap-2 ml-4 shrink-0">
                                <button onClick={() => { setForm({ name: a.name, description: a.description || '' }); setEditId(a.id); setShowForm(true); }} className="p-1.5 rounded hover:bg-[#222] text-secondary hover:text-white"><Edit2 size={13} /></button>
                                <button onClick={async () => { await symsFetch(`http://localhost:4000/api/masters/rework/${a.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${token}` } }); fetchAll(); }} className="p-1.5 rounded hover:bg-danger/20 text-secondary hover:text-danger"><Trash2 size={13} /></button>
                            </div>
                        </div>
                    </div>
                ))}
                {activities.length === 0 && <div className="py-8 text-center text-secondary text-xs italic border border-dashed border-[#222] rounded-xl">No rework activities defined. Add one below.</div>}
            </div>
            <div className="border border-[#222] rounded-lg overflow-hidden">
                <button onClick={() => { setShowForm(!showForm); setEditId(null); setForm({ name: '', description: '' }); }}
                    className="w-full flex items-center justify-between px-5 py-3 text-xs font-bold uppercase tracking-widest text-accent hover:bg-[#0d0d0d] transition-colors">
                    <span className="flex items-center"><Plus size={14} className="mr-2" />{editId ? 'Edit Activity' : 'Define Rework Activity'}</span>
                    {showForm ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                </button>
                {showForm && (
                    <div className="p-5 bg-[#0B0C10] border-t border-[#222] space-y-4">
                        {error && <div className="text-danger text-xs bg-danger/10 border border-danger/30 rounded px-3 py-2">{error}</div>}
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Activity Name *</label>
                                <input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="e.g. Scarfing, Grinding"
                                    className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white text-sm focus:border-accent focus:outline-none placeholder-[#444]" />
                            </div>
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Description</label>
                                <input value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} placeholder="Optional description"
                                    className="w-full bg-primary border border-[#333] rounded px-3 py-2 text-white text-sm focus:border-accent focus:outline-none placeholder-[#444]" />
                            </div>
                        </div>
                        <div className="flex gap-3 pt-2">
                            <button onClick={save} className="flex items-center px-5 py-2 bg-accent text-background font-bold text-xs uppercase tracking-widest rounded hover:opacity-80">
                                <Save size={13} className="mr-2" />{editId ? 'Save Changes' : 'Create Activity'}
                            </button>
                            <button onClick={() => { setShowForm(false); setEditId(null); }} className="px-5 py-2 border border-[#333] text-secondary text-xs uppercase tracking-widest rounded hover:text-white">Cancel</button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

// ─── Activity Log Panel ────────────────────────────────────────────────────
const ActivityLogPanel: React.FC<{ token: string | null }> = ({ token }) => {
    const [logs, setLogs] = useState<any[]>([]);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [filter, setFilter] = useState({ action: '', from: '', to: '' });
    const [expanded, setExpanded] = useState<string | null>(null);
    const [error, setError] = useState('');

    const fetch_ = useCallback(async () => {
        const params = new URLSearchParams({ page: String(page), limit: '50' });
        if (filter.action) params.set('action', filter.action);
        if (filter.from) params.set('from', filter.from);
        if (filter.to) params.set('to', filter.to);
        const r = await symsFetch(`http://localhost:4000/api/logs?${params}`, { headers: { Authorization: `Bearer ${token}` } });
        if (r.ok) {
            const d = await r.json();
            // Defensive even now the API sends { logs, total }: setLogs(undefined) used to
            // throw inside the render below and take the whole Configuration screen down
            // with it, which is a lot of damage for one malformed response.
            setLogs(Array.isArray(d?.logs) ? d.logs : []);
            setTotal(Number(d?.total) || 0);
            setError('');
        } else {
            setLogs([]);
            setTotal(0);
            setError(await symsError(r, 'Could not read the activity log.'));
        }
    }, [token, page, filter]);

    useEffect(() => { fetch_(); const t = setInterval(fetch_, 30000); return () => clearInterval(t); }, [fetch_]);

    const ACTION_COLORS: Record<string, string> = {
        USER_CREATED: 'text-success bg-success/10', USER_UPDATED: 'text-blue-300 bg-blue-300/10', USER_DEACTIVATED: 'text-danger bg-danger/10',
        QA_LOCK_APPLIED: 'text-warning bg-warning/10', QA_LOCK_RELEASED: 'text-accent bg-accent/10',
        AUDIT_MISMATCH: 'text-danger bg-danger/10 font-black', AUDIT_CLEAN: 'text-success bg-success/10'
    };

    return (
        <div>
            <SectionHeader icon={<ScrollText size={20} />} title="Activity Log" sub="Full audit trail of system events. Auto-refreshes every 30 seconds." />

            {error && <div className="mb-4 text-danger text-xs bg-danger/10 border border-danger/30 rounded px-3 py-2">{error}</div>}

            {/* Filters */}
            <div className="flex gap-4 mb-4 flex-wrap">
                <input value={filter.action} onChange={e => setFilter({ ...filter, action: e.target.value })} placeholder="Filter by action..."
                    className="bg-primary border border-[#333] rounded px-3 py-1.5 text-white text-xs focus:border-accent focus:outline-none placeholder-[#444] w-48" />
                <input type="date" value={filter.from} onChange={e => setFilter({ ...filter, from: e.target.value })} className="bg-primary border border-[#333] rounded px-3 py-1.5 text-white text-xs focus:border-accent focus:outline-none" />
                <span className="text-secondary text-xs self-center">→</span>
                <input type="date" value={filter.to} onChange={e => setFilter({ ...filter, to: e.target.value })} className="bg-primary border border-[#333] rounded px-3 py-1.5 text-white text-xs focus:border-accent focus:outline-none" />
                <button onClick={() => { setPage(1); fetch_(); }} className="px-4 py-1.5 bg-accent text-background text-xs font-bold uppercase tracking-widest rounded hover:opacity-80">Apply</button>
                <button onClick={() => { setFilter({ action: '', from: '', to: '' }); setPage(1); }} className="px-4 py-1.5 border border-[#333] text-secondary text-xs rounded hover:text-white">Clear</button>
                <span className="text-secondary text-xs self-center ml-auto">{total} total events</span>
            </div>

            <div className="space-y-1 max-h-[60vh] overflow-y-auto pr-1">
                {logs.map(log => (
                    <div key={log.id} className="bg-primary border border-[#1a1a1a] rounded-lg overflow-hidden hover:border-[#333] transition-colors">
                        <button onClick={() => setExpanded(expanded === log.id ? null : log.id)} className="w-full flex items-center gap-4 px-4 py-3 text-left">
                            <span className="text-[10px] text-secondary font-mono whitespace-nowrap">{new Date(log.timestamp).toLocaleString()}</span>
                            <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded shrink-0 ${ACTION_COLORS[log.action] || 'text-secondary bg-[#222]'}`}>{log.action}</span>
                            {log.user_id && <span className="text-[10px] text-secondary font-mono">uid:{log.user_id.slice(-6)}</span>}
                            <span className="ml-auto text-secondary"><ChevronRight size={12} className={`transition-transform ${expanded === log.id ? 'rotate-90' : ''}`} /></span>
                        </button>
                        {expanded === log.id && (
                            <div className="px-4 pb-3 border-t border-[#1a1a1a] bg-[#050508]">
                                <pre className="text-[11px] text-secondary font-mono mt-2 whitespace-pre-wrap break-all">{JSON.stringify(log.details, null, 2)}</pre>
                            </div>
                        )}
                    </div>
                ))}
                {logs.length === 0 && <div className="py-12 text-center text-secondary text-xs italic">No log entries found.</div>}
            </div>

            {total > 50 && (
                <div className="flex items-center justify-center gap-4 mt-4">
                    <button disabled={page === 1} onClick={() => setPage(p => p - 1)} className="px-4 py-1.5 border border-[#333] text-xs text-secondary rounded disabled:opacity-30 hover:text-white">← Prev</button>
                    <span className="text-xs text-secondary">Page {page}</span>
                    <button disabled={page * 50 >= total} onClick={() => setPage(p => p + 1)} className="px-4 py-1.5 border border-[#333] text-xs text-secondary rounded disabled:opacity-30 hover:text-white">Next →</button>
                </div>
            )}
        </div>
    );
};

// ─── Main AdminConfigurator ────────────────────────────────────────────────
export const AdminConfigurator: React.FC = () => {
    const { token } = useAuth();
    const { material } = useYardMaterial();
    const formationOptions = formationsFor(material.type_key);
    const [activeTab, setActiveTab] = useState<Tab>('master_data');
    const [masterSub, setMasterSub] = useState<'grades' | 'dimensions' | 'productmix' | 'rework'>('grades');
    const [zones, setZones] = useState<any[]>([]);
    const [zoneError, setZoneError] = useState('');
    const [pendingDelete, setPendingDelete] = useState<string | null>(null);
    const [rules, setRules] = useState<Record<string, string>>({});
    const [newZone, setNewZone] = useState({ name: '', type: 'STORAGE' });
    const [storageStyle, setStorageStyle] = useState<StorageStyle>(formationOptions[0]);
    const [floorCap, setFloorCap] = useState(14);
    const [gridSetup, setGridSetup] = useState({ rows: 1, cols: 1, defaultCapacity: 5 });
    const [gridConfig, setGridConfig] = useState<{ row: number, col: number, cap: number }[]>([]);

    const applyFormationDefaults = (style: StorageStyle) => {
        if (style === 'RACK') setGridSetup({ rows: 3, cols: 4, defaultCapacity: 1 });
        else if (style === 'PYRAMID') setGridSetup({ rows: 3, cols: 4, defaultCapacity: 1 });
        else if (style === 'FLOOR') setFloorCap(14);
        else setGridSetup({ rows: 1, cols: 1, defaultCapacity: 5 });
    };

    useEffect(() => { fetchZones(); fetchRules(); }, [activeTab]);

    useEffect(() => {
        const next = formationsFor(material.type_key)[0];
        setStorageStyle(next);
        applyFormationDefaults(next);
    }, [material.type_key]);

    useEffect(() => {
        if (newZone.type === 'STORAGE' && storageStyle !== 'FLOOR') {
            const cap = storageStyle === 'STACK' ? gridSetup.defaultCapacity : 1;
            const g = [];
            for (let r = 0; r < gridSetup.rows; r++)
                for (let c = 0; c < gridSetup.cols; c++)
                    g.push({ row: r, col: c, cap });
            setGridConfig(g);
        }
    }, [gridSetup.rows, gridSetup.cols, gridSetup.defaultCapacity, newZone.type, storageStyle]);

    const fetchZones = async () => { if (!token) return; const r = await symsFetch('http://localhost:4000/api/zones', { headers: { Authorization: `Bearer ${token}` } }); if (r.ok) setZones(await r.json()); };
    const fetchRules = async () => {
        if (!token) return;
        const r = await symsFetch('http://localhost:4000/api/config', { headers: { Authorization: `Bearer ${token}` } });
        let map: any = { ...rules };
        if (r.ok) { const data = await r.json(); data.forEach((d: any) => { map[d.key] = d.value; }); }
        
        setRules(map);
    };
    const updateRule = async (key: string, value: string) => {
        setRules(p => ({ ...p, [key]: value }));
        await symsFetch(`http://localhost:4000/api/config/${key}`, { method: 'PUT', headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ value }) });
    };
    const handleGridChange = (r: number, c: number, cap: string) => setGridConfig(prev => prev.map(cell => (cell.row === r && cell.col === c) ? { ...cell, cap: parseInt(cap) || 0 } : cell));
    const createZone = async (e: React.FormEvent) => {
        e.preventDefault();
        setZoneError('');
        const style: StorageStyle = newZone.type === 'STORAGE' ? storageStyle : 'STACK';
        const columns = style === 'FLOOR'
            ? [{ row_index: 0, col_index: 0, capacity_limit: floorCap }]
            : gridConfig.map(c => ({ row_index: c.row, col_index: c.col, capacity_limit: c.cap }));
        const payload = { name: newZone.name, type: newZone.type, storage_style: style, layout_config: newZone.type === 'STORAGE' && style !== 'FLOOR' ? { rows: gridSetup.rows, cols: gridSetup.cols } : null, columns };
        // The response was thrown away, so a duplicate sector name or a grid the yard
        // refused looked exactly like success: the form cleared and no zone appeared.
        const r = await symsFetch('http://localhost:4000/api/zones', { method: 'POST', body: JSON.stringify(payload) });
        if (!r.ok) { setZoneError(await symsError(r, 'Could not create that sector.')); return; }
        setNewZone({ name: '', type: 'STORAGE' }); applyFormationDefaults(storageStyle); fetchZones();
    };
    const deleteZone = async (zone: { id: string }) => {
        setZoneError('');
        const r = await symsFetch(`http://localhost:4000/api/zones/${zone.id}`, { method: 'DELETE' });
        if (!r.ok) { setPendingDelete(null); setZoneError(await symsError(r, 'Could not delete that sector.')); return; }
        setPendingDelete(null);
        fetchZones();
    };

    // Was `user?.role !== 'Admin'` — a standalone mSYMS role name the platform never
    // produces, so this screen said "Unauthorized" to everybody, super admin included.
    if (!hasPermission('YRDA')) {
        return (
            <div className="max-w-xl mx-auto mt-16 text-center">
                <Shield size={48} className="text-danger mx-auto mb-4" />
                <h2 className="text-xl font-black text-white uppercase tracking-widest mb-2">Not your call</h2>
                <p className="text-secondary text-sm">Configuring the yard needs the Slab Yard Administration permission.</p>
            </div>
        );
    }

    // Three tabs are gone, and none of them because they were hard.
    //
    //   Users     — the platform owns identity. This tab created Slab-Yard-local accounts
    //               with their own usernames and passwords: a second place to be a person.
    //               System -> User Management is the one place.
    //   Integrations — the platform owns plant connections. Communication Interface and the
    //               Tag Hub are where a PLC, a LIMS or an ERP is wired up, with driver
    //               tests, confirmed writes and an audit trail behind them.
    //   Decision Rules — a GoRules editor for a decision model nothing evaluates. The rule
    //               engine's Zen path is not built, so every model saved here would have sat
    //               unread while the built-in allocator did the routing.
    const TABS: { id: Tab; label: string; icon: React.ReactNode }[] = [
        { id: 'zones' as Tab, label: 'Yard Topology', icon: <Layers size={14} /> },
        { id: 'master_data', label: 'Master Data', icon: <Database size={14} /> },
        { id: 'erp_modules', label: 'Rule Book', icon: <Shield size={14} /> },
        { id: 'activity_log', label: 'Activity Log', icon: <ScrollText size={14} /> },
    ];

    return (
        <div className="text-secondary max-w-7xl mx-auto pb-12">
            <div className="flex items-center justify-between mb-8 border-b border-[#333] pb-5">
                <h2 className="text-2xl tracking-widest uppercase font-black text-white">System Configurator</h2>
                <span className="text-[10px] text-secondary border border-[#333] px-3 py-1 rounded uppercase tracking-widest">Admin Access</span>
            </div>

            {/* Tab Bar */}
            <div className="flex space-x-2 mb-8 overflow-x-auto pb-1">
                {TABS.map(t => (
                    <button key={t.id} onClick={() => setActiveTab(t.id)}
                        className={`flex items-center gap-2 px-4 py-2 text-xs font-bold uppercase tracking-widest rounded whitespace-nowrap transition-all ${activeTab === t.id ? 'bg-accent text-background shadow-[0_0_20px_rgba(9,132,70,0.3)]' : 'bg-primary border border-[#333] text-secondary hover:text-white hover:border-[#555]'}`}>
                        {t.icon}{t.label}
                    </button>
                ))}
            </div>

            {/* ── Tab: Yard Topology (Zones) ─────────────────────────────── */}
            {activeTab === 'zones' && (
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                    <div className="bg-primary p-6 rounded-xl border border-[#333]">
                        <SectionHeader icon={<Layers size={18} />} title="Provision Sector" sub={`A ${material.singular.toLowerCase()} area is a ${FORMATION[storageStyle].label.toLowerCase()}. ${FORMATION[storageStyle].rule}`} />
                        <form onSubmit={createZone} className="space-y-4">
                            {zoneError && <div className="text-danger text-xs bg-danger/10 border border-danger/30 rounded px-3 py-2">{zoneError}</div>}
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Sector Name *</label>
                                <input required value={newZone.name} onChange={e => setNewZone({ ...newZone, name: e.target.value })} placeholder="e.g. Main Storage Bay"
                                    className="w-full bg-[#0B0C10] p-2 rounded text-white border border-[#333] focus:border-accent focus:outline-none" />
                            </div>
                            <div>
                                <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Classification *</label>
                                <select value={newZone.type} onChange={e => setNewZone({ ...newZone, type: e.target.value })} className="w-full bg-[#0B0C10] p-2 rounded text-white border border-[#333] focus:border-accent focus:outline-none">
                                    <option value="STORAGE">Storage</option>
                                    <option value="SCARFING">Scarfing Bucket</option>
                                    <option value="SHUFFLING">Shuffling Cache</option>
                                    <option value="DISPATCH">Dispatch Lane</option>
                                </select>
                            </div>
                            {newZone.type === 'STORAGE' && (
                                <div>
                                    <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Formation *</label>
                                    {formationOptions.length === 1 ? (
                                        <div className="w-full bg-[#0B0C10] p-2 rounded text-white border border-[#333] text-sm">{FORMATION[storageStyle].label} — {FORMATION[storageStyle].materials}</div>
                                    ) : (
                                        <select value={storageStyle} onChange={e => { const style = e.target.value as StorageStyle; setStorageStyle(style); applyFormationDefaults(style); }} className="w-full bg-[#0B0C10] p-2 rounded text-white border border-[#333] focus:border-accent focus:outline-none">
                                            {formationOptions.map(style => (
                                                <option key={style} value={style}>{FORMATION[style].label} — {FORMATION[style].materials}</option>
                                            ))}
                                        </select>
                                    )}
                                </div>
                            )}
                            {newZone.type === 'STORAGE' && storageStyle !== 'FLOOR' ? (
                                <div className="bg-[#0B0C10] p-4 rounded border border-[#222]">
                                    <h4 className="text-accent uppercase text-[10px] font-bold tracking-widest mb-3">{storageStyle === 'STACK' ? 'Stack Grid' : storageStyle === 'RACK' ? 'Rack Slots' : 'Pyramid Rows'}</h4>
                                    <div className="flex gap-4 mb-4">
                                        {[['rows', 'Rows'], ['cols', 'Cols'], ...(storageStyle === 'STACK' ? [['defaultCapacity', 'Depth']] : [])].map(([field, label]) => (
                                            <div key={field}>
                                                <label className="block text-[10px] text-secondary uppercase mb-1">{label}</label>
                                                <input type="number" min="1" max="20" value={(gridSetup as any)[field]} onChange={e => setGridSetup({ ...gridSetup, [field]: parseInt(e.target.value) || 1 })} className="w-20 bg-primary p-2 rounded text-white border border-[#333] focus:border-accent focus:outline-none" />
                                            </div>
                                        ))}
                                    </div>
                                    {storageStyle === 'STACK' && (
                                        <div className="grid gap-2" style={{ gridTemplateColumns: `repeat(${gridSetup.cols}, minmax(0, 1fr))` }}>
                                            {gridConfig.map((c, i) => (
                                                <div key={i} className={`relative group flex flex-col p-2 rounded border ${c.cap === 0 ? 'bg-[#1a1111] border-danger/50' : 'bg-primary border-[#333]'}`}>
                                                    <span className={`text-[9px] mb-1 text-center font-bold ${c.cap === 0 ? 'text-danger/70' : 'text-gray-500'}`}>R{c.row + 1}-C{c.col + 1}</span>
                                                    {c.cap === 0 ? <div className="text-center font-bold text-danger text-[10px] py-1">Disabled</div> : <input type="number" value={c.cap} onChange={e => handleGridChange(c.row, c.col, e.target.value)} className="w-full text-center bg-[#0B0C10] p-1 rounded text-white border border-[#222] focus:border-accent focus:outline-none text-xs" />}
                                                    <button type="button" onClick={() => handleGridChange(c.row, c.col, c.cap === 0 ? String(gridSetup.defaultCapacity) : '0')} className={`absolute top-0 right-0 p-1 opacity-0 group-hover:opacity-100 transition-opacity ${c.cap === 0 ? 'text-success' : 'text-danger'}`}>
                                                        {c.cap === 0 ? <Plus size={12} /> : <Trash2 size={12} />}
                                                    </button>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                    {storageStyle !== 'STACK' && (
                                        <p className="text-[10px] uppercase tracking-widest text-secondary">{gridSetup.rows * gridSetup.cols} addressed {storageStyle === 'RACK' ? 'slots, one item each.' : 'places. The top row stays free.'}</p>
                                    )}
                                </div>
                            ) : newZone.type === 'STORAGE' ? (
                                <div>
                                    <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Spaces on the floor</label>
                                    <input required type="number" min="1" max="1000" value={floorCap} onChange={e => setFloorCap(parseInt(e.target.value) || 1)} className="w-full bg-[#0B0C10] p-2 rounded text-white border border-[#333] focus:border-accent focus:outline-none" />
                                </div>
                            ) : (
                                <div>
                                    <label className="block text-[10px] uppercase tracking-widest text-secondary mb-1.5 font-semibold">Bucket Capacity ({material.plural})</label>
                                    <input required type="number" min="1" value={gridConfig.length ? gridConfig[0].cap : 10} onChange={e => setGridConfig([{ row: 0, col: 0, cap: parseInt(e.target.value) }])} className="w-full bg-[#0B0C10] p-2 rounded text-white border border-[#333] focus:border-accent focus:outline-none" />
                                </div>
                            )}
                            <button type="submit" className="w-full bg-accent text-background font-bold py-3 uppercase tracking-widest text-sm rounded hover:opacity-80 transition-opacity">Provision Sector</button>
                        </form>
                    </div>
                    <div className="bg-primary p-6 rounded-xl border border-[#333] max-h-[700px] overflow-y-auto">
                        <SectionHeader icon={<Layers size={18} />} title="Active Sectors" />
                        {zoneError && <div className="text-danger text-xs bg-danger/10 border border-danger/30 rounded px-3 py-2 mb-3">{zoneError}</div>}
                        <div className="space-y-3">
                            {zones.map(z => {
                                const totalCap = z.columns ? z.columns.reduce((a: number, c: any) => a + c.capacity_limit, 0) : 0;
                                return (
                                    <div key={z.id} className="bg-[#0B0C10] p-4 rounded-lg border border-[#222]">
                                        <div className="flex justify-between items-center gap-3">
                                            <div className="min-w-0"><div className="font-bold text-white uppercase text-sm">{z.name}</div><div className="text-xs text-accent mt-0.5 tracking-widest uppercase">{z.type}{z.storage_style ? ` · ${FORMATION[z.storage_style as StorageStyle]?.label || z.storage_style}` : ''}</div></div>
                                            <div className="flex items-center gap-3 shrink-0">
                                                <div className="text-right"><div className="text-[10px] text-secondary uppercase">Capacity</div><div className="font-mono text-white text-lg font-bold">{totalCap}</div></div>
                                                <button type="button" onClick={() => setPendingDelete(pendingDelete === z.id ? null : z.id)} title={`Delete ${z.name}`} className="p-1.5 rounded hover:bg-danger/20 text-secondary hover:text-danger"><Trash2 size={14} /></button>
                                            </div>
                                        </div>
                                        {pendingDelete === z.id && (
                                            <div className="flex items-center justify-between gap-3 border-t border-[#1a1a1a] pt-2 mt-2">
                                                <span className="text-[10px] uppercase tracking-widest text-danger">Delete this sector?</span>
                                                <span className="flex gap-2">
                                                    <button type="button" onClick={() => setPendingDelete(null)} className="px-2 py-1 text-[10px] uppercase tracking-widest text-secondary border border-[#333] rounded">Cancel</button>
                                                    <button type="button" onClick={() => deleteZone(z)} className="px-2 py-1 text-[10px] uppercase tracking-widest text-white bg-danger rounded">Delete</button>
                                                </span>
                                            </div>
                                        )}
                                        {z.layout_config && <div className="text-[10px] text-secondary border-t border-[#1a1a1a] pt-2 mt-2">{z.layout_config.rows} × {z.layout_config.cols} grid — {z.columns?.length || 0} columns</div>}
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                </div>
            )}

            {/* ── Tab: Master Data ───────────────────────────────────────── */}
            {activeTab === 'master_data' && (
                <div className="space-y-0">
                    <div className="flex gap-2 mb-6">
                        {([['grades', 'Grade Master', <FlaskConical size={12} />], ['dimensions', 'Dimension Profiles', <Ruler size={12} />], ['productmix', 'Product Mix Rules', <Package size={12} />], ...(rules.MODULE_REWORK === 'true' ? [['rework', 'Rework Activities', <Package size={12} />]] : [])] as [string, string, React.ReactNode][]).map(([id, label, icon]) => (
                            <button key={id} onClick={() => setMasterSub(id as any)}
                                className={`flex items-center gap-1.5 px-4 py-1.5 text-[10px] font-bold uppercase tracking-widest rounded border transition-all ${masterSub === id ? 'border-accent text-accent bg-accent/10' : 'border-[#2a2a2a] text-secondary hover:text-white'}`}>
                                {icon}{label}
                            </button>
                        ))}
                    </div>
                    <div className="bg-primary p-6 rounded-xl border border-[#333]">
                        {masterSub === 'grades' && <GradeMasterPanel token={token} />}
                        {masterSub === 'dimensions' && <DimensionPanel token={token} />}
                        {masterSub === 'productmix' && <ProductMixPanel token={token} />}
                        {masterSub === 'rework' && <ReworkActivityPanel token={token} />}
                    </div>
                </div>
            )}

            {/* ── Tab: ERP Modules ──────────────────────────────────────── */}
            {activeTab === 'erp_modules' && (
                <div className="max-w-4xl space-y-4">
                    {/* Thermal Tracking Module Toggle */}
                    <div className="bg-[#1a1511] p-6 rounded-xl border border-warning/50 shadow-[0_0_15px_rgba(255,165,0,0.05)] mb-6">
                        <div className="flex justify-between items-start gap-6">
                            <div className="flex-1">
                                <div className="font-black text-sm uppercase tracking-widest mb-1 text-warning">MODULE: Thermal Tracking Engine</div>
                                <div className="text-secondary text-xs leading-relaxed">Ages every slab from HOT to WARM to COLD by how long ago it was cast, checked every 30 seconds. A slab with no cast time stays COLD rather than being guessed at. The two boundaries are the thermal_hot_minutes and thermal_warm_minutes settings. Thermal state drives the mill charging order, so leaving this off means the campaign is sequenced as if every slab were cold.</div>
                            </div>
                            <div className="shrink-0">
                                <button 
                                    onClick={() => updateRule('MODULE_THERMAL_TRACKING', rules.MODULE_THERMAL_TRACKING === 'true' ? 'false' : 'true')}
                                    className={`relative inline-flex h-[28px] w-[56px] shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 focus:outline-none ${rules.MODULE_THERMAL_TRACKING === 'true' ? 'bg-warning' : 'bg-[#2a2a2a]'}`}>
                                    <span className={`pointer-events-none inline-block h-[24px] w-[24px] transform rounded-full bg-white shadow ring-0 transition duration-200 ${rules.MODULE_THERMAL_TRACKING === 'true' ? 'translate-x-[28px]' : 'translate-x-0'}`} />
                                </button>
                            </div>
                        </div>
                    </div>

                    <div className="bg-primary p-5 rounded-xl border border-[#333] mb-2">
                        <p className="text-xs text-secondary">Turn on the parts of the yard's behaviour this facility actually uses. With everything off, Slab Yard is a plain tracker; each switch adds a rule the allocator and the operator worklist then obey.</p>
                    </div>

                    <div className="bg-primary p-5 rounded-xl border border-[#333] mb-2">
                        <div className="text-[10px] uppercase tracking-widest text-secondary mb-2 font-bold">Configured elsewhere</div>
                        <p className="text-xs text-secondary mb-3">Who can use this yard, and how it is wired to the plant, are the platform's to answer — the same answers the rest of the modules use.</p>
                        <div className="flex flex-wrap gap-2">
                            <Link to="/system/user-management" className="inline-flex items-center gap-2 px-3 py-1.5 text-[10px] font-bold uppercase tracking-widest rounded border border-[#333] text-secondary hover:text-white hover:border-accent transition-colors">
                                <ExternalLink size={12} /> Users
                            </Link>
                            <Link to="/system/roles-permissions" className="inline-flex items-center gap-2 px-3 py-1.5 text-[10px] font-bold uppercase tracking-widest rounded border border-[#333] text-secondary hover:text-white hover:border-accent transition-colors">
                                <ExternalLink size={12} /> Roles &amp; permissions
                            </Link>
                            <Link to="/tag-hub" className="inline-flex items-center gap-2 px-3 py-1.5 text-[10px] font-bold uppercase tracking-widest rounded border border-[#333] text-secondary hover:text-white hover:border-accent transition-colors">
                                <ExternalLink size={12} /> Plant connections
                            </Link>
                        </div>
                    </div>

                    {[
                        { key: 'rule_grade_separation', label: 'Strict Grade Separation', desc: 'Prevents allocation of different steel chemistry grades within the same physical stacking column. The RuleEngine filters column candidates to grade-homogeneous pits only.', color: 'bg-success' },
                        { key: 'MODULE_QA_WORKFLOW', label: 'QA Security Deadlock', desc: 'Adds the operator tablet’s "Request QA" button, so a crane driver who sees a defect can stop the slab. A slab under a Met-Lab hold cannot be moved, dispatched or charged to the mill whether or not this is on — releasing one needs the Release QA Lock permission, and sending it for rework needs Quality.', color: 'bg-warning' },
                        { key: 'MODULE_MASTER_ROUTING', label: 'Hard Constraints (Product Mix Routing)', desc: 'Forces the RuleEngine to validate incoming slabs against ProductMix master rules. Slabs with no matching ProductMix are held in HOLD status rather than auto-routed. Configure rules in Master Data → Product Mix.', color: 'bg-blue-500' },
                        { key: 'MODULE_REWORK', label: 'Rework Activities Master', desc: 'Enables the creation of predefined Rework Activities (e.g. Scarfing, Grinding). Required for the QA Dashboard to assign specific rework activities to slabs.', color: 'bg-accent' },
                        { key: 'MODULE_ZONE_BUILDER', label: 'Visual Zone Configurator', desc: 'Enables the Sector Zones tab in the Admin Panel to visually create and configure new Yard Zones and Columns.', color: 'bg-accent' },
                    ].map(m => (
                        <div key={m.key} className="bg-primary p-6 rounded-xl border border-[#222]">
                            <div className="flex justify-between items-start gap-6">
                                <div className="flex-1">
                                    <div className={`font-black text-sm uppercase tracking-widest mb-1 ${rules[m.key] === 'true' ? 'text-white' : 'text-secondary'}`}>{m.label}</div>
                                    <div className="text-secondary text-xs leading-relaxed">{m.desc}</div>
                                </div>
                                <div className="shrink-0 flex flex-col items-center gap-1">
                                    <Toggle value={rules[m.key] === 'true'} onChange={v => updateRule(m.key, v ? 'true' : 'false')} color={m.color} />
                                    <span className={`text-[10px] font-bold uppercase ${rules[m.key] === 'true' ? 'text-success' : 'text-secondary'}`}>{rules[m.key] === 'true' ? 'ON' : 'OFF'}</span>
                                </div>
                            </div>
                        </div>
                    ))}

                    {/* Stale Slab Threshold */}
                    <div className="bg-primary p-6 rounded-xl border border-[#222]">
                        <div className="font-black text-sm uppercase tracking-widest mb-1 text-white">Stale Slab Detection Threshold</div>
                        <div className="text-secondary text-xs mb-4">Slabs that haven't been physically moved for more than this many days are flagged amber on the Yard Dashboard. Evaluated on each dashboard load.</div>
                        <div className="flex items-center gap-4">
                            <input type="range" min="1" max="90" value={parseInt(rules.stale_slab_days || '14')} onChange={e => updateRule('stale_slab_days', e.target.value)} className="flex-1 accent-accent" />
                            <span className="text-white font-mono text-lg font-bold w-16 text-center">{rules.stale_slab_days || '14'} days</span>
                        </div>
                    </div>

                    {/* Plant value on the dashboard — the one place Slab Yard reads live plant data */}
                    <div className="bg-primary p-6 rounded-xl border border-[#222]">
                        <div className="font-black text-sm uppercase tracking-widest mb-1 text-white">Plant Value On The Dashboard</div>
                        <div className="text-secondary text-xs mb-4">
                            One live reading from the Tag Hub, shown beside the sector grid — a furnace temperature,
                            a line speed, whatever this yard's crew watches. Name the tag exactly as it appears under
                            Communication Interface. Leave it empty and the badge does not appear.
                        </div>
                        <div className="flex flex-col gap-3 sm:flex-row">
                            <input type="text" value={rules.dashboard_tag_path || ''}
                                onChange={e => updateRule('dashboard_tag_path', e.target.value)}
                                placeholder="BRM1 / Furnace / Zone1 / Temperature"
                                className="flex-1 bg-[#1a1a1a] border border-[#333] rounded px-4 py-2 text-white font-mono text-sm focus:outline-none focus:border-accent" />
                            <input type="text" value={rules.dashboard_tag_label || ''}
                                onChange={e => updateRule('dashboard_tag_label', e.target.value)}
                                placeholder="Furnace Z1"
                                className="sm:w-48 bg-[#1a1a1a] border border-[#333] rounded px-4 py-2 text-white text-sm focus:outline-none focus:border-accent" />
                        </div>
                        <div className="text-[10px] text-secondary mt-2 uppercase tracking-widest">Tag path · short label</div>
                    </div>

                    {/* Stack Nomenclature */}
                    <div className="bg-primary p-6 rounded-xl border border-[#222]">
                        <div className="font-black text-sm uppercase tracking-widest mb-1 text-white">Stack Nomenclature Format</div>
                        <div className="text-secondary text-xs mb-4">Define how stack locations are named on the dashboard. Use {"{row}"} and {"{col}"} as variables. (e.g. R{"{row}"} - C{"{col}"} or Bay {"{row}"} Stack {"{col}"})</div>
                        <div className="flex items-center gap-4">
                            <input type="text" value={rules.rule_nomenclature_format || 'R{row} - C{col}'} onChange={e => updateRule('rule_nomenclature_format', e.target.value)} className="flex-1 bg-[#1a1a1a] border border-[#333] rounded px-4 py-2 text-white font-mono text-sm focus:outline-none focus:border-accent" placeholder="R{row} - C{col}" />
                        </div>
                    </div>
                </div>
            )}

            {/* ── Tab: Activity Log ────────────────────────────────────── */}
            {activeTab === 'activity_log' && (
                <div className="bg-primary p-6 rounded-xl border border-[#333]">
                    <ActivityLogPanel token={token} />
                </div>
            )}
        </div>
    );
};

export default AdminConfigurator;
