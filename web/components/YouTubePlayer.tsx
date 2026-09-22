'use client';

import { useEffect, useRef } from 'react';

type Player = { getCurrentTime(): number; destroy(): void };
type YTGlobal = { Player: new (element: HTMLElement, options: { videoId: string; width: string; height: string; events: { onReady: () => void } }) => Player };
declare global { interface Window { YT?: YTGlobal; onYouTubeIframeAPIReady?: () => void } }

export default function YouTubePlayer({ videoId, onTime }: { videoId: string; onTime: (seconds: number) => void }) {
  const container = useRef<HTMLDivElement>(null);
  useEffect(() => {
    let player: Player | undefined;
    let timer: ReturnType<typeof setInterval> | undefined;
    let cancelled = false;
    const start = () => {
      if (cancelled || !window.YT || !container.current) return;
      player = new window.YT.Player(container.current, { videoId, width: '100%', height: '360', events: { onReady: () => { timer = setInterval(() => { if (player) onTime(player.getCurrentTime()); }, 250); } } });
    };
    if (window.YT?.Player) start();
    else {
      const previous = window.onYouTubeIframeAPIReady;
      window.onYouTubeIframeAPIReady = () => { previous?.(); start(); };
      if (!document.querySelector('script[src="https://www.youtube.com/iframe_api"]')) {
        const script = document.createElement('script'); script.src = 'https://www.youtube.com/iframe_api'; document.head.appendChild(script);
      }
    }
    return () => { cancelled = true; if (timer) clearInterval(timer); player?.destroy(); };
  }, [videoId, onTime]);
  return <div className="youtube-player" ref={container} />;
}
