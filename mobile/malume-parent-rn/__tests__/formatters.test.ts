import {
  formatDistanceMeters,
  formatEtaTime,
  formatSastDate,
  isPeakWindowSast,
  parseSignalRFrames,
  SIGNALR_RECORD_SEPARATOR,
} from '../src/utils/formatters';

describe('Parent formatters & helpers', () => {
  test('formatSastDate converts UTC date near midnight into SAST date', () => {
    const utcLateNight = new Date('2026-11-28T22:30:00Z'); // 00:30 SAST on 2026-11-29
    expect(formatSastDate(utcLateNight)).toBe('2026-11-29');
  });

  test('isPeakWindowSast detects morning and afternoon school windows', () => {
    expect(isPeakWindowSast(new Date('2026-11-28T04:30:00Z'))).toBe(true); // 06:30 SAST
    expect(isPeakWindowSast(new Date('2026-11-28T12:15:00Z'))).toBe(true); // 14:15 SAST
    expect(isPeakWindowSast(new Date('2026-11-28T09:00:00Z'))).toBe(false); // 11:00 SAST
  });

  test('formatDistanceMeters and formatEtaTime format cleanly', () => {
    expect(formatDistanceMeters(640)).toBe('640 m');
    expect(formatDistanceMeters(1500)).toBe('1.50 km');
    expect(formatEtaTime('2026-11-28T04:33:00Z')).toBe('06:33 SAST');
    expect(formatEtaTime(null)).toBe('ETA pending');
  });

  test('parseSignalRFrames parses multiple record-separated frames', () => {
    const raw =
      `{"type":1,"target":"LocationUpdated","arguments":[{"tripId":"t1"}]}${SIGNALR_RECORD_SEPARATOR}` +
      `{"type":6}${SIGNALR_RECORD_SEPARATOR}`;
    const frames = parseSignalRFrames(raw);
    expect(frames).toHaveLength(2);
    expect(frames[0].target).toBe('LocationUpdated');
    expect(frames[1].type).toBe(6);
  });
});
