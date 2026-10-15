/** How a piece is drawn. Coil and wire rod are round; the others are blocks. */
export type PieceShape = 'round' | 'slab' | 'billet' | 'plate' | 'square';

export function pieceShape(typeKey?: string | null): PieceShape {
    const key = (typeKey || '').replace(/[\s-]+/g, '_').toUpperCase();
    if (key === 'COIL' || key === 'WIRE_ROD' || key === 'WIREROD' || key.includes('WIRE')) return 'round';
    if (key === 'BILLET') return 'billet';
    if (key === 'PLATE') return 'plate';
    if (key === 'TMT_BUNDLE' || key === 'TMT' || key.includes('BUNDLE')) return 'square';
    return 'slab';
}

/** Free-standing piece: rack, floor, pyramid, and the linear chip row. */
export function pieceFrame(shape: PieceShape): string {
    switch (shape) {
        case 'round': return 'h-10 w-10 rounded-full';
        case 'billet': return 'h-9 w-14 rounded-sm';
        case 'plate': return 'h-3.5 w-16 rounded-sm';
        case 'square': return 'h-10 w-10 rounded-sm';
        default: return 'h-7 w-16 rounded-sm';
    }
}

/** Piece sitting in a stack slot. The slot keeps the stack height; this is the silhouette. */
export function pieceInStack(shape: PieceShape): string {
    switch (shape) {
        case 'round': return 'h-[82%] aspect-square max-w-full rounded-full';
        case 'billet': return 'h-[72%] w-[58%] rounded-sm';
        case 'plate': return 'h-[36%] w-[94%] rounded-sm';
        case 'square': return 'h-[78%] aspect-square max-w-full rounded-sm';
        default: return 'h-[64%] w-[94%] rounded-sm';
    }
}
