import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useSocket } from '../contexts/SocketContext';
import { Settings2, Check, ExternalLink, Activity, AlertCircle, Trash2, Plus, ArrowRightToLine, ArrowLeftToLine, Maximize2, Minimize2, Search, X, Clock, MapPin, History, ShieldAlert, Flame } from 'lucide-react';
import { symsFetch } from '../symsApi';
import { hasPermission } from '../../../../auth';
import { PlantLinkBadge } from '../components/PlantLinkBadge';

export const Dashboard: React.FC = () => {
    const { token } = useAuth();
    const { socket, connected } = useSocket();

    // Was `user?.role === 'Admin'` — a standalone mSYMS role name the platform never
    // produces, so Edit Sectors was hidden from everybody including the super admin. Same
    // code the API gates the capacity write on, so the button and the endpoint agree.
    const canConfigure = hasPermission('YRDA');
    const [zones, setZones] = useState<any[]>([]);
    const [feedLogs, setFeedLogs] = useState<any[]>([]);
    const [workOrders, setWorkOrders] = useState<any[]>([]);
    const [isEditMode, setIsEditMode] = useState(false);
    const [isFeedOpen, setIsFeedOpen] = useState(false);
    const [isFullscreen, setIsFullscreen] = useState(false);
    const [rules, setRules] = useState<Record<string, string>>({});
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedSlab, setSelectedSlab] = useState<any | null>(null);
    const [slabHistory, setSlabHistory] = useState<any[]>([]);
    const [historyLoading, setHistoryLoading] = useState(false);
    const [editCaps, setEditCaps] = useState<Record<string, number>>({});
    const [toastMessage, setToastMessage] = useState<{ title: string, message: string, type: 'error' | 'success' } | null>(null);

    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === 'Escape' && isFullscreen) {
                setIsFullscreen(false);
                if (document.fullscreenElement) {
                    document.exitFullscreen().catch(err => console.log(err));
                }
            }
        };
        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [isFullscreen]);

    const formatStackName = (row: number, col: number) => {
        const format = rules.rule_nomenclature_format || 'R{row} - C{col}';
        return format.replace('{row}', String(row + 1)).replace('{col}', String(col + 1));
    };

    const fetchDashboard = async () => {
        if (!token) return;
        const resZ = await symsFetch('http://localhost:4000/api/zones', { headers: { Authorization: `Bearer ${token}` } });
        if (resZ.ok) setZones(await resZ.json());
        const resW = await symsFetch('http://localhost:4000/api/workorders', { headers: { Authorization: `Bearer ${token}` } });
        if (resW.ok) setWorkOrders(await resW.json());
        const resR = await symsFetch('http://localhost:4000/api/config', { headers: { Authorization: `Bearer ${token}` } });
        if (resR.ok) {
            const data = await resR.json();
            const r: Record<string, string> = {};
            data.forEach((d: any) => { r[d.key] = d.value; });
            setRules(r);
        }
        fetchFeed();
    };

    useEffect(() => {
        fetchDashboard();
        if (socket) {
            socket.on('workorder:new', fetchDashboard);
            socket.on('workorder:completed', () => {
                fetchDashboard();
                fetchFeed();
            });
            socket.on('slab:updated', fetchDashboard);
            socket.on('internal:slab_ingested', fetchDashboard);
            socket.on('zone:compromised', fetchDashboard);
            socket.on('zone:verified', fetchDashboard);
        }
        return () => {
            if (socket) {
                socket.off('workorder:new');
                socket.off('workorder:completed');
                socket.off('slab:updated');
                socket.off('internal:slab_ingested');
                socket.off('zone:compromised');
                socket.off('zone:verified');
            }
        };
    }, [socket, token]);

    const openSlabDetail = async (slab: any) => {
        setSelectedSlab(slab);
        setHistoryLoading(true);
        try {
            const r = await symsFetch(`http://localhost:4000/api/workorders/slab/${slab.id}/history`, { headers: { Authorization: `Bearer ${token}` } });
            if (r.ok) setSlabHistory(await r.json());
        } catch { }
        setHistoryLoading(false);
    };

    const handleSaveColumn = async (columnId: string, newCap: number) => {
        if (newCap < 0) return;
        try {
            await symsFetch(`http://localhost:4000/api/zones/columns/${columnId}`, {
                method: 'PUT',
                headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ capacity_limit: newCap })
            });
            const newCaps = { ...editCaps };
            delete newCaps[columnId];
            setEditCaps(newCaps);
            fetchDashboard();
        } catch (e) { }
    };

    // Deploy Full Audit, Quick Audit and High-Velocity Audit used to live here. All three
    // posted to endpoints that have never existed on this API — /workorders/audit,
    // /workorders/reconciliation/quick and /workorders/reconciliation/high-velocity — so
    // every one of them 404'd and told the operator an audit had been deployed anyway.
    //
    // They are gone rather than reimplemented: what a cycle-count audit should actually do
    // to a yard (which slabs it covers, whether the zone is frozen while it runs, what
    // happens when the count disagrees with the record) is a decision about how the plant
    // works, not a gap in the wiring. The schema is ready for it when that is settled —
    // sp_syms_work_order takes an AUDIT order type and sp_syms_slab has is_virtual for the
    // row such an order hangs off.

    const fetchFeed = async () => {
        if (!token) return;
        const res = await symsFetch('http://localhost:4000/api/workorders/activity-feed', { headers: { Authorization: `Bearer ${token}` } });
        if (res.ok) setFeedLogs(await res.json());
    };

    const activeAlerts = workOrders.filter(w => w.status === 'FLAGGED');

    const getTheme = (type: string) => {
        if (type === 'SCARFING') return 'text-warning shadow-warning border-warning/30';
        if (type === 'DISPATCH') return 'text-[#87ceeb] shadow-[#87ceeb] border-[#87ceeb]/30';
        return 'text-accent shadow-accent border-[#222]';
    };

    const renderLogString = (log: any) => {
        if (log.order_type === 'INBOUND') return `Allocated to ${log.to_zone_name} (${log.to_col_desc})`;
        if (log.order_type === 'MOVE' || log.order_type === 'SCARFING_EXIT') return `Moved from ${log.from_zone_name || 'unknown'} to ${log.to_zone_name}`;
        if (log.order_type === 'DISPATCH') return `Loaded for dispatch from ${log.from_zone_name}`;
        return `Completed ${log.order_type}`;
    };

    const toggleFullscreen = () => {
        setIsFullscreen(!isFullscreen);
        if (!isFullscreen) { 
            document.documentElement.requestFullscreen().catch(e => console.log(e)); 
        } else {
            if (document.fullscreenElement) {
                document.exitFullscreen().catch(e => console.log(e));
            }
        }
    };

    return (
        <div className={`text-white w-full flex flex-col overflow-hidden transition-all duration-500 ease-in-out ${isFullscreen ? 'fixed inset-0 z-[100] bg-[#050608] px-4 py-4' : 'mx-auto h-screen px-6 py-4'}`}>

            {/* Header */}
            <div className="flex justify-between items-center mb-4 border-b border-[#333] pb-4 shrink-0">
                <h2 className="text-xl tracking-widest uppercase font-bold flex items-center">
                    Sector Grid Overview
                    {/* Said LIVE DATA STREAM unconditionally, in green, whether or not anything
                        was reaching the API. It now reflects the same health probe the link
                        strip above the screen uses. */}
                    <span className={`ml-4 text-[10px] font-normal border px-2 py-1 rounded tracking-widest ${connected ? 'text-success border-success bg-success/10' : 'text-danger border-danger bg-danger/10'}`}>
                        {connected ? 'LIVE' : 'NOT UPDATING'}
                    </span>
                    <PlantLinkBadge />
                </h2>
                <div className="flex items-center space-x-3">
                    <div className="relative">
                        <Search size={13} className="absolute left-3 top-1/2 -translate-y-1/2 text-secondary" />
                        <input value={searchQuery} onChange={e => setSearchQuery(e.target.value)} placeholder="Search heat / slab ID..."
                            className="pl-8 pr-3 py-1.5 bg-[#0B0C10] border border-[#333] rounded text-white text-xs focus:border-accent focus:outline-none placeholder-[#555] w-52" />
                        {searchQuery && <button onClick={() => setSearchQuery('')} className="absolute right-2 top-1/2 -translate-y-1/2 text-secondary hover:text-white"><X size={12} /></button>}
                    </div>
                    <button onClick={toggleFullscreen} className="flex items-center px-3 py-1.5 text-xs font-bold tracking-widest uppercase rounded transition-colors bg-[#0B0C10] border border-[#333] text-secondary hover:text-white">
                        {isFullscreen ? <Minimize2 size={14} className="mr-1.5" /> : <Maximize2 size={14} className="mr-1.5" />}
                        {isFullscreen ? 'Exit' : 'Fullscreen'}
                    </button>
                    <button onClick={() => setIsFeedOpen(!isFeedOpen)} className="flex items-center px-3 py-1.5 text-xs font-bold tracking-widest uppercase rounded transition-colors bg-[#0B0C10] border border-[#333] text-secondary hover:text-white">
                        {isFeedOpen ? <ArrowRightToLine size={14} className="mr-1.5" /> : <ArrowLeftToLine size={14} className="mr-1.5" />}
                        {isFeedOpen ? 'Collapse' : 'Feed'}
                    </button>
                    {canConfigure && (
                        <button onClick={() => setIsEditMode(!isEditMode)} className={`flex items-center px-3 py-1.5 text-xs font-bold tracking-widest uppercase rounded transition-colors ${isEditMode ? 'bg-accent text-background border-accent' : 'bg-primary border border-gray-600 text-secondary hover:text-white hover:border-accent'}`}>
                            <Settings2 size={14} className="mr-1.5" />{isEditMode ? 'Done Editing' : 'Edit Sectors'}
                        </button>
                    )}
                </div>
            </div>

            {/* Top Utilization Banner */}
            <div className="w-full bg-[#0B0C10] border border-[#222] rounded-xl p-4 mb-6 shrink-0 flex items-center shadow-lg overflow-x-auto thin-scrollbar">
                <div className="flex items-center text-secondary uppercase tracking-widest text-xs font-bold border-r border-[#333] pr-6 mr-6 shrink-0">
                    <Activity size={18} className="mr-2 text-accent" /> Live Utilization
                </div>
                <div className="flex items-center space-x-12 px-2">
                    {zones.map(zone => {
                        const cols = zone.columns || [];
                        const totalCap = cols.reduce((a: number, c: any) => a + c.capacity_limit, 0);
                        const used = cols.reduce((a: number, c: any) => a + (c.slabs?.length || 0), 0);
                        const ratio = totalCap === 0 ? 0 : Math.round((used / totalCap) * 100);
                        return (
                            <div key={zone.id} className="flex items-center space-x-4 shrink-0 transition-opacity hover:opacity-80">
                                <div className="relative w-16 h-16 flex items-center justify-center shadow-inner rounded-full p-1 bg-[#050608]">
                                    <svg className="w-full h-full transform -rotate-90" viewBox="0 0 100 100">
                                        <circle cx="50" cy="50" r="40" fill="transparent" stroke="#111" strokeWidth="8" />
                                        <circle cx="50" cy="50" r="40" fill="transparent" stroke="#2E8B57" strokeWidth="8" strokeDasharray={`${ratio * 2.51} 251`} className="transition-all duration-1000 ease-out" />
                                    </svg>
                                    <div className="absolute font-black tracking-wider text-[11px] text-white bg-[#111] px-1.5 py-0.5 rounded shadow-sm">{ratio}%</div>
                                </div>
                                <div className="flex flex-col justify-center">
                                    <div className="text-sm font-black uppercase tracking-widest text-white mb-1 truncate">{zone.name}</div>
                                    <div className="text-[10px] uppercase font-bold tracking-widest text-secondary bg-[#1a1c23] inline-block px-2 py-0.5 rounded">{used} / {totalCap} Slabs</div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            </div>

            {/* Main Work Area */}
            <div className="flex flex-row gap-6 flex-1 min-h-0 overflow-hidden">

                {/* Zone Grid Panel */}
                <div className="flex-1 h-full overflow-y-auto pr-4 thin-scrollbar">
                    <div className={isFullscreen ? "columns-1 xl:columns-2 3xl:columns-3 gap-6 pb-20" : "space-y-6 pb-20"}>
                        {zones.map(zone => {
                            const colsCount = zone.layout_config?.cols || 1;
                            const themeClasses = getTheme(zone.type);
                            const isCompromised = zone.is_compromised === true;
                            const isLinear = zone.type !== 'STORAGE';

                            return (
                                <div key={zone.id} className={`break-inside-avoid mb-6 bg-[#0B0C10] border p-4 rounded-xl shadow-2xl transition-all ${isCompromised ? 'border-danger animate-pulse-border' : themeClasses.split(' ').find(c => c.startsWith('border-')) || 'border-[#333]'}`}>

                                    {/* Zone Header */}
                                    <div className={`flex justify-between items-center ${isLinear ? 'mb-3' : 'mb-5 border-b border-[#222] pb-3'}`}>
                                        <div className={`text-lg uppercase font-black tracking-widest flex items-center ${isCompromised ? 'text-danger' : themeClasses.split(' ')[0]}`}>
                                            {zone.name}
                                            <span className="ml-4 text-[9px] text-secondary font-mono border border-[#333] rounded px-2 py-0.5 bg-primary">{zone.type}</span>
                                            {isCompromised && (
                                                <span className="ml-3 text-[10px] font-bold text-danger border border-danger/50 bg-danger/10 px-2 py-0.5 rounded flex items-center">
                                                    <ShieldAlert size={11} className="mr-1" />COMPROMISED
                                                </span>
                                            )}
                                        </div>
                                    </div>

                                    {/* ── LINEAR zones (Scarfing / Dispatch / Shuffling): compact horizontal chip row ── */}
                                    {isLinear && (
                                        <div className="flex items-start gap-3 flex-wrap">
                                            {zone.columns?.map((col: any) => {
                                                const sortedSlabs = (col.slabs || []).slice().sort((a: any, b: any) => (a.stack_index || 0) - (b.stack_index || 0));
                                                return (
                                                    <div key={col.id} className="flex-1 min-w-0">
                                                        <div className="text-[9px] text-secondary font-mono mb-1">{sortedSlabs.length} / {col.capacity_limit}</div>
                                                        <div className="flex items-center gap-1.5 flex-wrap min-h-[36px]">
                                                            {sortedSlabs.length === 0 ? (
                                                                <div className="flex-1 h-9 border border-dashed border-[#222] rounded flex items-center justify-center text-[10px] text-[#333] uppercase tracking-widest">Empty</div>
                                                            ) : sortedSlabs.map((slab: any) => {
                                                                const chem = slab.chemistry_data as any || {};
                                                                const isDefect = chem.grade?.includes('DEFECT');
                                                                const isStale = slab.stale_flagged === true;
                                                                const isQALocked = slab.quality_status === 'LOCKED_QA';
                                                                const matchesSearch = searchQuery && (slab.heat_number?.toLowerCase().includes(searchQuery.toLowerCase()) || slab.id?.toLowerCase().includes(searchQuery.toLowerCase()));
                                                                let chipColor = 'bg-gradient-to-r from-[#216e54] to-accent text-[#0B0C10]';
                                                                if (isDefect) chipColor = 'bg-gradient-to-r from-danger to-[#8B0000] text-white';
                                                                else if (isQALocked) chipColor = 'bg-gradient-to-r from-warning/60 to-warning text-black';
                                                                else if (isStale) chipColor = 'bg-gradient-to-r from-[#6b4c00] to-[#b87d00] text-white';
                                                                return (
                                                                    <div key={slab.id} onClick={() => openSlabDetail(slab)}
                                                                        title={`${slab.id}\n${slab.heat_number}${isStale ? '\n⚠ STALE' : ''}${isQALocked ? '\n🔒 QA HOLD' : ''}`}
                                                                        className={`h-9 px-3 rounded flex items-center gap-1.5 cursor-pointer hover:brightness-125 transition-all shadow border shrink-0 ${chipColor} ${isQALocked ? 'border-warning' : 'border-black/30'} ${matchesSearch ? 'ring-2 ring-white scale-105 animate-pulse' : ''}`}>
                                                                        {isStale && <Clock size={9} />}
                                                                        {isQALocked && <span className="text-[9px]">🔒</span>}
                                                                        <span className="font-black text-[11px] font-mono tracking-widest">{slab.heat_number || slab.id}</span>
                                                                        <span className="text-[9px] opacity-70 hidden sm:inline">{slab.heat_number}</span>
                                                                    </div>
                                                                );
                                                            })}
                                                        </div>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    )}

                                    {/* ── STORAGE zones: vertical stack pit grid ── */}
                                    {!isLinear && (
                                        <div className="grid gap-3" style={{ gridTemplateColumns: `repeat(${colsCount}, minmax(0, 1fr))` }}>
                                            {zone.columns?.map((col: any) => {
                                                const isEditing = editCaps[col.id] !== undefined;
                                                const draftVal = isEditing ? editCaps[col.id] : col.capacity_limit;
                                                const sortedSlabs = (col.slabs || []).slice().sort((a: any, b: any) => (a.stack_index || 0) - (b.stack_index || 0));

                                                if (col.capacity_limit === 0 && !isEditMode) {
                                                    return <div key={col.id} className="bg-transparent border border-dashed border-[#222] flex items-center justify-center text-[#444] text-[10px] uppercase rounded h-64">Disabled</div>;
                                                }

                                                return (
                                                    <div key={col.id} className={`p-2 rounded border transition-all relative flex flex-col ${isFullscreen ? 'h-[180px]' : 'h-[256px]'} shrink-0 ${col.capacity_limit === 0 ? 'bg-[#1a1111] border-danger/50' : 'bg-primary border-[#333]'} ${isEditMode ? 'ring-2 ring-accent/30 bg-[#161f1c]' : ''}`}>
                                                        <div className="flex justify-between items-center mb-2 px-1">
                                                            <div className="text-[10px] uppercase font-bold tracking-widest text-[#888]">{formatStackName(col.row_index, col.col_index)}</div>
                                                            {isEditMode ? (
                                                                <div className="flex items-center space-x-1">
                                                                    {col.capacity_limit > 0 ? (
                                                                        <>
                                                                            <input type="number" min="0" value={draftVal} onChange={e => setEditCaps({ ...editCaps, [col.id]: parseInt(e.target.value) || 0 })}
                                                                                className="w-10 bg-black border border-gray-600 rounded text-accent tracking-widest font-black text-center py-0.5 text-[10px] focus:outline-none" />
                                                                            {isEditing && <button onClick={() => handleSaveColumn(col.id, draftVal)} className="text-success hover:bg-success/20 p-1 rounded border border-success"><Check size={12} /></button>}
                                                                            <button onClick={() => handleSaveColumn(col.id, 0)} className="text-danger hover:bg-danger/20 p-1 rounded ml-1 bg-[#1a1111] border border-danger/50"><Trash2 size={12} /></button>
                                                                        </>
                                                                    ) : (
                                                                        <button onClick={() => handleSaveColumn(col.id, 10)} className="text-success hover:bg-success/20 p-1 rounded bg-[#111a11] border border-success/50 flex items-center"><Plus size={12} className="mr-1" />ENABLE</button>
                                                                    )}
                                                                </div>
                                                            ) : (
                                                                <div className={`text-[10px] font-mono tracking-widest ${sortedSlabs.length >= col.capacity_limit ? 'text-danger' : 'text-success'}`}>{sortedSlabs.length} / {col.capacity_limit}</div>
                                                            )}
                                                        </div>

                                                        {/* Stack Pit */}
                                                        <div className="flex-1 w-full bg-[#050608] border border-[#1a1a1a] rounded flex flex-col-reverse justify-start p-1 overflow-y-auto overflow-x-hidden relative shadow-[inset_0_4px_10px_rgba(0,0,0,0.5)] thin-scrollbar">
                                                            {col.capacity_limit === 0 ? (
                                                                <div className="absolute inset-0 flex items-center justify-center -rotate-90 text-[10px] tracking-widest font-bold text-danger/30 uppercase">DEAD SPACE</div>
                                                            ) : sortedSlabs.length === 0 ? (
                                                                <div className="absolute inset-0 flex flex-col justify-end">
                                                                    {[...Array(col.capacity_limit)].map((_, idx) => (
                                                                        <div key={idx} style={{ height: `${100 / col.capacity_limit}%` }} className="w-full mb-[2px] border-b border-dashed border-[#1a1a1a] opacity-30" />
                                                                    ))}
                                                                </div>
                                                            ) : (
                                                                sortedSlabs.map((slab: any) => {
                                                                    const chem = slab.chemistry_data as any || {};
                                                                    const isDefect = chem.grade?.includes('DEFECT');
                                                                    const isStale = slab.stale_flagged === true;
                                                                    const isQALocked = slab.quality_status === 'LOCKED_QA';
                                                                    const isHot = slab.temperature_state === 'HOT';
                                                                    const matchesSearch = searchQuery && (slab.heat_number?.toLowerCase().includes(searchQuery.toLowerCase()) || slab.id?.toLowerCase().includes(searchQuery.toLowerCase()));
                                                                    let bgClass = 'bg-gradient-to-r from-[#216e54] to-accent text-white drop-shadow-[0_1px_1px_rgba(0,0,0,0.8)] font-bold';
                                                                    if (isDefect) bgClass = 'bg-gradient-to-r from-danger to-[#8B0000] text-white';
                                                                    else if (isQALocked) bgClass = 'bg-gradient-to-r from-warning/60 to-warning text-black';
                                                                    else if (isStale) bgClass = 'bg-gradient-to-r from-[#6b4c00] to-[#b87d00] text-white font-bold drop-shadow-[0_1px_1px_rgba(0,0,0,0.8)]';
                                                                    
                                                                    const tooltipText = `ID: ${slab.id}\nHeat: ${slab.heat_number}\nGrade: ${chem.grade || 'Standard'}\nTarget Reheat: ${slab.target_reheat_temp ? slab.target_reheat_temp + '°C' : 'N/A'}\nRolling Width: ${slab.rolling_width || 'N/A'}mm\nCast Time: ${slab.cast_time ? new Date(slab.cast_time).toLocaleString() : 'N/A'}\n${isHot ? '🔥 HOT CHARGE READY\n' : ''}${isStale ? '⚠ STALE SLAB' : ''}${isQALocked ? '🔒 QA HOLD' : ''}`;

                                                                    return (
                                                                        <div key={slab.id}
                                                                            style={{ height: `${100 / col.capacity_limit}%` }}
                                                                            onClick={() => openSlabDetail(slab)}
                                                                            className={`w-full mb-[3px] rounded flex items-center justify-center shrink-0 shadow-[inset_0_-2px_5px_rgba(0,0,0,0.6)] border cursor-pointer hover:brightness-125 transition-all ${bgClass} ${isQALocked ? 'border-warning' : isHot ? 'border-[#ff4500] shadow-[0_0_8px_rgba(255,69,0,0.8)] animate-pulse' : 'border-[#000]'} ${matchesSearch ? 'ring-2 ring-white scale-[1.03] z-10 animate-pulse' : ''}`}
                                                                            title={tooltipText}>
                                                                            <div className="font-black text-[11px] tracking-widest font-mono select-none overflow-hidden flex items-center gap-1">
                                                                                {isHot && <Flame size={9} className="text-[#ff4500]" />}
                                                                                {isStale && <Clock size={8} />}
                                                                                {isQALocked && <span className="text-[8px]">🔒</span>}
                                                                                {slab.heat_number || slab.id}
                                                                            </div>
                                                                        </div>
                                                                    );
                                                                })
                                                            )}
                                                        </div>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    )}

                                </div>
                            );
                        })}
                    </div>
                </div>

                {/* Collapsible Operations Sidebar */}
                <div className={`h-full bg-primary border-[#333] rounded-xl flex flex-col shrink-0 shadow-2xl transition-all duration-300 ease-in-out ${isFeedOpen ? 'w-[320px] opacity-100 border' : 'w-0 opacity-0 border-0 overflow-hidden'}`}>
                    {activeAlerts.length > 0 && (
                        <div className="bg-[#1a1111] border-b border-danger/30 p-4 animate-pulse shrink-0">
                            <h3 className="uppercase tracking-widest text-[10px] text-danger font-bold mb-3 flex items-center"><AlertCircle size={12} className="mr-2" /> Flagged Exceptions</h3>
                            <div className="space-y-2 max-h-32 overflow-y-auto thin-scrollbar">
                                {activeAlerts.map(alert => (
                                    <div key={alert.id} className="text-[10px] font-mono text-danger/80 bg-black/40 p-2 rounded">
                                        [FLAG] Order <span className="text-white">{alert.id.slice(-4)}</span> issue.
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                    <div className="p-4 border-b border-[#222] bg-[#0c1015] shrink-0">
                        <h3 className="uppercase tracking-widest text-[10px] text-accent font-bold flex items-center"><ExternalLink size={12} className="mr-2" /> Yard Activity Feed</h3>
                    </div>
                    <div className="flex-1 overflow-y-auto p-4 space-y-4 thin-scrollbar bg-[#0B0C10]/50 scroll-smooth">
                        {feedLogs.length === 0 ? (
                            <div className="text-secondary text-xs italic opacity-50 px-2 mt-4 text-center">Listening for movements...</div>
                        ) : feedLogs.map(log => (
                            <div key={log.id} className="flex relative border-l border-[#333]/50 pl-4 py-2 hover:border-accent transition-colors group">
                                <div className="absolute w-2.5 h-2.5 rounded-full bg-[#444] -left-[5.5px] top-3 group-hover:bg-accent transition-colors shadow-[0_0_8px_rgba(46,139,87,0.5)]" />
                                <div className="flex-1">
                                    <div className="text-[10px] tracking-widest text-gray-500 mb-1.5 flex justify-between items-center">
                                        <span>{new Date(log.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                                        <span className="font-mono text-white text-xs font-bold bg-accent/20 border border-accent/40 px-1.5 py-0.5 rounded">#{log.slab?.heat_number || log.slab_id.slice(-6)}</span>
                                    </div>
                                    <div className="text-xs text-gray-300 font-medium leading-relaxed">{renderLogString(log)}</div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {/* Slab Detail Slide Panel */}
            {selectedSlab && (
                <div className="fixed inset-0 z-[200] flex justify-end" onClick={() => setSelectedSlab(null)}>
                    <div className="w-[400px] h-full bg-[#0B0C10] border-l border-[#333] flex flex-col shadow-2xl" onClick={e => e.stopPropagation()}>
                        <div className="flex items-center justify-between px-5 py-4 border-b border-[#222] shrink-0">
                            <div>
                                <div className="text-xs text-secondary uppercase tracking-widest mb-0.5">Slab Detail</div>
                                <div className="font-black text-white font-mono text-xl">{selectedSlab.heat_number || selectedSlab.id}</div>
                            </div>
                            <button onClick={() => setSelectedSlab(null)} className="p-2 rounded hover:bg-[#222] text-secondary hover:text-white"><X size={18} /></button>
                        </div>
                        <div className="flex-1 overflow-y-auto p-5 space-y-5 thin-scrollbar">
                            <div>
                                <div className="text-[10px] text-accent uppercase tracking-widest font-bold mb-2 flex items-center"><MapPin size={11} className="mr-1.5" />Identity &amp; Location</div>
                                <div className="bg-primary rounded-lg border border-[#222] divide-y divide-[#1a1a1a]">
                                    {[
                                        ['Heat Number', selectedSlab.heat_number],
                                        ['Grade Code', selectedSlab.grade_code || (selectedSlab.chemistry_data as any)?.grade || '—'],
                                        ['Weight', selectedSlab.weight_kg ? `${selectedSlab.weight_kg.toLocaleString()} kg` : '—'],
                                        ['Status', selectedSlab.status],
                                        ['QA Status', selectedSlab.quality_status],
                                        ['Stale Flag', selectedSlab.stale_flagged ? '⚠ YES' : 'No'],
                                    ].map(([label, val]) => (
                                        <div key={label} className="flex justify-between items-center px-4 py-2">
                                            <span className="text-[10px] text-secondary uppercase tracking-wider">{label}</span>
                                            <span className="text-xs font-mono text-white">{val as string}</span>
                                        </div>
                                    ))}
                                </div>
                            </div>
                            {selectedSlab.chemistry_data && Object.keys(selectedSlab.chemistry_data as any).length > 0 && (
                                <div>
                                    <div className="text-[10px] text-accent uppercase tracking-widest font-bold mb-2 flex items-center"><span className="mr-1.5">⚗</span>Chemistry Data</div>
                                    <div className="bg-primary rounded-lg border border-[#222] divide-y divide-[#1a1a1a]">
                                        {Object.entries(selectedSlab.chemistry_data as any).map(([k, v]) => (
                                            <div key={k} className="flex justify-between items-center px-4 py-2">
                                                <span className="text-[10px] text-secondary uppercase tracking-wider">{k}</span>
                                                <span className="text-xs font-mono text-white">{String(v)}</span>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            )}
                            <div>
                                <div className="text-[10px] text-accent uppercase tracking-widest font-bold mb-2 flex items-center"><History size={11} className="mr-1.5" />Movement History</div>
                                {historyLoading ? (
                                    <div className="text-secondary text-xs italic text-center py-4">Loading history...</div>
                                ) : slabHistory.length === 0 ? (
                                    <div className="text-secondary text-xs italic text-center py-4">No movements recorded yet.</div>
                                ) : (
                                    <div className="space-y-2">
                                        {slabHistory.map((log) => (
                                            <div key={log.id} className="relative border-l-2 border-[#333] pl-4 ml-2 pb-2 last:border-transparent">
                                                <div className="absolute -left-[5px] top-1 w-2.5 h-2.5 rounded-full bg-accent border-2 border-[#0B0C10]" />
                                                <div className="text-[9px] text-secondary font-mono mb-0.5">{new Date(log.timestamp).toLocaleString()}</div>
                                                <div className="text-xs text-white font-medium">
                                                    <span className="text-secondary">{log.from_zone_name || 'External'}</span>
                                                    {log.from_col_desc && <span className="text-secondary"> {log.from_col_desc}</span>}
                                                    <span className="text-accent mx-2">→</span>
                                                    <span>{log.to_zone_name || '—'}</span>
                                                    {log.to_col_desc && <span className="text-secondary"> {log.to_col_desc}</span>}
                                                </div>
                                                <div className="flex items-center gap-2 mt-0.5">
                                                    <span className="text-[9px] bg-[#222] text-secondary px-1.5 py-0.5 rounded uppercase tracking-wider">{log.order_type}</span>
                                                    {log.performed_name && <span className="text-[9px] text-secondary">by {log.performed_name}</span>}
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Legend */}
            <div className="shrink-0 flex items-center gap-4 pt-2 pb-1 border-t border-[#111] text-[9px] text-secondary uppercase tracking-widest">
                <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-gradient-to-r from-[#216e54] to-accent inline-block" />Normal</span>
                <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-gradient-to-r from-[#6b4c00] to-[#b87d00] inline-block" /><Clock size={9} className="inline" /> Stale</span>
                <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-gradient-to-r from-warning/60 to-warning inline-block" />🔒 QA Hold</span>
                <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-gradient-to-r from-danger to-[#8B0000] inline-block" />Defect</span>
                <span className="flex items-center gap-1 ml-auto italic opacity-60">Click any slab to view detail &amp; history</span>
            </div>

            <style dangerouslySetInnerHTML={{
                __html: `
               body { overflow: hidden; }
               .thin-scrollbar::-webkit-scrollbar { width: 4px; height: 4px; }
               .thin-scrollbar::-webkit-scrollbar-track { background: transparent; }
               .thin-scrollbar::-webkit-scrollbar-thumb { background: #333; border-radius: 4px; }
               .thin-scrollbar::-webkit-scrollbar-thumb:hover { background: #2E8B57; }
               @keyframes pulse-border { 0%, 100% { border-color: rgba(220,38,38,0.4); } 50% { border-color: rgba(220,38,38,1); } }
               .animate-pulse-border { animation: pulse-border 2s ease-in-out infinite; }
            `}} />
            {/* TOAST NOTIFICATION */}
            {toastMessage && (
                <div className={`fixed bottom-6 right-6 p-4 rounded shadow-lg border z-[300] min-w-[300px] transition-all transform ${toastMessage.type === 'error' ? 'bg-[#1a0f0f] border-danger text-danger' : 'bg-[#0a1a10] border-accent text-accent'}`}>
                    <div className="flex justify-between items-start">
                        <div>
                            <div className="font-bold uppercase tracking-widest text-xs mb-1">{toastMessage.title}</div>
                            <div className="text-sm opacity-90">{toastMessage.message}</div>
                        </div>
                        <button onClick={() => setToastMessage(null)}><X size={16}/></button>
                    </div>
                </div>
            )}

        </div>
    );
};

export default Dashboard;
