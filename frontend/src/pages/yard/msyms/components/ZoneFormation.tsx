import React from 'react';
import { Check, Clock, Flame, Plus, Trash2 } from 'lucide-react';
import { FORMATION, asFormation, type StorageStyle } from '../../formations';
import { pieceFrame, pieceInStack, pieceShape, type PieceShape } from '../../materialShape';

interface ZoneFormationProps {
    style?: string | null;
    materialKey?: string;
    columns: any[];
    colsCount: number;
    isFullscreen: boolean;
    isEditMode: boolean;
    editCaps: Record<string, number>;
    setEditCaps: React.Dispatch<React.SetStateAction<Record<string, number>>>;
    formatStackName: (row: number, col: number) => string;
    onOpen: (slab: any) => void;
    onSaveColumn: (id: string, cap: number) => void;
    searchQuery: string;
}

function flags(slab: any, searchQuery: string) {
    const chem = slab.chemistry_data || {};
    const q = searchQuery.toLowerCase();
    return {
        isDefect: !!chem.grade?.includes('DEFECT'),
        isStale: slab.stale_flagged === true,
        isQALocked: slab.quality_status === 'LOCKED_QA',
        isHot: slab.temperature_state === 'HOT',
        matches: !!q && (slab.heat_number?.toLowerCase().includes(q) || slab.id?.toLowerCase().includes(q)),
        label: slab.heat_number || slab.id,
    };
}

function tone(f: ReturnType<typeof flags>) {
    if (f.isDefect) return 'bg-danger text-white border-danger';
    if (f.isQALocked) return 'bg-warning text-black border-warning';
    if (f.isStale) return 'bg-[#b87d00] text-white border-[#b87d00]';
    return 'bg-accent text-white border-accent';
}

function Rule({ style }: { style: StorageStyle }) {
    return <p className="mt-3 text-[10px] uppercase tracking-widest text-secondary">{FORMATION[style].rule}</p>;
}

function shapeOf(slab: any, materialKey?: string): PieceShape {
    return pieceShape(slab?.material_type || materialKey);
}

function Mark({ slab, materialKey, searchQuery, onOpen, className }: { slab: any; materialKey?: string; searchQuery: string; onOpen: (s: any) => void; className?: string }) {
    const f = flags(slab, searchQuery);
    const shape = shapeOf(slab, materialKey);
    const onPiece = shape === 'slab' || shape === 'plate' || shape === 'billet';
    return (
        <div className="flex flex-col items-center min-w-0 max-w-full">
            <button type="button" onClick={() => onOpen(slab)} title={`${f.label}${f.isStale ? '\nStale' : ''}${f.isQALocked ? '\nQA hold' : ''}`}
                className={`${pieceFrame(shape)} ${className || ''} ${tone(f)} ${f.matches ? 'ring-2 ring-white scale-105' : ''} flex items-center justify-center overflow-hidden cursor-pointer hover:brightness-125 shrink-0`}>
                {f.isHot && <Flame size={9} className="text-[#ff4500] shrink-0" />}
                {f.isStale && <Clock size={8} className="shrink-0" />}
                {onPiece && <span className="font-black text-[8px] font-mono tracking-wider truncate px-0.5">{f.label}</span>}
            </button>
            {!onPiece && <span className="mt-1 max-w-full truncate text-[8px] font-mono text-[#c8c8c8]">{f.label}</span>}
        </div>
    );
}

function EmptyMark({ shape, className }: { shape: PieceShape; className?: string }) {
    return <div className={`${pieceFrame(shape)} border ${className || 'border-[#2a2a2a]'}`} />;
}

