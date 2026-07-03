import { useEffect, useRef } from 'react';

const BAR_COUNT = 40;

interface Props {
  active: boolean;
  paused?: boolean;
}

export function AudioVisualizer({ active, paused = false }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const frameRef = useRef<number | null>(null);
  const phaseRef = useRef(0);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const context = canvas.getContext('2d');
    if (!context) return;

    const draw = () => {
      const { width, height } = canvas;
      context.clearRect(0, 0, width, height);

      const barWidth = width / BAR_COUNT;
      const gap = Math.max(1, barWidth * 0.28);
      const innerWidth = barWidth - gap;

      for (let index = 0; index < BAR_COUNT; index += 1) {
        const normalizedIndex = index / BAR_COUNT;
        const wave =
          Math.sin(phaseRef.current * 0.08 + normalizedIndex * 8) * 0.35 +
          Math.sin(phaseRef.current * 0.05 + normalizedIndex * 14) * 0.25 +
          Math.sin(phaseRef.current * 0.11 + normalizedIndex * 5) * 0.2;

        let level: number;
        if (!active) {
          level = 0.08;
        } else if (paused) {
          level = 0.12 + Math.abs(Math.sin(normalizedIndex * 4)) * 0.08;
        } else {
          level = 0.18 + Math.abs(wave) * 0.82;
        }

        const barHeight = Math.max(6, level * height);
        const x = index * barWidth + gap / 2;
        const y = (height - barHeight) / 2;

        const gradient = context.createLinearGradient(0, y, 0, y + barHeight);
        gradient.addColorStop(0, 'rgba(79, 209, 197, 0.95)');
        gradient.addColorStop(0.55, 'rgba(63, 177, 181, 0.85)');
        gradient.addColorStop(1, 'rgba(13, 43, 82, 0.55)');

        context.fillStyle = gradient;
        context.beginPath();
        if (typeof context.roundRect === 'function') {
          context.roundRect(x, y, innerWidth, barHeight, innerWidth / 2);
        } else {
          context.rect(x, y, innerWidth, barHeight);
        }
        context.fill();
      }

      if (active && !paused) {
        phaseRef.current += 1;
      }

      frameRef.current = window.requestAnimationFrame(draw);
    };

    frameRef.current = window.requestAnimationFrame(draw);

    return () => {
      if (frameRef.current) window.cancelAnimationFrame(frameRef.current);
    };
  }, [active, paused]);

  return (
    <div className="audio-visualizer" aria-hidden="true">
      <canvas ref={canvasRef} width={560} height={160} className="audio-visualizer__canvas" />
    </div>
  );
}
