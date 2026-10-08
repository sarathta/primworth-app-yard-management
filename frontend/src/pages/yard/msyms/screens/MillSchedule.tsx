import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { RefreshCcw, ArrowRight, Flame, MapPin, Truck } from 'lucide-react';
import { symsFetch, symsError } from '../symsApi';

export default function MillSchedule() {
    const { token } = useAuth();
    const [campaign, setCampaign] = useState<any[]>([]);
    const [loading, setLoading] = useState(false);
    const [transferring, setTransferring] = useState<Record<string, boolean>>({});
    const [message, setMessage] = useState('');

    const fetchSchedule = async () => {
        setLoading(true);
        try {
            const res = await symsFetch('http://localhost:4000/api/workorders/hsm-schedule');
            if (!res.ok) {
                setCampaign([]);
                setMessage(await symsError(res, 'Could not read the rolling campaign.'));
            } else {
                const data = await res.json();
                setCampaign(data.campaign || []);
                setMessage('');
            }
        } catch {
            setCampaign([]);
            setMessage('Could not reach the yard.');
        }
        setLoading(false);
    };

    useEffect(() => {
        fetchSchedule();
    }, [token]);

    const deployTransfer = async (slabId: string) => {
        setTransferring(prev => ({ ...prev, [slabId]: true }));
        setMessage('');
        try {
            const res = await symsFetch('http://localhost:4000/api/workorders/hsm-transfer', {
                method: 'POST',
                body: JSON.stringify({ slab_id: Number(slabId) })
            });
            if (res.ok) {
                const data = await res.json();
                setMessage(data.message);
                setCampaign(prev => prev.filter(s => s.id !== slabId));
            } else {
                setMessage(await symsError(res, 'That transfer could not be queued.'));
            }
        } catch {
            setMessage('Could not reach the yard. Nothing was changed.');
        }
        setTransferring(prev => ({ ...prev, [slabId]: false }));
    };

    return (
        <div className="max-w-6xl mx-auto space-y-6 pb-12">
            <div className="flex justify-between items-center bg-[#0c1015] p-6 rounded-xl border border-[#222]">
                <div>
                    <h1 className="text-3xl font-black uppercase tracking-widest text-white mb-2">HSM Operations Center</h1>
                    <p className="text-secondary text-sm">Manage the active Rolling Campaign and sequence slab transfers to the Reheating Furnace.</p>
                </div>
                <button 
                    onClick={fetchSchedule}
                    disabled={loading}
                    className="flex items-center px-4 py-2 bg-primary border border-[#333] hover:border-[#555] rounded font-bold uppercase tracking-widest text-xs text-white transition-all disabled:opacity-50">
                    <RefreshCcw size={14} className={`mr-2 ${loading ? 'animate-spin' : ''}`} /> Generate Schedule
                </button>
            </div>

            {message && (
                <div className="bg-accent/20 border border-accent text-accent p-4 rounded text-sm font-mono animate-pulse">
                    {message}
                </div>
            )}

            <div className="bg-[#0c1015] border border-[#222] rounded-xl overflow-hidden">
                <div className="grid grid-cols-12 gap-4 p-4 border-b border-[#222] bg-[#111] text-xs font-black uppercase tracking-widest text-secondary">
                    <div className="col-span-1 text-center">SEQ</div>
                    <div className="col-span-2">Slab ID</div>
                    <div className="col-span-2">Heat / Grade</div>
                    <div className="col-span-2">Thermal State</div>
                    <div className="col-span-2">Location</div>
                    <div className="col-span-3 text-right">Action</div>
                </div>

                <div className="divide-y divide-[#222]">
                    {campaign.map((slab, idx) => {
                        const isHot = slab.temperature_state === 'HOT';
                        const loc = `${slab.zone?.name} / R${slab.column?.row_index}-C${slab.column?.col_index}`;
                        const chem = slab.chemistry_data || {};
                        return (
                            <div key={slab.id} className="grid grid-cols-12 gap-4 p-4 items-center hover:bg-[#151a22] transition-colors">
                                <div className="col-span-1 text-center font-black text-xl text-[#333]">{(idx + 1).toString().padStart(2, '0')}</div>
                                <div className="col-span-2 font-mono text-sm text-white font-bold">{slab.heat_number || slab.id}</div>
                                <div className="col-span-2">
                                    <div className="text-xs text-white mb-1 font-bold">{slab.heat_number}</div>
                                    <div className="text-[10px] uppercase text-secondary">{chem.grade || slab.grade_code}</div>
                                </div>
                                <div className="col-span-2">
                                    {isHot ? (
                                        <span className="flex items-center text-xs font-bold text-[#ff4500] bg-[#ff4500]/10 px-2 py-1 rounded w-fit border border-[#ff4500]/30 animate-pulse">
                                            <Flame size={12} className="mr-1" /> HOT CHARGE
                                        </span>
                                    ) : (
                                        <span className="flex items-center text-xs font-bold text-blue-400 bg-blue-400/10 px-2 py-1 rounded w-fit border border-blue-400/30">
                                            COLD CHARGE
                                        </span>
                                    )}
                                </div>
                                <div className="col-span-2 flex items-center text-xs text-gray-400 font-mono">
                                    <MapPin size={12} className="mr-1 text-accent" /> {loc}
                                </div>
                                <div className="col-span-3 flex justify-end">
                                    <button 
                                        onClick={() => deployTransfer(slab.id)}
                                        disabled={transferring[slab.id]}
                                        className="flex items-center px-4 py-2 bg-accent/20 hover:bg-accent text-accent hover:text-[#0c1015] border border-accent rounded font-black uppercase tracking-widest text-[10px] transition-all disabled:opacity-50">
                                        {transferring[slab.id] ? 'Deploying...' : <>Deploy Transfer <Truck size={14} className="ml-2" /></>}
                                    </button>
                                </div>
                            </div>
                        )
                    })}
                    {campaign.length === 0 && !loading && (
                        <div className="p-12 text-center text-secondary">
                            No slabs are ready to charge. A slab appears here once it is stacked in
                            the yard and not under a Met-Lab hold, hottest first.
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
