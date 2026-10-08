import React, { useState, useEffect, useCallback } from 'react';
import { Activity, ShieldAlert, CheckCircle, Wrench, X, Clock, MapPin, ChevronRight, TestTube, AlertTriangle } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { useSocket } from '../contexts/SocketContext';
import { symsFetch, symsError } from '../symsApi';
import { hasPermission } from '../../../../auth';

export const QADashboard = () => {
    const { token } = useAuth();
    const { socket } = useSocket();
    const [lockedSlabs, setLockedSlabs] = useState<any[]>([]);
    const [reworkActivities, setReworkActivities] = useState<any[]>([]);
    const [selectedSlab, setSelectedSlab] = useState<any | null>(null);
    const [selectedRework, setSelectedRework] = useState<string>('');
    const [loading, setLoading] = useState(false);
    const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null);

    const fetchLockedSlabs = useCallback(async () => {
        if (!token) return;
        try {
            const r = await symsFetch('http://localhost:4000/api/slabs?quality_status=LOCKED_QA', {
                headers: { Authorization: `Bearer ${token}` }
            });
            if (r.ok) setLockedSlabs(await r.json());
        } catch { }
    }, [token]);

    const fetchReworkActivities = useCallback(async () => {
        if (!token) return;
        try {
            const r = await symsFetch('http://localhost:4000/api/masters/rework', {
                headers: { Authorization: `Bearer ${token}` }
            });
            if (r.ok) {
                const acts = await r.json();
                setReworkActivities(acts.filter((a: any) => a.is_active));
            }
        } catch { }
    }, [token]);

    useEffect(() => {
        fetchLockedSlabs();
        fetchReworkActivities();

        if (socket) {
            socket.on('slab:updated', () => {
                fetchLockedSlabs();
            });
        }
        return () => {
            if (socket) socket.off('slab:updated');
        };
    }, [fetchLockedSlabs, fetchReworkActivities, socket]);

    // Both handlers used window.alert, and the release handler never looked at the
    // response — a refusal from the server (no scarfing space, the hold already lifted,
    // the role not allowed) resolved normally, so the slab silently stayed put and the
    // operator was told nothing. The outcome is now shown on the screen itself.
    const handleClearHold = async (slabId: string) => {
        setLoading(true);
        setNotice(null);
        try {
            const res = await symsFetch(`http://localhost:4000/api/workorders/${slabId}/qa-unlock`, {
                method: 'PUT',
            });
            if (res.ok) {
                setNotice({ tone: 'ok', text: 'Hold cleared. The slab can move again.' });
                setSelectedSlab(null);
                fetchLockedSlabs();
            } else {
                setNotice({ tone: 'bad', text: await symsError(res, 'Could not clear that hold.') });
            }
        } catch {
            setNotice({ tone: 'bad', text: 'Could not reach the yard. Nothing was changed.' });
        }
        setLoading(false);
    };

    const handleAssignRework = async (slabId: string) => {
        if (!selectedRework) {
            setNotice({ tone: 'bad', text: 'Pick a rework activity first.' });
            return;
        }
        setLoading(true);
        setNotice(null);
        try {
            const res = await symsFetch(`http://localhost:4000/api/workorders/${slabId}/rework`, {
                method: 'POST',
                body: JSON.stringify({ rework_activity_id: Number(selectedRework) }),
            });
            if (res.ok) {
                const body = await res.json().catch(() => null);
                const digs = Math.max((body?.work_orders?.length ?? 1) - 1, 0);
                setNotice({
                    tone: 'ok',
                    text: digs > 0
                        ? `Rework assigned. ${digs} slab(s) above it must be dug out first — those orders are on the operator worklist.`
                        : 'Rework assigned and the slab is routed to scarfing.',
                });
                setSelectedSlab(null);
                setSelectedRework('');
                fetchLockedSlabs();
            } else {
                setNotice({ tone: 'bad', text: await symsError(res, 'Could not assign that rework.') });
            }
        } catch {
            setNotice({ tone: 'bad', text: 'Could not reach the yard. Nothing was changed.' });
        }
        setLoading(false);
    };

    // Was `user?.role === 'Admin' || user?.role === 'Quality Inspector'` — standalone mSYMS
    // role names that the platform never produces, so this screen showed "Access Denied" to
    // everybody, super admin included. The platform expresses the same thing as a
    // permission code, which is also what the API is gated on, so the two now agree.
    const canSeeQuality = hasPermission('YRDQ');

    // Declaring the steel fit to move again is the one action here that carries its own
    // code. Assigning rework does not: it sends the slab to be made good rather than
    // passing it, and Quality owns that decision.
    const canReleaseHold = hasPermission('YRDU');

    if (!canSeeQuality) {
        return (
            <div className="flex flex-col items-center justify-center h-[80vh] text-center">
                <ShieldAlert size={64} className="text-danger mb-4" />
                <h2 className="text-2xl font-black text-white uppercase tracking-widest mb-2">Access Denied</h2>
                <p className="text-secondary">Your role does not carry Slab Yard quality access.</p>
            </div>
        );
    }

    return (
        <>
        {notice && (
            <div className={`mb-4 px-4 py-3 rounded border text-xs font-bold tracking-wide ${
                notice.tone === 'ok'
                    ? 'text-success bg-success/10 border-success/30'
                    : 'text-danger bg-danger/10 border-danger/30'
            }`}>
                {notice.text}
            </div>
        )}
        <div className="msyms-fill flex gap-6">
            <div className="flex-1 flex flex-col h-full bg-[#0B0C10] border border-[#222] rounded-xl overflow-hidden shadow-2xl">
                <div className="p-5 border-b border-[#222] flex items-center justify-between bg-[#050608]">
                    <div className="flex items-center">
                        <Activity size={24} className="text-warning mr-3" />
                        <div>
                            <h2 className="text-xl tracking-widest uppercase font-black text-white">QA Inspector Workspace</h2>
                            <p className="text-xs text-secondary mt-1">Manage slabs currently under Quality Assurance Hold.</p>
                        </div>
                    </div>
                    <div className="bg-warning/10 border border-warning/30 text-warning px-4 py-1.5 rounded text-xs font-bold uppercase tracking-widest">
                        {lockedSlabs.length} Pending
                    </div>
                </div>

                <div className="flex-1 overflow-y-auto p-4 space-y-3 thin-scrollbar">
                    {lockedSlabs.map(slab => (
                        <div key={slab.id}
                            onClick={() => setSelectedSlab(slab)}
                            className={`flex items-center justify-between p-4 rounded-lg border cursor-pointer transition-all ${selectedSlab?.id === slab.id ? 'bg-[#1a1c23] border-warning ring-1 ring-warning shadow-[0_0_15px_rgba(234,179,8,0.15)]' : 'bg-primary border-[#333] hover:border-warning/50'}`}>
                            <div className="flex items-center gap-4">
                                <div className="h-10 w-10 rounded-full bg-warning/20 border border-warning/50 flex items-center justify-center text-warning shrink-0">
                                    <ShieldAlert size={18} />
                                </div>
                                <div>
                                    <div className="font-black text-white uppercase tracking-widest text-sm flex items-center gap-2">
                                        {slab.heat_number}
                                        <span className="text-[10px] text-secondary font-mono font-normal">{slab.heat_number || slab.id}</span>
                                    </div>
                                    <div className="text-[10px] text-secondary mt-1 flex items-center gap-3 font-mono">
                                        <span className="flex items-center"><MapPin size={10} className="mr-1" />{slab.zone?.name || 'Unknown Zone'}</span>
                                        <span className="flex items-center"><Clock size={10} className="mr-1" />{new Date(slab.updated_at).toLocaleString()}</span>
                                    </div>
                                </div>
                            </div>
                            <ChevronRight size={18} className="text-secondary" />
                        </div>
                    ))}
                    {lockedSlabs.length === 0 && (
                        <div className="h-full flex flex-col items-center justify-center text-secondary opacity-50 py-20">
                            <CheckCircle size={48} className="mb-4 text-success" />
                            <div className="text-sm font-bold uppercase tracking-widest">No Active QA Holds</div>
                            <div className="text-xs mt-1">All slabs meet quality standards.</div>
                        </div>
                    )}
                </div>
            </div>

            {/* Side Panel: Detail View */}
            <div className="w-[400px] h-full flex flex-col">
                {selectedSlab ? (
                    <div className="flex-1 bg-primary border border-[#333] rounded-xl overflow-hidden flex flex-col shadow-2xl">
                        <div className="bg-gradient-to-r from-warning/20 to-[#0B0C10] p-6 border-b border-[#333] relative overflow-hidden">
                            <ShieldAlert size={120} className="absolute -right-6 -bottom-6 text-warning/10" />
                            <button onClick={() => setSelectedSlab(null)} className="absolute top-4 right-4 text-secondary hover:text-white"><X size={16} /></button>
                            <h3 className="text-2xl font-black text-white uppercase tracking-widest">{selectedSlab.heat_number}</h3>
                            <div className="text-warning text-xs font-bold tracking-widest uppercase mt-1 flex items-center">
                                <AlertTriangle size={12} className="mr-1" /> QA Hold Active
                            </div>
                        </div>

                        <div className="flex-1 p-6 space-y-6 overflow-y-auto thin-scrollbar">
                            <div>
                                <h4 className="text-[10px] uppercase font-bold tracking-widest text-secondary mb-3 flex items-center"><TestTube size={12} className="mr-2" /> Chemistry & Physical Data</h4>
                                <div className="grid grid-cols-2 gap-3">
                                    <div className="bg-[#0B0C10] p-3 rounded border border-[#222]">
                                        <div className="text-[9px] uppercase tracking-widest text-secondary mb-1">Dimensions</div>
                                        <div className="text-xs font-mono text-white">{selectedSlab.dimensions.length}x{selectedSlab.dimensions.width}x{selectedSlab.dimensions.thickness}</div>
                                    </div>
                                    <div className="bg-[#0B0C10] p-3 rounded border border-[#222]">
                                        <div className="text-[9px] uppercase tracking-widest text-secondary mb-1">Weight</div>
                                        <div className="text-xs font-mono text-white">{selectedSlab.weight_kg} kg</div>
                                    </div>
                                </div>
                                <div className="bg-[#0B0C10] p-3 rounded border border-[#222] mt-3">
                                    <div className="text-[9px] uppercase tracking-widest text-secondary mb-2">LIMS Data Profile</div>
                                    <pre className="text-[10px] text-gray-400 font-mono overflow-hidden break-words">{JSON.stringify(selectedSlab.chemistry_data, null, 2)}</pre>
                                </div>
                            </div>

                            <div className="border-t border-[#333] pt-6">
                                <h4 className="text-[10px] uppercase font-bold tracking-widest text-secondary mb-4 flex items-center"><Wrench size={12} className="mr-2" /> Rework Assessment</h4>
                                
                                {reworkActivities.length > 0 ? (
                                    <div className="space-y-4">
                                        <div>
                                            <label className="text-[10px] text-secondary uppercase font-bold mb-1.5 block">Select Rework Activity</label>
                                            <select 
                                                value={selectedRework} 
                                                onChange={e => setSelectedRework(e.target.value)}
                                                className="w-full bg-[#0B0C10] p-3 rounded text-white border border-[#333] focus:border-warning focus:outline-none text-sm appearance-none cursor-pointer">
                                                <option value="" disabled>-- Choose Rework Protocol --</option>
                                                {reworkActivities.map(a => (
                                                    <option key={a.id} value={a.id}>{a.name}</option>
                                                ))}
                                            </select>
                                        </div>
                                        <button 
                                            disabled={loading || !selectedRework}
                                            onClick={() => handleAssignRework(selectedSlab.id)}
                                            className="w-full flex items-center justify-center p-3 rounded bg-accent text-background font-bold uppercase tracking-widest text-xs hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed transition-opacity">
                                            <Wrench size={14} className="mr-2" />
                                            Assign Rework
                                        </button>
                                    </div>
                                ) : (
                                    <div className="text-xs text-danger bg-danger/10 p-3 rounded border border-danger/30">
                                        No active Rework Activities configured in Master Data. Cannot assign rework.
                                    </div>
                                )}
                            </div>
                        </div>

                        <div className="p-6 bg-[#050608] border-t border-[#333]">
                            {canReleaseHold ? (
                                <button
                                    disabled={loading}
                                    onClick={() => handleClearHold(selectedSlab.id)}
                                    className="w-full flex items-center justify-center p-3 rounded border border-success text-success font-bold uppercase tracking-widest text-xs hover:bg-success/10 disabled:opacity-50 transition-colors">
                                    <CheckCircle size={14} className="mr-2" />
                                    Clear Hold &amp; Authorize Release
                                </button>
                            ) : (
                                <div className="text-xs text-secondary bg-[#0d0d0d] p-3 rounded border border-[#222] text-center">
                                    Releasing a Met-Lab hold needs the Release QA Lock permission.
                                    You can still assign rework.
                                </div>
                            )}
                        </div>
                    </div>
                ) : (
                    <div className="flex-1 bg-primary border border-[#333] rounded-xl flex flex-col items-center justify-center text-secondary p-8 text-center shadow-inner">
                        <Activity size={48} className="opacity-20 mb-4" />
                        <h3 className="text-sm font-bold uppercase tracking-widest text-white mb-2">Select a Slab</h3>
                        <p className="text-xs leading-relaxed">Choose a locked slab from the list to view its chemistry data, assign a rework protocol, or authorize release.</p>
                    </div>
                )}
            </div>
        </div>
        </>
    );
};

export default QADashboard;
