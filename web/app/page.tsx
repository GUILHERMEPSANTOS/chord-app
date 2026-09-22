'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import YouTubePlayer from '../components/YouTubePlayer';

type Chord = { id: string; startTime: number; endTime: number; chord: string; confidence: number | null; corrected: boolean };
type Music = { id: string; fileName: string; sourceUrl: string | null; durationSeconds: number; key: string | null; status: string; error: string | null; chords: Chord[] };
const API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';
const NOTES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
const CHORDS = ['N', ...NOTES.flatMap(n => [n, `${n}m`])];
const fmt = (s: number) => `${Math.floor(s / 60)}:${Math.floor(s % 60).toString().padStart(2, '0')}`;

export default function Home() {
  const [file, setFile] = useState<File | null>(null);
  const [url, setUrl] = useState<string | null>(null);
  const [music, setMusic] = useState<Music | null>(null);
  const [library, setLibrary] = useState<Pick<Music, 'id' | 'fileName' | 'status'>[]>([]);
  const [current, setCurrent] = useState(0);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [youtubeUrl, setYoutubeUrl] = useState('');
  const updateTime = useCallback((seconds: number) => setCurrent(seconds), []);
  const active = useMemo(() => music?.chords.find(c => current >= c.startTime && current < c.endTime), [music, current]);
  useEffect(() => { fetch(`${API}/api/musics`).then(r => r.json()).then(setLibrary).catch(() => {}); }, []);
  useEffect(() => {
    if (!music || !['Pending', 'Processing'].includes(music.status)) return;
    const timer = setInterval(() => fetch(`${API}/api/musics/${music.id}`).then(r => r.json()).then(setMusic).catch(() => {}), 1500);
    return () => clearInterval(timer);
  }, [music]);
  useEffect(() => () => { if (url) URL.revokeObjectURL(url); }, [url]);

  function selectFile(next: File | null) {
    const reattach = music && !music.sourceUrl && next?.name === music.fileName && music.status === 'Completed';
    setFile(next); if (!reattach) setMusic(null); setCurrent(0); setMessage('');
    if (url) URL.revokeObjectURL(url);
    setUrl(next ? URL.createObjectURL(next) : null);
  }
  async function upload() {
    if (!file) return;
    setBusy(true); setMessage('');
    try {
      const form = new FormData(); form.append('file', file);
      const response = await fetch(`${API}/api/musics`, { method: 'POST', body: form });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error ?? 'Falha no upload');
      const result = await fetch(`${API}/api/musics/${data.id}`);
      setMusic(await result.json());
      setLibrary(old => [{ id: data.id, fileName: file.name, status: 'Pending' }, ...old]);
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Falha inesperada'); }
    finally { setBusy(false); }
  }
  async function analyzeYouTube() {
    setBusy(true); setMessage(''); setMusic(null); setCurrent(0);
    setFile(null); if (url) URL.revokeObjectURL(url); setUrl(null);
    try {
      const response = await fetch(`${API}/api/musics/youtube`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ url: youtubeUrl }) });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error ?? 'Não foi possível adicionar o vídeo');
      const result = await fetch(`${API}/api/musics/${data.id}`);
      const saved: Music = await result.json(); setMusic(saved);
      setLibrary(old => [{ id: data.id, fileName: saved.fileName, status: 'Pending' }, ...old]);
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Falha inesperada'); }
    finally { setBusy(false); }
  }
  async function changeChord(item: Chord, chord: string) {
    if (!music) return;
    const response = await fetch(`${API}/api/musics/${music.id}/chords/${item.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ chord }) });
    if (!response.ok) { setMessage('Não foi possível salvar a correção.'); return; }
    const updated: Chord = await response.json();
    setMusic({ ...music, chords: music.chords.map(c => c.id === updated.id ? updated : c) });
  }
  async function openMusic(id: string) {
    const response = await fetch(`${API}/api/musics/${id}`);
    if (response.ok) { const saved: Music = await response.json(); setMusic(saved); setFile(null); if (url) URL.revokeObjectURL(url); setUrl(null); setCurrent(0); setMessage(saved.sourceUrl ? '' : 'Para ouvir uma análise antiga, selecione o mesmo arquivo de áudio novamente.'); }
  }

  return <main>
    <header><h1>ChordApp</h1><p>Envie uma música e acompanhe os acordes no tempo.</p></header>
    <section className="card">
      <label htmlFor="audio">MP3 ou WAV, até 30 MB e 15 minutos</label>
      <input id="audio" type="file" accept=".mp3,.wav,audio/mpeg,audio/wav" onChange={e => selectFile(e.target.files?.[0] ?? null)} />
      <button disabled={!file || busy} onClick={upload}>{busy ? 'Enviando…' : 'Analisar música'}</button>
      {url && <audio controls src={url} onTimeUpdate={e => setCurrent(e.currentTarget.currentTime)} />}
      {message && <p role="alert">{message}</p>}
    </section>
    <section className="card"><label htmlFor="youtube-url">URL de vídeo público do YouTube</label><input id="youtube-url" type="url" placeholder="https://www.youtube.com/watch?v=..." value={youtubeUrl} onChange={e => setYoutubeUrl(e.target.value)} /><button disabled={!youtubeUrl || busy} onClick={analyzeYouTube}>Analisar vídeo</button></section>
    {music && <section className="card">
      <h2>{music.fileName}</h2><p>Estado: {music.status} · Tom estimado: {music.key ?? 'indisponível'} · {fmt(music.durationSeconds)}</p>
      {music.sourceUrl && <YouTubePlayer videoId={new URL(music.sourceUrl).searchParams.get('v') ?? ''} onTime={updateTime} />}
      {music.error && <p role="alert">{music.error}</p>}
      {music.status === 'Completed' && <><p className="current">Acorde atual: <strong>{active?.chord ?? 'N'}</strong></p>
        <div className="timeline">{music.chords.map(c => <div key={c.id} className={c.id === active?.id ? 'segment active' : 'segment'} style={{ flexGrow: c.endTime - c.startTime }} title={`${fmt(c.startTime)}–${fmt(c.endTime)}`}>{c.chord}</div>)}</div>
        <ul>{music.chords.map(c => <li key={c.id} className={c.id === active?.id ? 'highlight' : ''}><span>{fmt(c.startTime)} – {fmt(c.endTime)}</span><select aria-label={`Acorde em ${fmt(c.startTime)}`} value={c.chord} onChange={e => changeChord(c, e.target.value)}>{CHORDS.map(name => <option key={name} value={name}>{name}</option>)}</select>{c.corrected && <small>corrigido</small>}</li>)}</ul>
      </>}
    </section>}
    <section className="card"><h2>Análises salvas</h2><ul>{library.map(item => <li key={item.id}><button className="link" onClick={() => openMusic(item.id)}>{item.fileName}</button></li>)}</ul></section>
  </main>;
}
