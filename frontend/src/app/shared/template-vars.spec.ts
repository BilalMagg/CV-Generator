import { resolveTemplateVars, recipientGreeting } from './template-vars';

describe('resolveTemplateVars', () => {
  it('replaces known tokens case-insensitively with whitespace tolerance', () => {
    const out = resolveTemplateVars('Hi {{recipient_name}} / {{ RECIPIENT_NAME }}', { recipient_name: 'Marie' });
    expect(out).toBe('Hi Marie / Marie');
  });

  it('leaves unknown tokens verbatim', () => {
    const out = resolveTemplateVars('a {{unknown_token}} b', { recipient_name: 'X' });
    expect(out).toBe('a {{unknown_token}} b');
  });

  it('supports kebab-case and numeric values', () => {
    expect(resolveTemplateVars('{{cv-version}}', { 'cv-version': 3 })).toBe('3');
  });

  it('uses empty string when a value is explicitly empty', () => {
    expect(resolveTemplateVars('x{{my_phone}}y', { my_phone: '' })).toBe('xy');
  });
});

describe('recipientGreeting', () => {
  it('uses "cher" for males', () => {
    expect(recipientGreeting('Karim', 'male')).toBe('cher Karim');
  });
  it('uses "chère" for females', () => {
    expect(recipientGreeting('Marie', 'female')).toBe('chère Marie');
  });
  it('uses the plain name when gender is unknown', () => {
    expect(recipientGreeting('Jean', undefined)).toBe('Jean');
    expect(recipientGreeting('Jean', '')).toBe('Jean');
    expect(recipientGreeting('Jean', 'other')).toBe('Jean');
  });
  it('returns empty for an empty name', () => {
    expect(recipientGreeting('', 'male')).toBe('');
  });
  it('is case-insensitive on gender values', () => {
    expect(recipientGreeting('Karim', 'Male')).toBe('cher Karim');
  });
});