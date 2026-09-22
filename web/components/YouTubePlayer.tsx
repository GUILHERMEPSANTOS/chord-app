'use client';

import { useEffect, useRef, useState } from 'react';

export type YouTubeControls = { play(): void; pause(): void; seek(seconds: number): void };
type Player = { getCurrentTime(): number; playVideo(): void; pauseVideo(): void; seekTo(seconds: number, allowSeekAhead: boolean): void; destroy(): void };
type YTGlobal = { Player: new (element: HTMLElement, options: { events: { onReady: () => void; onStateChange: (event: { data: number }) => void; onError: (event: { data: number }) => void } }) => Player };
declare global { interface Window { YT?: YTGlobal; onYouTubeIframeAPIReady?: () => void } }

export default function YouTubePlayer({ videoId, onTime, onPlaybackChange, onControls }: {
  videoId: string;
  onTime: (seconds: number) => void;
  onPlaybackChange: (playing: boolean) => void;
  onControls: (controls: YouTubeControls | null) => void;
}) {
  const iframe = useRef<HTMLIFrameElement>(null);
  const [error, setError] = useState('');
  const [origin, setOrigin] = useState('');
  useEffect(() => setOrigin(window.location.origin), []);
  useEffect(() => {
    if (!origin) return;
    let player: Player | undefined;
    let timer: ReturnType<typeof setInterval> | undefined;
    let timeout: ReturnType<typeof setTimeout> | undefined;
    let cancelled = false;
    setError('');
    const start = () => {
      if (cancelled || !window.YT || !iframe.current) return;
      player = new window.YT.Player(iframe.current, {
        events: {
          onReady: () => {
            if (!player || cancelled) return;
            if (timeout) clearTimeout(timeout);
            onControls({ play: () => player?.playVideo(), pause: () => player?.pauseVideo(), seek: seconds => player?.seekTo(seconds, true) });
            timer = setInterval(() => { if (player) onTime(player.getCurrentTime()); }, 250);
          },
          onStateChange: event => onPlaybackChange(event.data === 1),
          onError: event => {
            if (timeout) clearTimeout(timeout);
            onControls(null);
            onPlaybackChange(false);
            setError(event.data === 101 || event.data === 150 ? 'O proprietário deste vídeo não permite reprodução incorporada.' : event.data === 153 ? 'O YouTube bloqueou a reprodução por falta de identificação do aplicativo.' : 'O YouTube não conseguiu reproduzir este vídeo aqui.');
          },
        },
      });
      timeout = setTimeout(() => setError('O player do YouTube não respondeu. Abra o vídeo no YouTube ou tente outro navegador.'), 12000);
    };
    if (window.YT?.Player) start();
    else {
      const previous = window.onYouTubeIframeAPIReady;
      window.onYouTubeIframeAPIReady = () => { previous?.(); start(); };
      if (!document.querySelector('script[src="https://www.youtube.com/iframe_api"]')) {
        const script = document.createElement('script'); script.src = 'https://www.youtube.com/iframe_api'; document.head.appendChild(script);
      }
    }
    return () => { cancelled = true; onControls(null); onPlaybackChange(false); if (timer) clearInterval(timer); if (timeout) clearTimeout(timeout); player?.destroy(); };
  }, [videoId, origin, onTime, onPlaybackChange, onControls]);
  const src = `https://www.youtube.com/embed/${encodeURIComponent(videoId)}?enablejsapi=1&origin=${encodeURIComponent(origin)}`;
  return <div className="youtube-wrapper">{origin && <iframe key={videoId} ref={iframe} className="youtube-player" src={src} title="Vídeo do YouTube" referrerPolicy="strict-origin-when-cross-origin" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowFullScreen />}{error && <p className="video-error" role="alert">{error} <a href={`https://www.youtube.com/watch?v=${encodeURIComponent(videoId)}`} target="_blank" rel="noopener noreferrer">Assistir no YouTube</a></p>}</div>;
}
