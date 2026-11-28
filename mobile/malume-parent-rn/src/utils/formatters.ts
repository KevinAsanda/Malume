export const SIGNALR_RECORD_SEPARATOR = '\u001e';

export function formatSastDate(date: Date = new Date()): string {
  const sastMs = date.getTime() + 2 * 60 * 60 * 1000;
  const sastDate = new Date(sastMs);
  const year = sastDate.getUTCFullYear();
  const month = String(sastDate.getUTCMonth() + 1).padStart(2, '0');
  const day = String(sastDate.getUTCDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function isPeakWindowSast(date: Date = new Date()): boolean {
  const sastMs = date.getTime() + 2 * 60 * 60 * 1000;
  const sastDate = new Date(sastMs);
  const totalMinutes = sastDate.getUTCHours() * 60 + sastDate.getUTCMinutes();
  const morningPeak = totalMinutes >= 6 * 60 && totalMinutes <= 8 * 60;
  const afternoonPeak = totalMinutes >= 13 * 60 + 30 && totalMinutes <= 15 * 60 + 30;
  return morningPeak || afternoonPeak;
}

export function formatDistanceMeters(meters: number): string {
  if (!Number.isFinite(meters) || meters < 0) {
    return '0 m';
  }
  if (meters < 1000) {
    return `${Math.round(meters)} m`;
  }
  return `${(meters / 1000).toFixed(2)} km`;
}

export function formatEtaTime(isoUtc?: string | null): string {
  if (!isoUtc) {
    return 'ETA pending';
  }
  const parsed = new Date(isoUtc);
  if (Number.isNaN(parsed.getTime())) {
    return 'ETA pending';
  }
  const sastMs = parsed.getTime() + 2 * 60 * 60 * 1000;
  const sastDate = new Date(sastMs);
  const hh = String(sastDate.getUTCHours()).padStart(2, '0');
  const mm = String(sastDate.getUTCMinutes()).padStart(2, '0');
  return `${hh}:${mm} SAST`;
}

export interface SignalRInvocationFrame {
  type: number;
  target?: string;
  arguments?: unknown[];
}

export function parseSignalRFrames(rawChunk: string): SignalRInvocationFrame[] {
  const parts = rawChunk.split(SIGNALR_RECORD_SEPARATOR);
  const frames: SignalRInvocationFrame[] = [];
  for (const part of parts) {
    const trimmed = part.trim();
    if (!trimmed) {
      continue;
    }
    try {
      const parsed = JSON.parse(trimmed) as SignalRInvocationFrame;
      if (typeof parsed.type === 'number') {
        frames.push(parsed);
      }
    } catch {
      // Ignore partial or malformed frames
    }
  }
  return frames;
}
