'use client';

import { useEffect, useRef } from 'react';

export type YouTubeControls = { play(): void; pause(): void; seek(seconds: number): void };
type Player = { getCurrentTime(): number; playVideo(): void; pauseVideo(): void; seekTo(seconds: number, allowSeekAhead: boolean): void; destroy(): void };
type YTGlobal = { Player: new (element: HTMLElement, options: { videoId: string; width: string; height: string; events: { onReady: () => void; onStateChange: (event: { data: number }) => void } }) => Player };
declare global { interface Window { YT?: YTGlobal; onYouTubeIframeAPIReady?: () => void } }

export default function YouTubePlayer({ videoId, onTime, onPlaybackChange, onControls }: {
  videoId: string;
  onTime: (seconds: number) => void;
  onPlaybackChange: (playing: boolean) => void;
  onControls: (controls: YouTubeControls | null) => void;
}) {
  const container = useRef<HTMLDivElement>(null);
  useEffect(() => {
    let player: Player | undefined;
    let timer: ReturnType<typeof setInterval> | undefined;
    let cancelled = false;
    const start = () => {
      if (cancelled || !window.YT || !container.current) return;
      player = new window.YT.Player(container.current, {
        videoId, width: '100%', height: '220',
        events: {
          onReady: () => {
            if (!player || cancelled) return;
            onControls({ play: () => player?.playVideo(), pause: () => player?.pauseVideo(), seek: seconds => player?.seekTo(seconds, true) });
            timer = setInterval(() => { if (player) onTime(player.getCurrentTime()); }, 250);
          },
          onStateChange: event => onPlaybackChange(event.data === 1),
        },
      });
    };
    if (window.YT?.Player) start();
    else {
      const previous = window.onYouTubeIframeAPIReady;
      window.onYouTubeIframeAPIReady = () => { previous?.(); start(); };
      if (!document.querySelector('script[src="https://www.youtube.com/iframe_api"]')) {
        const script = document.createElement('script'); script.src = 'https://www.youtube.com/iframe_api'; document.head.appendChild(script);
      }
    }
    return () => { cancelled = true; onControls(null); onPlaybackChange(false); if (timer) clearInterval(timer); player?.destroy(); };
  }, [videoId, onTime, onPlaybackChange, onControls]);
  return <div className="youtube-player" ref={container} />;
}
