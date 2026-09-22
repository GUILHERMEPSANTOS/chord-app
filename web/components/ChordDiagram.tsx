const ROOT_FRET: Record<string, number> = {
  E: 0, F: 1, 'F#': 2, G: 3, 'G#': 4, A: 5,
  'A#': 6, B: 7, C: 8, 'C#': 9, D: 10, 'D#': 11,
};

export default function ChordDiagram({ chord }: { chord: string }) {
  if (chord === 'N') return <div className="no-chord-mark" aria-label="Sem acorde definido">—</div>;
  const quality = ['maj7', 'm7b5', 'dim7', 'dim', 'm7', '7', 'm'].find(suffix => chord.endsWith(suffix)) ?? '';
  const root = quality ? chord.slice(0, -quality.length) : chord;
  const fret = ROOT_FRET[root];
  if (fret === undefined) return null;
  const shape: Record<string, number[]> = {
    '': [0, 2, 2, 1, 0, 0],
    m: [0, 2, 2, 0, 0, 0],
    '7': [0, 2, 0, 1, 0, 0],
    maj7: [0, 2, 1, 1, 0, 0],
    m7: [0, 2, 0, 0, 0, 0],
    dim: [0, 1, 2, 0, -1, -1],
    dim7: [0, 1, 2, 0, 2, 0],
    m7b5: [0, 1, 2, 0, 3, 0],
  };
  const fingers = shape[quality].map(position => position < 0 ? -1 : position + fret);
  const start = fret === 0 ? 1 : fret;
  const x = (string: number) => 20 + string * 30;
  const y = (fingerFret: number) => 42 + (fingerFret - start) * 27;

  return <svg className="chord-diagram" viewBox="0 0 190 173" role="img" aria-label={`Diagrama de violão para ${chord}`}>
    {fret > 1 && <text x="4" y="46" className="fret-number">{fret}</text>}
    {[0, 1, 2, 3, 4].map(i => <line key={`f${i}`} x1="20" y1={28 + i * 27} x2="170" y2={28 + i * 27} className="fret-line" />)}
    {[0, 1, 2, 3, 4, 5].map(i => <line key={`s${i}`} x1={x(i)} y1="28" x2={x(i)} y2="136" className="string-line" />)}
    {fingers.map((fingerFret, i) => fingerFret < 0
      ? <text key={i} x={x(i)} y="21" textAnchor="middle" className="string-label">×</text>
      : fingerFret === 0
      ? <circle key={i} cx={x(i)} cy="17" r="5" className="open-string" />
      : <circle key={i} cx={x(i)} cy={y(fingerFret)} r="10" className="finger-dot" />)}
    {['E', 'A', 'D', 'G', 'B', 'E'].map((note, i) => <text key={i} x={x(i)} y="160" textAnchor="middle" className="string-label">{note}</text>)}
  </svg>;
}
