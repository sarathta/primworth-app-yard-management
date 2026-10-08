// An image full screen, with rotate and zoom; Escape or a click outside closes it. Pair with a
// <button className="pg-thumb"><img/></button> thumbnail in the table.
import { useEffect, useState } from 'react';
import { IconAction } from './Table';
import './page.css';

export function ImageViewer({ src, alt, onClose }: { src: string; alt: string; onClose: () => void }) {
  const [zoom, setZoom] = useState(1);
  const [turn, setTurn] = useState(0);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);
  return (
    <div className="pg-viewer" onClick={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="pg-viewer__tools">
        <IconAction icon="rotate_left" title="Rotate left" onClick={() => setTurn(t => t - 90)} />
        <IconAction icon="rotate_right" title="Rotate right" onClick={() => setTurn(t => t + 90)} />
        <IconAction icon="zoom_out" title="Zoom out" disabled={zoom <= 0.5} onClick={() => setZoom(z => Math.max(0.5, z - 0.25))} />
        <IconAction icon="zoom_in" title="Zoom in" disabled={zoom >= 3} onClick={() => setZoom(z => Math.min(3, z + 0.25))} />
        <IconAction icon="close" title="Close" onClick={onClose} />
      </div>
      <img src={src} alt={alt} style={{ transform: `rotate(${turn}deg) scale(${zoom})` }} />
    </div>
  );
}
