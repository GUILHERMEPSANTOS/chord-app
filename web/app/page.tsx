'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import ChordDiagram from '../components/ChordDiagram';
import YouTubePlayer, { type YouTubeControls } from '../components/YouTubePlayer';

type Chord = { id: string; startTime: number; endTime: number; chord: string; confidence: number | null; corrected: boolean };
type Music = { id: string; fileName: string; sourceUrl: string | null; durationSeconds: number; key: string | null; status: string; error: string | null; progressPercent: number; progressStage: string | null; chords: Chord[] };
type LibraryItem = Pick<Music, 'id' | 'fileName' | 'status'>;
const API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';
const NOTES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
const CHORDS = ['N', ...NOTES.flatMap(note => [note, `${note}m`, `${note}7`, `${note}maj7`, `${note}m7`, `${note}dim`, `${note}dim7`, `${note}m7b5`])];
const fmt = (seconds: number) => `${Math.floor(seconds / 60)}:${Math.floor(seconds % 60).toString().padStart(2, '0')}`;
const statusLabel: Record<string, string> = { Pending: 'Na fila', Processing: 'Analisando', Completed: 'Concluída', Failed: 'Falhou' };

function ChordCard({ item, featured }: { item?: Chord; featured?: boolean }) {
  return <div className={featured ? 'chord-card featured' : 'chord-card'}>
    <span className="card-eyebrow">{featured ? 'ACORDE ATUAL' : 'A SEGUIR'}</span>
    <ChordDiagram chord={item?.chord ?? 'N'} />
    <strong>{item?.chord ?? '—'}</strong>
    <span className="card-time">{item ? `${fmt(item.startTime)} – ${fmt(item.endTime)}` : '—'}</span>
    {featured && <span className="card-rule" />}
  </div>;
}

