export type StorageStyle = 'STACK' | 'RACK' | 'FLOOR' | 'PYRAMID';

export const FORMATION: Record<StorageStyle, { label: string; materials: string; rule: string }> = {
    STACK: { label: 'Stack', materials: 'Slab, plate, billet', rule: 'Top item only; the rest become dig orders.' },
    RACK: { label: 'Rack', materials: 'TMT bundle, coil and wire rod in cradles', rule: 'Every slot has an address. No digging, ever.' },
    FLOOR: { label: 'Floor', materials: 'Coil and wire rod eye-to-sky, loose billet', rule: 'Counted, not addressed. Found by walking.' },
    PYRAMID: { label: 'Pyramid', materials: 'Coil and wire rod on the floor, eye-to-side', rule: 'Top row only; the rows beneath are locked.' },
};

/** Formations a material is stored in. The first is the one a new area uses. */
export function formationsFor(typeKey: string): StorageStyle[] {
    switch ((typeKey || '').toUpperCase()) {
        case 'TMT_BUNDLE': return ['RACK'];
        case 'COIL':
        case 'WIRE_ROD': return ['RACK', 'FLOOR', 'PYRAMID'];
        case 'BILLET': return ['STACK', 'FLOOR'];
        default: return ['STACK'];
    }
}

export function defaultFormation(typeKey: string): StorageStyle {
    return formationsFor(typeKey)[0];
}

export function asFormation(style?: string | null): StorageStyle {
    const key = (style || 'STACK').toUpperCase();
    return key in FORMATION ? key as StorageStyle : 'STACK';
}
