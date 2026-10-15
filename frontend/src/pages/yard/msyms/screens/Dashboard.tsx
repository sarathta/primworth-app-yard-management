import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useSocket } from '../contexts/SocketContext';
import { Settings2, ExternalLink, Activity, AlertCircle, ArrowRightToLine, ArrowLeftToLine, Maximize2, Minimize2, Search, X, Clock, MapPin, History, ShieldAlert } from 'lucide-react';
import { symsFetch } from '../symsApi';
import { hasPermission } from '../../../../auth';
import { useYardMaterial } from '../../components/MaterialGate';
import { PlantLinkBadge } from '../components/PlantLinkBadge';
import { ZoneFormation } from '../components/ZoneFormation';
import { FORMATION, asFormation, defaultFormation } from '../../formations';
import { pieceFrame, pieceShape } from '../../materialShape';

export const Dashboard: React.FC = () => {
    const { token } = useAuth();
    const { socket, connected } = useSocket();
    const { material } = useYardMaterial();

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
        return format.replace('{row}', String(row)).replace('{col}', String(col));
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
                        <input value={searchQuery} onChange={e => setSearchQuery(e.target.value)} placeholder={`Search heat / ${material.id_label}...`}
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
                                    <div className="text-[10px] uppercase font-bold tracking-widest text-secondary bg-[#1a1c23] inline-block px-2 py-0.5 rounded">{used} / {totalCap} {material.plural}</div>
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
                    {zones.length === 0 && (
                        <div className="border border-dashed border-[#333] rounded-xl p-10 text-center text-secondary text-sm">
                            <p>No areas in this facility accept {material.plural.toLowerCase()} yet.</p>
                            <p className="mt-2 text-[11px] uppercase tracking-widest text-[#666]">
                                {FORMATION[defaultFormation(material.type_key)].label} — {FORMATION[defaultFormation(material.type_key)].rule}
                            </p>
                        </div>
                    )}
                    <div className={isFullscreen ? "columns-1 xl:columns-2 3xl:columns-3 gap-6 pb-20" : "space-y-6 pb-20"}>
                        {zones.map(zone => {
                            const colsCount = zone.layout_config?.cols || 1;
                            const themeClasses = getTheme(zone.type);
                            const isCompromised = zone.is_compromised === true;
                            const isLinear = zone.type !== 'STORAGE';
                            const formation = asFormation(zone.storage_style);

                            return (
                                <div key={zone.id} className={`break-inside-avoid mb-6 bg-[#0B0C10] border p-4 rounded-xl shadow-2xl transition-all ${isCompromised ? 'border-danger animate-pulse-border' : themeClasses.split(' ').find(c => c.startsWith('border-')) || 'border-[#333]'}`}>

                                    {/* Zone Header */}
                                    <div className={`flex justify-between items-center ${isLinear ? 'mb-3' : 'mb-5 border-b border-[#222] pb-3'}`}>
                                        <div className={`text-lg uppercase font-black tracking-widest flex items-center ${isCompromised ? 'text-danger' : themeClasses.split(' ')[0]}`}>
                                            {zone.name}
                                            <span className="ml-4 text-[9px] text-secondary font-mono border border-[#333] rounded px-2 py-0.5 bg-primary">{zone.type}</span>
                                            {!isLinear && <span className="ml-2 text-[9px] text-accent font-mono border border-accent/40 rounded px-2 py-0.5 bg-accent/10">{FORMATION[formation].label}</span>}
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
                                                                const shape = pieceShape(slab.material_type || material.type_key);
                                                                const onPiece = shape === 'slab' || shape === 'plate' || shape === 'billet';
                                                                return (
                                                                    <div key={slab.id} onClick={() => openSlabDetail(slab)}
                                                                        title={`${slab.heat_number || slab.id}${isStale ? '\nStale' : ''}${isQALocked ? '\nQA hold' : ''}`}
                                                                        className={`${pieceFrame(shape)} overflow-hidden cursor-pointer hover:brightness-125 transition-all shadow border shrink-0 flex items-center justify-center ${chipColor} ${isQALocked ? 'border-warning' : 'border-black/30'} ${matchesSearch ? 'ring-2 ring-white scale-105' : ''}`}>
                                                                        {isStale && <Clock size={9} className="shrink-0" />}
                                                                        {onPiece && <span className="font-black text-[8px] font-mono tracking-widest truncate px-0.5">{slab.heat_number || slab.id}</span>}
                                                                    </div>
                                                                );
                                                            })}
                                                        </div>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    )}

                                    {!isLinear && (
                                        <ZoneFormation
                                            style={zone.storage_style}
                                            materialKey={material.type_key}
                                            columns={zone.columns || []}
                                            colsCount={colsCount}
                                            isFullscreen={isFullscreen}
                                            isEditMode={isEditMode}
                                            editCaps={editCaps}
                                            setEditCaps={setEditCaps}
                                            formatStackName={formatStackName}
                                            onOpen={openSlabDetail}
                                            onSaveColumn={handleSaveColumn}
                                            searchQuery={searchQuery}
                                        />
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
                                <div className="text-xs text-secondary uppercase tracking-widest mb-0.5">{material.singular} Detail</div>
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
                <span className="flex items-center gap-1 ml-auto italic opacity-60">Click any {material.singular.toLowerCase()} to view detail &amp; history</span>
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
