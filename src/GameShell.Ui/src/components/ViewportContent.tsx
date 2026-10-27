import { useLayoutEffect, useRef, type ReactNode } from 'react';

// Keep the whole page visible. Measure the actual content region after the
// footer and padding have been laid out rather than guessing from window height.
export function ViewportContent({ children }: { children: ReactNode }) {
  const frameRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const frame = frameRef.current;
    const content = contentRef.current;
    if (!frame || !content) return;
    let pending = 0;
    let lastMeasurement = '';
    const measurement = () => `${frame.clientWidth}:${frame.clientHeight}:${content.scrollWidth}:${content.scrollHeight}`;
    const fit = () => {
      pending = 0;
      if (frame.clientWidth <= 0 || frame.clientHeight <= 0) return;
      const apply = (scale: number) => {
        content.style.zoom = String(scale);
        content.style.width = `${frame.clientWidth / scale}px`;
      };
      const fits = (scale: number) => {
        apply(scale);
        return content.scrollHeight * scale <= frame.clientHeight - 2
          && content.scrollWidth * scale <= frame.clientWidth + 1;
      };
      if (!fits(1)) {
        let low = 0.05;
        let high = 1;
        for (let index = 0; index < 12; index++) {
          const middle = (low + high) / 2;
          if (fits(middle)) low = middle;
          else high = middle;
        }
        apply(low);
      }
      lastMeasurement = measurement();
    };
    const schedule = () => {
      if (!pending && measurement() !== lastMeasurement) pending = requestAnimationFrame(fit);
    };
    const observer = new ResizeObserver(schedule);
    observer.observe(frame);
    observer.observe(content);
    const mutations = new MutationObserver(schedule);
    mutations.observe(content, { childList: true, subtree: true, characterData: true });
    fit();
    return () => {
      cancelAnimationFrame(pending);
      observer.disconnect();
      mutations.disconnect();
    };
  }, []);

  return <div ref={frameRef} className="viewport-content-frame"><div ref={contentRef} className="viewport-content">{children}</div></div>;
}
