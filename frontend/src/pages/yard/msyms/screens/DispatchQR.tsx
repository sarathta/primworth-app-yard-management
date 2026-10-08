import React, { useEffect, useState } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { useSocket } from '../contexts/SocketContext';
import { useAuth } from '../contexts/AuthContext';
import { Truck, Printer, X, CheckCircle, Package } from 'lucide-react';
import { symsFetch } from '../symsApi';

export const DispatchQR: React.FC = () => {
    const { token } = useAuth();
    const { socket } = useSocket();
    const [slabs, setSlabs] = useState<any[]>([]);
    const [loading, setLoading] = useState(false);
    const [selectedSlab, setSelectedSlab] = useState<any | null>(null);
    const [printing, setPrinting] = useState(false);
    const [printSuccess, setPrintSuccess] = useState(false);

    const fetchDispatchQueue = async () => {
        if (!token) return;
        setLoading(true);
        try {
            const res = await symsFetch('http://localhost:4000/api/slabs/dispatch', { headers: { Authorization: `Bearer ${token}` } });
            if (res.ok) setSlabs(await res.json());
        } catch (e) {
            console.error(e);
        }
        setLoading(false);
    };

    useEffect(() => {
        fetchDispatchQueue();
        if (socket) {
            socket.on('internal:slab_ingested', fetchDispatchQueue);
        }
        return () => {
            if (socket) socket.off('internal:slab_ingested', fetchDispatchQueue);
        };
    }, [token, socket]);

    const handlePrintAndShip = async () => {
        if (!selectedSlab) return;
        setPrinting(true);
        try {
            const res = await symsFetch('http://localhost:4000/api/slabs/ship', {
                method: 'POST',
                headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ slab_id: selectedSlab.id })
            });
            if (res.ok) {
                setPrintSuccess(true);
                setTimeout(() => {
                    setPrintSuccess(false);
                    setSelectedSlab(null);
                    fetchDispatchQueue();
                }, 2000);
            }
        } catch (e) {
            console.error(e);
        }
        setPrinting(false);
    };

    const getZplCode = (slab: any) => {
        const chem = slab.chemistry_data || {};
        const grade = chem.grade || slab.grade_code || 'STANDARD';
        return `^XA\n^FO50,50^A0N,50,50^FD${slab.id.slice(-8)}^FS\n^FO50,120^A0N,30,30^FDHeat: ${slab.heat_number}^FS\n^FO50,160^A0N,30,30^FDWeight: ${slab.weight_kg} kg^FS\n^FO50,200^A0N,30,30^FDGrade: ${grade}^FS\n^FO50,260^BQN,2,5^FDQA,${slab.id}^FS\n^XZ`;
    };

    return (
        <div className="max-w-6xl mx-auto space-y-6 pb-12">
            <div className="flex justify-between items-center bg-[#0c1015] p-6 rounded-xl border border-[#222]">
                <div>
                    <h1 className="text-3xl font-black uppercase tracking-widest text-white mb-2 flex items-center">
                        <Printer size={32} className="text-accent mr-4" /> Tagging & Dispatch Station
                    </h1>
                    <p className="text-secondary text-sm">Review slabs awaiting shipping, generate Zebra Tags, and finalize dispatch logs.</p>
                </div>
            </div>

            <div className="bg-[#0c1015] border border-[#222] rounded-xl overflow-hidden">
                <div className="grid grid-cols-12 gap-4 p-4 border-b border-[#222] bg-[#111] text-xs font-black uppercase tracking-widest text-secondary">
                    <div className="col-span-3">Slab ID</div>
                    <div className="col-span-2">Heat No.</div>
                    <div className="col-span-2">Grade</div>
                    <div className="col-span-2">Weight</div>
                    <div className="col-span-3 text-right">Action</div>
                </div>

                <div className="divide-y divide-[#222]">
                    {loading && slabs.length === 0 ? (
                        <div className="p-12 text-center text-secondary animate-pulse">Loading queue...</div>
                    ) : slabs.length === 0 ? (
                        <div className="p-16 text-center flex flex-col items-center justify-center text-secondary">
                            <Package size={48} className="mb-4 opacity-50" />
                            <div className="text-xl font-bold uppercase tracking-widest text-[#555]">Queue Empty</div>
                            <div className="text-sm mt-2">Deploy a transfer from the Mill Schedule to populate this zone.</div>
                        </div>
                    ) : (
                        slabs.map((slab) => {
                            const chem = slab.chemistry_data || {};
                            const grade = chem.grade || slab.grade_code || 'STANDARD';
                            return (
                                <div key={slab.id} className="grid grid-cols-12 gap-4 p-4 items-center hover:bg-[#151a22] transition-colors">
                                    <div className="col-span-3 font-mono text-sm text-white font-bold">{slab.id}</div>
                                    <div className="col-span-2 text-xs text-secondary font-bold">{slab.heat_number}</div>
                                    <div className="col-span-2 text-xs uppercase text-white bg-[#333] px-2 py-1 rounded w-fit">{grade}</div>
                                    <div className="col-span-2 text-xs font-mono text-secondary">{slab.weight_kg} kg</div>
                                    <div className="col-span-3 flex justify-end">
                                        <button 
                                            onClick={() => setSelectedSlab(slab)}
                                            className="flex items-center px-4 py-2 bg-accent/20 hover:bg-accent text-accent hover:text-[#0c1015] border border-accent rounded font-black uppercase tracking-widest text-[10px] transition-all">
                                            <Printer size={14} className="mr-2" /> Generate Tag
                                        </button>
                                    </div>
                                </div>
                            );
                        })
                    )}
                </div>
            </div>

            {/* Zebra Tag Modal */}
            {selectedSlab && (
                <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
                    <div className="bg-[#050608] border border-[#333] p-8 rounded-2xl shadow-2xl max-w-4xl w-full flex relative overflow-hidden">
                        <button onClick={() => !printing && setSelectedSlab(null)} className="absolute top-4 right-4 text-secondary hover:text-white z-10 disabled:opacity-50" disabled={printing}>
                            <X size={24} />
                        </button>
                        
                        {/* ZPL Code Section */}
                        <div className="flex-1 pr-8 border-r border-[#333]">
                            <h3 className="font-black text-sm uppercase tracking-widest text-[#555] mb-4">Network Payload (ZPL)</h3>
                            <pre className="bg-[#0c1015] border border-[#222] p-4 rounded text-[#0f0] font-mono text-[10px] overflow-auto h-[350px]">
                                {getZplCode(selectedSlab)}
                            </pre>
                            <div className="mt-4 text-[10px] text-secondary font-mono">
                                DESTINATION IP: 192.168.1.104:9100<br/>
                                PROTOCOL: RAW TCP/IP
                            </div>
                        </div>

                        {/* Physical Tag Preview Section */}
                        <div className="flex-1 pl-8 flex flex-col items-center">
                            <h3 className="font-black text-sm uppercase tracking-widest text-accent mb-6">Tag Visualization</h3>
                            <div className="bg-white p-8 rounded shadow-inner flex flex-col items-center text-black w-64 rotate-90 transform origin-center my-12">
                                <QRCodeSVG 
                                    value={`QA,${selectedSlab.id}`} 
                                    size={100} 
                                    level={"M"}
                                    includeMargin={false}
                                />
                                <div className="mt-4 text-center w-full">
                                    <div className="font-mono text-xl font-bold tracking-widest mb-1">{selectedSlab.heat_number || selectedSlab.id}</div>
                                    <div className="text-gray-600 font-bold uppercase tracking-wider text-[10px]">HEAT: {selectedSlab.heat_number}</div>
                                    <div className="text-gray-600 font-bold uppercase tracking-wider text-[10px]">{selectedSlab.weight_kg} kg | {selectedSlab.chemistry_data?.grade || selectedSlab.grade_code || 'STANDARD'}</div>
                                </div>
                            </div>

                            <div className="mt-auto w-full">
                                {printSuccess ? (
                                    <div className="w-full bg-green-500 text-white font-black uppercase tracking-widest py-4 rounded flex justify-center items-center">
                                        <CheckCircle size={20} className="mr-2" /> Printed & Shipped
                                    </div>
                                ) : (
                                    <button 
                                        onClick={handlePrintAndShip}
                                        disabled={printing}
                                        className="w-full flex justify-center items-center bg-blue-600 hover:bg-blue-500 disabled:opacity-50 text-white font-black uppercase tracking-widest py-4 rounded transition-colors shadow-[0_0_15px_rgba(37,99,235,0.5)]">
                                        {printing ? 'Transmitting...' : <><Truck size={20} className="mr-2" /> Confirm Print & Ship</>}
                                    </button>
                                )}
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default DispatchQR;