export default function Home() {
  const [file, setFile] = useState<File | null>(null);
  const [audioUrl, setAudioUrl] = useState<string | null>(null);
  const [music, setMusic] = useState<Music | null>(null);
  const [library, setLibrary] = useState<LibraryItem[]>([]);
  const [current, setCurrent] = useState(0);
  const [playing, setPlaying] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [youtubeUrl, setYoutubeUrl] = useState('');
  const [youtubeControls, setYoutubeControls] = useState<YouTubeControls | null>(null);
  const [showTimeline, setShowTimeline] = useState(false);
  const audioRef = useRef<HTMLAudioElement>(null);
  const updateTime = useCallback((seconds: number) => setCurrent(seconds), []);
  const updatePlaying = useCallback((value: boolean) => setPlaying(value), []);
  const updateControls = useCallback((controls: YouTubeControls | null) => setYoutubeControls(controls), []);
  const activeIndex = useMemo(() => {
    if (!music?.chords.length) return -1;
    const found = music.chords.findIndex(chord => current >= chord.startTime && current < chord.endTime);
    return found >= 0 ? found : current < music.chords[0].startTime ? 0 : music.chords.length - 1;
  }, [music, current]);
  const active = activeIndex >= 0 ? music?.chords[activeIndex] : undefined;
  const upcoming = music?.chords.slice(activeIndex + 1, activeIndex + 3) ?? [];
  const playable = Boolean(music?.sourceUrl ? youtubeControls : audioUrl);

  useEffect(() => { fetch(`${API}/api/musics`).then(response => response.json()).then(setLibrary).catch(() => {}); }, []);
  useEffect(() => {
    if (!music || !['Pending', 'Processing'].includes(music.status)) return;
    const timer = setInterval(() => fetch(`${API}/api/musics/${music.id}`).then(response => response.json()).then((updated: Music) => {
      setMusic(updated);
      if (updated.status === 'Completed' || updated.status === 'Failed') {
        setLibrary(items => items.map(item => item.id === updated.id ? { ...item, status: updated.status } : item));
      }
    }).catch(() => {}), 1500);
    return () => clearInterval(timer);
  }, [music?.id, music?.status]);
  useEffect(() => () => { if (audioUrl) URL.revokeObjectURL(audioUrl); }, [audioUrl]);

  function selectFile(next: File | null) {
    const reattach = music && !music.sourceUrl && next?.name === music.fileName && music.status === 'Completed';
    setFile(next);
    if (!reattach) setMusic(null);
    setCurrent(0); setPlaying(false); setMessage('');
    setAudioUrl(next ? URL.createObjectURL(next) : null);
  }
  async function loadCreated(id: string) {
    const response = await fetch(`${API}/api/musics/${id}`);
    if (!response.ok) throw new Error('Não foi possível abrir a análise');
    const saved: Music = await response.json();
    setMusic(saved);
    setLibrary(items => [{ id, fileName: saved.fileName, status: saved.status }, ...items.filter(item => item.id !== id)]);
  }
  async function upload() {
    if (!file) return;
    setBusy(true); setMessage('');
    try {
      const form = new FormData(); form.append('file', file);
      const response = await fetch(`${API}/api/musics`, { method: 'POST', body: form });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error ?? 'Falha no upload');
      await loadCreated(data.id);
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Falha inesperada'); }
    finally { setBusy(false); }
  }
  async function analyzeYouTube() {
    setBusy(true); setMessage(''); setMusic(null); setCurrent(0); setPlaying(false); setFile(null); setAudioUrl(null);
    try {
      const response = await fetch(`${API}/api/musics/youtube`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ url: youtubeUrl }) });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error ?? 'Não foi possível adicionar o vídeo');
      await loadCreated(data.id);
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Falha inesperada'); }
    finally { setBusy(false); }
  }
  async function changeChord(item: Chord, chord: string) {
    if (!music) return;
    try {
      const response = await fetch(`${API}/api/musics/${music.id}/chords/${item.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ chord }) });
      if (!response.ok) throw new Error();
      const updated: Chord = await response.json();
      setMusic({ ...music, chords: music.chords.map(segment => segment.id === updated.id ? updated : segment) });
    } catch { setMessage('Não foi possível salvar a correção.'); }
  }
  async function openMusic(id: string) {
    try {
      const response = await fetch(`${API}/api/musics/${id}`);
      if (!response.ok) throw new Error();
      const saved: Music = await response.json();
      setMusic(saved); setFile(null); setAudioUrl(null); setCurrent(0); setPlaying(false); setMessage(saved.sourceUrl ? '' : 'Para ouvir esta análise, selecione o mesmo arquivo de áudio novamente.');
    } catch { setMessage('Não foi possível abrir a análise.'); }
  }
  function togglePlayback() {
    if (music?.sourceUrl) {
      if (!youtubeControls) return;
      if (playing) youtubeControls.pause(); else youtubeControls.play();
    } else if (audioRef.current) {
      if (playing) audioRef.current.pause(); else audioRef.current.play().catch(() => setMessage('Não foi possível reproduzir o áudio.'));
    }
  }
  function seek(seconds: number) {
    const bounded = Math.max(0, Math.min(seconds, music?.durationSeconds ?? 0));
    if (music?.sourceUrl) youtubeControls?.seek(bounded);
    else if (audioRef.current) audioRef.current.currentTime = bounded;
    setCurrent(bounded);
  }

  return <main className="app-shell">
    <header className="topbar"><span className="brand">◉ <strong>ChordApp</strong></span><span className="topbar-title">Acordes</span><span className="topbar-badge">Maiores · menores · sétimas · diminutos</span></header>
    <div className="workspace">
      <aside className="sidebar">
        <div className="sidebar-heading"><span className="overline">BIBLIOTECA</span><h1>Suas músicas</h1><p>Envie um áudio ou cole um link para acompanhar os acordes.</p></div>
        <section className="input-card"><label htmlFor="audio">Arquivo de áudio</label><input id="audio" type="file" accept=".mp3,.wav,audio/mpeg,audio/wav" onChange={event => selectFile(event.target.files?.[0] ?? null)} /><small>MP3 ou WAV · até 30 MB · 15 min</small><button className="primary-button" disabled={!file || busy} onClick={upload}>{busy ? 'Enviando…' : 'Analisar arquivo'}</button></section>
        <section className="input-card"><label htmlFor="youtube-url">Vídeo do YouTube</label><input id="youtube-url" type="url" placeholder="Cole a URL do vídeo" value={youtubeUrl} onChange={event => setYoutubeUrl(event.target.value)} /><button className="secondary-button" disabled={!youtubeUrl || busy} onClick={analyzeYouTube}>Analisar vídeo</button></section>
        {message && <p className="alert" role="alert">{message}</p>}
        <div className="library-heading">Análises salvas <span>{library.length}</span></div>
        <div className="library-list">{library.map(item => <button key={item.id} className={`library-item ${music?.id === item.id ? 'selected' : ''}`} onClick={() => openMusic(item.id)}><span className="music-glyph">♫</span><span><strong>{item.fileName}</strong><small>{statusLabel[item.status] ?? item.status}</small></span></button>)}</div>
      </aside>
      <section className="stage">
        {music ? <>
          <div className="track-heading"><span className="overline">AGORA ANALISANDO / REPRODUZINDO</span><h2>{music.fileName}</h2><p><span className={`status-dot ${music.status.toLowerCase()}`} />{statusLabel[music.status] ?? music.status} <span className="separator">·</span> Tom estimado: <strong>{music.key ?? '—'}</strong> <span className="separator">·</span> {music.durationSeconds ? fmt(music.durationSeconds) : '—'}</p></div>
          {music.status === 'Pending' || music.status === 'Processing' ? <div className="progress-panel" role="status" aria-live="polite"><span className="overline">PROCESSAMENTO</span><strong>{music.status === 'Pending' ? 'Na fila' : `${music.progressPercent ?? 2}%`}</strong><p>{music.status === 'Pending' ? 'A análise começará em instantes.' : music.progressStage ?? 'Preparando o áudio...'}</p><div className="progress-track"><span style={{ width: `${music.status === 'Pending' ? 0 : music.progressPercent ?? 2}%` }} /></div><small>Progresso aproximado por etapas do modelo</small></div> : null}
          {music.status === 'Failed' && <div className="failure-panel" role="alert">{music.error ?? 'Não foi possível concluir esta análise.'}</div>}
          {music.status === 'Completed' && <><div className="chord-stage"><ChordCard item={active} featured /><div className="next-chords"><ChordCard item={upcoming[0]} /><ChordCard item={upcoming[1]} /></div></div><div className="timeline-toggle"><span>{music.chords.length} segmentos identificados</span><button onClick={() => setShowTimeline(value => !value)}>{showTimeline ? 'Ocultar acordes' : 'Ver todos os acordes'}</button></div>{showTimeline && <div className="chord-list">{music.chords.map((item, index) => <div key={item.id} className={index === activeIndex ? 'chord-row current-row' : 'chord-row'}><button className="time-link" onClick={() => seek(item.startTime)}>{fmt(item.startTime)} – {fmt(item.endTime)}</button><select aria-label={`Acorde em ${fmt(item.startTime)}`} value={item.chord} onChange={event => changeChord(item, event.target.value)}>{CHORDS.map(chord => <option key={chord} value={chord}>{chord}</option>)}</select><span>{item.corrected ? 'Corrigido' : ''}</span></div>)}</div>}</>}
          {music.sourceUrl && <div className="video-pane"><span className="overline">VÍDEO ORIGINAL</span><YouTubePlayer videoId={new URL(music.sourceUrl).searchParams.get('v') ?? ''} onTime={updateTime} onPlaybackChange={updatePlaying} onControls={updateControls} /></div>}
          {!music.sourceUrl && audioUrl && <audio ref={audioRef} src={audioUrl} onTimeUpdate={event => setCurrent(event.currentTarget.currentTime)} onPlay={() => setPlaying(true)} onPause={() => setPlaying(false)} onEnded={() => setPlaying(false)} />}
        </> : <div className="empty-stage"><span className="empty-icon">♫</span><span className="overline">PRONTO PARA TOCAR</span><h2>Seus acordes, no ritmo da música.</h2><p>Selecione uma análise salva ou envie uma música para começar.</p></div>}
      </section>
    </div>
    <footer className="player-dock"><div className="dock-track"><span className="overline">{music?.sourceUrl ? 'YOUTUBE' : 'ÁUDIO LOCAL'}</span><strong>{music?.fileName ?? 'Nenhuma música selecionada'}</strong></div><div className="dock-main"><div className="transport"><button aria-label="Voltar 10 segundos" disabled={!playable} onClick={() => seek(current - 10)}>↶ 10</button><button className="play-button" aria-label={playing ? 'Pausar' : 'Reproduzir'} disabled={!playable} onClick={togglePlayback}>{playing ? 'Ⅱ' : '▶'}</button><button aria-label="Avançar 10 segundos" disabled={!playable} onClick={() => seek(current + 10)}>10 ↷</button></div><div className="seekbar"><span>{fmt(current)}</span><input type="range" min="0" max={Math.max(music?.durationSeconds ?? 0, 1)} step="0.1" value={Math.min(current, music?.durationSeconds ?? 0)} disabled={!playable || !music?.durationSeconds} onChange={event => seek(Number(event.target.value))} aria-label="Posição da música" /><span>{fmt(music?.durationSeconds ?? 0)}</span></div></div><div className="dock-key">Tom <strong>{music?.key ?? '—'}</strong></div></footer>
  </main>;
}
