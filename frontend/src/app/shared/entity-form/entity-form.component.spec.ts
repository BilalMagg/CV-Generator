import { describe, expect, it } from 'vitest';
import { toDateInputValue } from './entity-form.component';

describe('toDateInputValue', () => {
  it('passes through clean dates', () => {
    expect(toDateInputValue('2025-07-01')).toBe('2025-07-01');
  });

  it('zero-pads loose numeric dates', () => {
    expect(toDateInputValue('2025-7-1')).toBe('2025-07-01');
    expect(toDateInputValue('2025-7')).toBe('2025-07-01');
  });

  it('trims ISO datetimes to the date part', () => {
    expect(toDateInputValue('2025-07-01T00:00:00')).toBe('2025-07-01');
  });

  it('maps a bare year to Jan 1', () => {
    expect(toDateInputValue('2025')).toBe('2025-01-01');
  });

  it('rejects unparseable/invalid values', () => {
    expect(toDateInputValue('')).toBe('');
    expect(toDateInputValue(null)).toBe('');
    expect(toDateInputValue(undefined)).toBe('');
    expect(toDateInputValue('juillet 2025')).toBe('');
    expect(toDateInputValue('garbage')).toBe('');
    expect(toDateInputValue('2025-13-45')).toBe('');
    expect(toDateInputValue('2025-00-10')).toBe('');
  });

  it('trims surrounding whitespace', () => {
    expect(toDateInputValue('  2025-07-01  ')).toBe('2025-07-01');
  });
});