function StackFormation(props: ZoneFormationProps) {
    const { columns, colsCount, isFullscreen, isEditMode, editCaps, setEditCaps, formatStackName, onOpen, onSaveColumn, searchQuery, materialKey } = props;
    return (
        <>
            <div className="grid gap-3" style={{ gridTemplateColumns: `repeat(${colsCount}, minmax(0, 1fr))` }}>
                {columns?.map((col: any) => {
                    const isEditing = editCaps[col.id] !== undefined;
                    const draftVal = isEditing ? editCaps[col.id] : col.capacity_limit;
                    const sorted = (col.slabs || []).slice().sort((a: any, b: any) => (a.stack_index || 0) - (b.stack_index || 0));
                    const topId = sorted.length ? sorted[sorted.length - 1].id : null;

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
                                                {isEditing && <button onClick={() => onSaveColumn(col.id, draftVal)} className="text-success hover:bg-success/20 p-1 rounded border border-success"><Check size={12} /></button>}
                                                <button onClick={() => onSaveColumn(col.id, 0)} className="text-danger hover:bg-danger/20 p-1 rounded ml-1 bg-[#1a1111] border border-danger/50"><Trash2 size={12} /></button>
                                            </>
                                        ) : (
                                            <button onClick={() => onSaveColumn(col.id, 10)} className="text-success hover:bg-success/20 p-1 rounded bg-[#111a11] border border-success/50 flex items-center"><Plus size={12} className="mr-1" />ENABLE</button>
                                        )}
                                    </div>
                                ) : (
                                    <div className={`text-[10px] font-mono tracking-widest ${sorted.length >= col.capacity_limit ? 'text-danger' : 'text-success'}`}>{sorted.length} / {col.capacity_limit}</div>
                                )}
                            </div>
                            <div className="flex-1 w-full bg-[#050608] border border-[#1a1a1a] rounded flex flex-col-reverse justify-start p-1 overflow-y-auto overflow-x-hidden relative shadow-[inset_0_4px_10px_rgba(0,0,0,0.5)] thin-scrollbar">
                                {col.capacity_limit === 0 ? (
                                    <div className="absolute inset-0 flex items-center justify-center -rotate-90 text-[10px] tracking-widest font-bold text-danger/30 uppercase">DEAD SPACE</div>
                                ) : sorted.length === 0 ? (
                                    <div className="absolute inset-0 flex flex-col justify-end">
                                        {[...Array(col.capacity_limit)].map((_, idx) => (
                                            <div key={idx} style={{ height: `${100 / col.capacity_limit}%` }} className="w-full mb-[2px] border-b border-dashed border-[#1a1a1a] opacity-30" />
                                        ))}
                                    </div>
                                ) : (
                                    sorted.map((slab: any) => {
                                        const f = flags(slab, searchQuery);
                                        const isTop = slab.id === topId;
                                        let bgClass = 'bg-gradient-to-r from-[#216e54] to-accent text-white font-bold';
                                        if (f.isDefect) bgClass = 'bg-gradient-to-r from-danger to-[#8B0000] text-white';
                                        else if (f.isQALocked) bgClass = 'bg-gradient-to-r from-warning/60 to-warning text-black';
                                        else if (f.isStale) bgClass = 'bg-gradient-to-r from-[#6b4c00] to-[#b87d00] text-white font-bold';
                                        const chem = slab.chemistry_data || {};
                                        const reach = isTop ? 'Take this' : 'Dig to reach';
                                        const tooltipText = `${reach}\nID: ${slab.id}\nHeat: ${slab.heat_number}\nGrade: ${chem.grade || 'Standard'}\nTarget Reheat: ${slab.target_reheat_temp ? slab.target_reheat_temp + '°C' : 'N/A'}\nRolling Width: ${slab.rolling_width || 'N/A'}mm\nCast Time: ${slab.cast_time ? new Date(slab.cast_time).toLocaleString() : 'N/A'}${f.isHot ? '\nHot charge ready' : ''}${f.isStale ? '\nStale' : ''}${f.isQALocked ? '\nQA hold' : ''}`;
                                        const shape = shapeOf(slab, materialKey);
                                        const onPiece = shape === 'slab' || shape === 'plate' || shape === 'billet';
                                        return (
                                            <div key={slab.id} style={{ height: `${100 / col.capacity_limit}%` }} className="w-full mb-[3px] flex items-center justify-center shrink-0">
                                                <div onClick={() => onOpen(slab)} title={tooltipText}
                                                    className={`${pieceInStack(shape)} flex items-center justify-center overflow-hidden shadow-[inset_0_-2px_5px_rgba(0,0,0,0.6)] border cursor-pointer hover:brightness-125 transition-all ${bgClass} ${isTop ? 'border-[#3b82f6] ring-2 ring-[#3b82f6]' : 'border-[#000] opacity-80'} ${f.matches ? 'ring-2 ring-white scale-[1.03] z-10' : ''}`}>
                                                    <div className="font-black text-[9px] tracking-widest font-mono select-none overflow-hidden flex items-center gap-0.5 px-0.5">
                                                        {f.isHot && <Flame size={9} className="text-[#ff4500] shrink-0" />}
                                                        {f.isStale && <Clock size={8} className="shrink-0" />}
                                                        {onPiece && <span className="truncate">{f.label}</span>}
                                                    </div>
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
            <Rule style="STACK" />
        </>
    );
}

function RackFormation({ columns, colsCount, formatStackName, onOpen, searchQuery, materialKey }: ZoneFormationProps) {
    const ordered = (columns || []).slice().sort((a, b) => a.row_index - b.row_index || a.col_index - b.col_index);
    return (
        <>
            <div className="grid gap-2" style={{ gridTemplateColumns: `repeat(${Math.max(colsCount, 1)}, minmax(0, 1fr))` }}>
                {ordered.map((col: any) => {
                    const slab = (col.slabs || [])[0];
                    return (
                        <div key={col.id} className="rounded-lg border border-[#333] bg-[#111318] min-h-[76px] flex flex-col items-center justify-center p-2">
                            <div className="text-[9px] font-mono tracking-widest text-secondary mb-1.5">{formatStackName(col.row_index, col.col_index)}</div>
                            {slab ? (
                                <Mark slab={slab} materialKey={materialKey} searchQuery={searchQuery} onOpen={onOpen} className="border" />
                            ) : (
                                <EmptyMark shape={pieceShape(materialKey)} />
                            )}
                        </div>
                    );
                })}
            </div>
            <Rule style="RACK" />
        </>
    );
}

function FloorFormation({ columns, onOpen, searchQuery, materialKey }: ZoneFormationProps) {
    const items = (columns || []).flatMap((col: any) => col.slabs || []);
    const cap = (columns || []).reduce((n: number, col: any) => n + (col.capacity_limit || 0), 0);
    const empty = Math.max(0, cap - items.length);
    return (
        <>
            <div className="text-[10px] uppercase tracking-widest text-secondary mb-2">{items.length} of {cap} spaces</div>
            <div className="border border-dashed border-[#555] rounded-xl p-4 min-h-[150px] bg-[#0B0C10]">
                <div className="flex flex-wrap gap-2 content-start">
                    {items.map((slab: any) => (
                        <Mark key={slab.id} slab={slab} materialKey={materialKey} searchQuery={searchQuery} onOpen={onOpen} className="border" />
                    ))}
                    {[...Array(empty)].map((_, i) => (
                        <EmptyMark key={i} shape={pieceShape(materialKey)} />
                    ))}
                </div>
            </div>
            <Rule style="FLOOR" />
        </>
    );
}

function PyramidFormation({ columns, onOpen, searchQuery, materialKey }: ZoneFormationProps) {
    const byRow = new Map<number, any[]>();
    (columns || []).forEach((col: any) => {
        const row = byRow.get(col.row_index) || [];
        row.push(col);
        byRow.set(col.row_index, row);
    });
    const rows = [...byRow.keys()].sort((a, b) => b - a);
    const top = rows[0];
    return (
        <>
            <div className="flex flex-col items-center gap-2 py-2">
                {rows.map(row => {
                    const free = row === top;
                    const cells = (byRow.get(row) || []).slice().sort((a, b) => a.col_index - b.col_index);
                    return (
                        <div key={row} className="flex items-center gap-3">
                            <div className="flex gap-2 justify-center">
                                {cells.map((col: any) => {
                                    const slab = (col.slabs || [])[0];
                                    if (!slab) {
                                        return <EmptyMark key={col.id} shape={pieceShape(materialKey)} className={free ? 'border-[#3b82f6]/40' : 'border-[#c4a35a]/30'} />;
                                    }
                                    return (
                                        <Mark key={col.id} slab={slab} materialKey={materialKey} searchQuery={searchQuery} onOpen={onOpen}
                                            className={`border-2 ${free ? 'border-[#3b82f6] ring-2 ring-[#3b82f6]/50' : 'border-[#c4a35a] opacity-70'}`} />
                                    );
                                })}
                            </div>
                            <span className={`text-[10px] uppercase tracking-widest w-14 ${free ? 'text-[#3b82f6]' : 'text-[#c4a35a]'}`}>{free ? 'free' : 'locked'}</span>
                        </div>
                    );
                })}
            </div>
            <Rule style="PYRAMID" />
        </>
    );
}

export const ZoneFormation: React.FC<ZoneFormationProps> = (props) => {
    const style = asFormation(props.style);
    if (style === 'RACK') return <RackFormation {...props} />;
    if (style === 'FLOOR') return <FloorFormation {...props} />;
    if (style === 'PYRAMID') return <PyramidFormation {...props} />;
    return <StackFormation {...props} />;
};
