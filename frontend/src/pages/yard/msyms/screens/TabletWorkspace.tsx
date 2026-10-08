import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useSocket } from '../contexts/SocketContext';
import { CheckCircle2, AlertTriangle, ArrowRight, ScanLine, X, Plus } from 'lucide-react';
import { symsFetch, symsError } from '../symsApi';

export const TabletWorkspace: React.FC = () => {
    const { token } = useAuth();
    const { socket, connected } = useSocket();
    const [orders, setOrders] = useState<any[]>([]);
    const [validations, setValidations] = useState<Record<string, string>>({});
    const [rules, setRules] = useState<Record<string, string>>({});
    const [auditScans, setAuditScans] = useState<Record<string, string[]>>({});
    const [auditInput, setAuditInput] = useState<Record<string, string>>({});
    const [auditResult, setAuditResult] = useState<Record<string, any>>({});
    const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null);
    // Which order the operator is currently writing a reason for, and what they have typed.
    const [flagging, setFlagging] = useState<string | null>(null);
    const [flagReason, setFlagReason] = useState('');

    const fetchRules = async () => {
        if (!token) return;
        const res = await symsFetch('http://localhost:4000/api/config', { headers: { Authorization: `Bearer ${token}` } });
        if (res.ok) { const data = await res.json(); const r: any = {}; data.forEach((d: any) => { r[d.key] = d.value; }); setRules(r); }
    };

    const fetchOrders = async () => {
        if (!token) return;
        const res = await symsFetch('http://localhost:4000/api/workorders', { headers: { Authorization: `Bearer ${token}` } });
        if (res.ok) setOrders(await res.json());
    };

    useEffect(() => {
        fetchOrders(); fetchRules();
        if (socket) {
            socket.on('workorder:new', fetchOrders);
            socket.on('workorder:completed', fetchOrders);
        }
        return () => { if (socket) { socket.off('workorder:new'); socket.off('workorder:completed'); } };
    }, [socket, token]);

    // The response used to be discarded. Everything the yard can refuse an order for —
    // a QA hold applied since it was raised, a destination that filled up in the meantime,
    // an order somebody else already completed — comes back as a plain sentence explaining
    // which, and the crane operator saw none of it: the row simply did not change.
    const completeOrder = async (id: string, isFlagged: boolean = false, reason?: string) => {
        const endpoint = isFlagged
            ? `http://localhost:4000/api/workorders/${id}/flag`
            : `http://localhost:4000/api/workorders/${id}/complete`;
        setNotice(null);
        try {
            const res = await symsFetch(endpoint, {
                method: 'PUT',
                body: isFlagged ? JSON.stringify({ reason }) : undefined,
            });
            if (res.ok) {
                setNotice({ tone: 'ok', text: isFlagged ? 'Sent back with your reason. The next shift will see it.' : 'Done.' });
            } else {
                setNotice({ tone: 'bad', text: await symsError(res, 'That order could not be completed.') });
            }
        } catch {
            setNotice({ tone: 'bad', text: 'Could not reach the yard. Nothing was changed.' });
        }
        fetchOrders();
    };

    const submitAudit = async (order: any) => {
        const scanned = auditScans[order.id] || [];
        const res = await symsFetch(`http://localhost:4000/api/workorders/${order.id}/complete`, {
            method: 'PUT',
            headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
            body: JSON.stringify({ scanned_ids: scanned })
        });
        if (res.ok) {
            const data = await res.json();
            if (data.audit_result) setAuditResult(prev => ({ ...prev, [order.id]: data.audit_result }));
        }
    };

    const addAuditScan = (orderId: string) => {
        const val = (auditInput[orderId] || '').trim().toUpperCase();
        if (!val) return;
        setAuditScans(prev => ({ ...prev, [orderId]: [...(prev[orderId] || []), val] }));
        setAuditInput(prev => ({ ...prev, [orderId]: '' }));
    };

    const removeAuditScan = (orderId: string, idx: number) => {
        setAuditScans(prev => ({ ...prev, [orderId]: prev[orderId].filter((_, i) => i !== idx) }));
    };

    const toggleQALock = async (id: string, lock: boolean) => {
        const res = await symsFetch(`http://localhost:4000/api/workorders/${id}/qa-lock`, {
            method: 'PUT',
            headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
            body: JSON.stringify({ locked: lock })
        });
        if (!res.ok) setNotice({ tone: 'bad', text: await symsError(res, 'That quality hold could not be changed.') });
        else { setNotice({ tone: 'ok', text: lock ? 'Slab put on hold for Met-Lab.' : 'Hold released.' }); fetchOrders(); }
    };

    return (
        <div className="bg-[#0B0C10] text-white p-4 rounded-lg">
            <div className="flex items-center justify-between border-b border-[#333] pb-4 mb-4">
                <div>
                    <h1 className="text-2xl font-black uppercase tracking-widest text-accent flex items-center">
                        Active Worklist
                        <span className="ml-4 bg-[#222] text-[#888] text-sm px-3 py-1 rounded font-mono">{orders.length} QUEUED</span>
                    </h1>
                    <div className="text-xs text-secondary mt-1">Ground Operator Interface</div>
                </div>
                <div className={`px-4 py-2 rounded font-bold text-xs tracking-widest ${connected ? 'bg-success/20 text-success' : 'bg-danger/20 text-danger'}`}>
                    {connected ? 'SYNCED' : 'OFFLINE'}
                </div>
            </div>

            {notice && (
                <div className={`mb-4 px-4 py-3 rounded border text-xs font-bold tracking-wide ${
                    notice.tone === 'ok'
                        ? 'text-success bg-success/10 border-success/30'
                        : 'text-danger bg-danger/10 border-danger/30'
                }`}>
                    {notice.text}
                </div>
            )}

            <div className="space-y-4">
                {orders.length === 0 ? (
                    <div className="text-center p-12 text-secondary font-bold uppercase tracking-widest border border-dashed border-[#333] rounded">
                        No active work orders
                    </div>
                ) : orders.sort((a, b) => a.type === 'AUDIT' ? -1 : b.type === 'AUDIT' ? 1 : 0).map(order => {
                    const result = auditResult[order.id];

                    // ── Audit RESULT screen ─────────────────────
                    if (result) {
                        const isClean = result.status === 'CLEAN';
                        return (
                            <div key={order.id} className={`bg-primary rounded-xl border-l-8 p-6 shadow-lg ${isClean ? 'border-success' : 'border-danger'}`}>
                                <div className={`text-2xl font-black uppercase tracking-widest mb-4 ${isClean ? 'text-success' : 'text-danger'}`}>
                                    {isClean ? '✅ AUDIT CLEAN — ZONE VERIFIED' : '⚠ DISCREPANCY DETECTED'}
                                </div>
                                {!isClean && (
                                    <div className="space-y-3">
                                        {result.ghost?.length > 0 && (
                                            <div>
                                                <div className="text-xs text-danger font-bold uppercase tracking-widest mb-1">Ghost Slabs (in DB, physically missing):</div>
                                                <div className="flex flex-wrap gap-2">{result.ghost.map((id: string) => <span key={id} className="font-mono text-xs bg-danger/20 text-danger border border-danger/40 px-2 py-1 rounded">{id}</span>)}</div>
                                            </div>
                                        )}
                                        {result.phantom?.length > 0 && (
                                            <div>
                                                <div className="text-xs text-warning font-bold uppercase tracking-widest mb-1">Phantom Slabs (physically present, not in DB):</div>
                                                <div className="flex flex-wrap gap-2">{result.phantom.map((id: string) => <span key={id} className="font-mono text-xs bg-warning/20 text-warning border border-warning/40 px-2 py-1 rounded">{id}</span>)}</div>
                                            </div>
                                        )}
                                        <div className="text-xs text-secondary mt-2">Zone marked COMPROMISED on Admin Dashboard. Investigate and resolve physically before clearing.</div>
                                    </div>
                                )}
                            </div>
                        );
                    }

                    return (
                        <div key={order.id} className="bg-primary rounded-xl border-l-8 border-accent p-6 flex flex-col md:flex-row justify-between items-center shadow-lg">
                            <div className="w-full md:w-auto mb-6 md:mb-0">
                                <div className="text-xs font-bold text-secondary uppercase tracking-widest mb-1 flex items-center">
                                    {order.type} ORDER
                                    <span className="ml-3 px-2 py-0.5 bg-[#111] rounded text-accent">{order.slab.heat_number}</span>
                                </div>
                                <div className="text-4xl font-black font-mono tracking-wider mt-2 mb-4">
                                    {order.slab.heat_number || order.slab.id} <span className="text-sm font-sans text-secondary tracking-widest font-normal uppercase">Heat No.</span>
                                </div>

                                <div className="flex flex-col md:flex-row md:items-center space-y-4 md:space-y-0 md:space-x-4 bg-[#0B0C10] p-4 rounded border border-[#333]">
                                    {order.type === 'AUDIT' ? (
                                        <div className="flex-1 border-l-4 border-[#b498f3] pl-4">
                                            <div className="text-[10px] uppercase tracking-widest text-secondary mb-1">Ground Truth Cycle Count</div>
                                            <div className="font-bold text-[#b498f3] uppercase text-lg mb-2">{order.destination_zone?.name}</div>
                                            <div className="text-xs text-gray-400 mb-3 leading-relaxed">
                                                Walk the zone physically. Scan or enter each slab ID found, then submit.
                                            </div>
                                            <div className="flex gap-2 mb-2">
                                                <input
                                                    value={auditInput[order.id] || ''}
                                                    onChange={e => setAuditInput({ ...auditInput, [order.id]: e.target.value })}
                                                    onKeyDown={e => e.key === 'Enter' && addAuditScan(order.id)}
                                                    placeholder="Enter or scan slab ID..."
                                                    className="flex-1 bg-[#111] border border-[#b498f3]/40 rounded px-3 py-2 text-white font-mono text-sm focus:border-[#b498f3] focus:outline-none placeholder-[#444]"
                                                />
                                                <button onClick={() => addAuditScan(order.id)} className="px-3 py-2 bg-[#b498f3]/20 text-[#b498f3] border border-[#b498f3]/40 rounded hover:bg-[#b498f3]/30 flex items-center gap-1 text-xs font-bold">
                                                    <Plus size={14} />Add
                                                </button>
                                            </div>
                                            <div className="flex flex-wrap gap-1.5 mt-1">
                                                {(auditScans[order.id] || []).map((sid, idx) => (
                                                    <span key={idx} className="flex items-center gap-1 bg-[#b498f3]/10 text-[#b498f3] border border-[#b498f3]/30 px-2 py-0.5 rounded text-xs font-mono">
                                                        {sid}
                                                        <button onClick={() => removeAuditScan(order.id, idx)} className="hover:text-danger ml-1"><X size={10} /></button>
                                                    </span>
                                                ))}
                                                {(auditScans[order.id] || []).length === 0 && <span className="text-[10px] text-secondary italic">No slabs scanned yet</span>}
                                            </div>
                                        </div>
                                    ) : (
                                        <>
                                            <div className="flex-1">
                                                <div className="text-[10px] uppercase tracking-widest text-secondary mb-1">Pick Up From Sector</div>
                                                <div className="font-bold text-warning uppercase text-lg mb-1">{order.source_zone?.name || 'EXTERNAL ENTRY'}</div>
                                                {order.source_column && (
                                                    <div className="inline-block px-3 py-1 bg-[#111] text-accent border border-warning/30 rounded text-xs font-bold tracking-widest">Row {order.source_column.row_index + 1} &nbsp;•&nbsp; Col {order.source_column.col_index + 1}</div>
                                                )}
                                            </div>
                                            <ArrowRight className="text-secondary hidden md:block" />
                                            <div className="flex-1">
                                                <div className="text-[10px] uppercase tracking-widest text-secondary mb-1">Drop Off At Sector</div>
                                                <div className="font-bold text-success uppercase text-lg mb-1">{order.destination_zone?.name || 'TBD'}</div>
                                                {order.destination_column && (
                                                    <div className="inline-block px-3 py-1 bg-[#111] text-accent border border-success/30 rounded text-xs font-bold tracking-widest">Row {order.destination_column.row_index + 1} &nbsp;•&nbsp; Col {order.destination_column.col_index + 1}</div>
                                                )}
                                            </div>
                                        </>
                                    )}
                                </div>
                            </div>

                            <div className="w-full md:w-auto flex flex-col space-y-3">
                                {order.type === 'AUDIT' ? (
                                    <button
                                        onClick={() => submitAudit(order)}
                                        disabled={(auditScans[order.id] || []).length === 0}
                                        className="bg-[#b498f3] disabled:opacity-40 disabled:cursor-not-allowed text-black py-6 px-12 rounded shadow-[0_0_20px_rgba(180,152,243,0.3)] font-black uppercase tracking-widest text-xl hover:bg-opacity-90 active:scale-95 transition-all flex items-center justify-center cursor-pointer"
                                    >
                                        <ScanLine className="mr-3" size={28} /> Submit Verification
                                    </button>
                                ) : (
                                    <>
                                        <input
                                            type="text"
                                            placeholder="TYPE LAST 3 OF HEAT NO..."
                                            value={validations[order.id] || ''}
                                            onChange={(e) => setValidations({ ...validations, [order.id]: e.target.value.toUpperCase() })}
                                            className="bg-[#0B0C10] border border-[#333] text-center uppercase tracking-widest text-lg p-4 rounded focus:outline-none focus:border-accent text-white"
                                        />
                                        <button
                                            onClick={() => completeOrder(order.id)}
                                            disabled={validations[order.id] !== (order.slab.heat_number || order.slab.id).slice(-3).toUpperCase() || order.slab.quality_status === 'LOCKED_QA'}
                                            className="bg-success disabled:bg-[#1a221f] disabled:text-[#3a4f44] disabled:shadow-none text-white py-6 px-12 rounded shadow-[0_0_20px_rgba(46,139,87,0.3)] font-black uppercase tracking-widest text-xl hover:bg-opacity-90 active:scale-95 transition-all flex items-center justify-center cursor-pointer disabled:cursor-not-allowed"
                                        >
                                            {order.slab.quality_status === 'LOCKED_QA' ? 'QA HOLD ACTIVE' : <><CheckCircle2 className="mr-3" size={32} /> {order.type === 'SCARFING_EXIT' ? 'Complete Rework' : 'Confirm Move'}</>}
                                        </button>
                                    </>
                                )}
                                {rules.MODULE_QA_WORKFLOW === 'true' && order.type !== 'AUDIT' && (
                                    <>
                                        {order.slab.quality_status === 'LOCKED_QA' ? (
                                            <button onClick={() => toggleQALock(order.id, false)} className="bg-warning text-black py-4 px-12 rounded font-black uppercase tracking-widest hover:bg-opacity-90 transition-colors flex items-center justify-center hover:shadow-[0_0_20px_rgba(245,158,11,0.5)] active:scale-95">
                                                <AlertTriangle className="mr-2" /> Authorize Release
                                            </button>
                                        ) : (
                                            <button onClick={() => toggleQALock(order.id, true)} className="bg-transparent border border-warning text-warning py-4 px-12 rounded font-bold uppercase tracking-widest hover:bg-warning hover:text-black transition-colors flex items-center justify-center active:scale-95">
                                                Request LIMS QA
                                            </button>
                                        )}
                                    </>
                                )}
                                {/* Was a one-press "Flag Sector Null" that sent no reason, so every
                                    flagged order logged the same words and the next shift learned
                                    nothing from the log it was written for. */}
                                {flagging === order.id ? (
                                    <div className="flex flex-col gap-2 w-full max-w-md">
                                        <input
                                            autoFocus
                                            value={flagReason}
                                            onChange={e => setFlagReason(e.target.value)}
                                            placeholder="What stopped you? e.g. crane down, slab not where it should be"
                                            className="bg-[#0B0C10] border border-danger rounded px-4 py-3 text-white text-sm focus:outline-none placeholder-[#555]"
                                        />
                                        <div className="flex gap-2">
                                            <button
                                                disabled={!flagReason.trim()}
                                                onClick={() => {
                                                    completeOrder(order.id, true, flagReason.trim());
                                                    setFlagging(null); setFlagReason('');
                                                }}
                                                className="flex-1 bg-danger text-white py-3 rounded font-bold uppercase tracking-widest disabled:opacity-40">
                                                Send it back
                                            </button>
                                            <button
                                                onClick={() => { setFlagging(null); setFlagReason(''); }}
                                                className="px-6 border border-[#333] text-secondary rounded font-bold uppercase tracking-widest">
                                                Cancel
                                            </button>
                                        </div>
                                    </div>
                                ) : (
                                    <button
                                        onClick={() => { setFlagging(order.id); setFlagReason(''); }}
                                        className="bg-transparent border border-danger text-danger py-4 px-12 rounded font-bold uppercase tracking-widest hover:bg-danger hover:text-white transition-colors flex items-center justify-center hover:shadow-[0_0_20px_rgba(220,38,38,0.5)] active:scale-95"
                                    >
                                        <AlertTriangle className="mr-2" /> Can&apos;t do this
                                    </button>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};

export default TabletWorkspace;
