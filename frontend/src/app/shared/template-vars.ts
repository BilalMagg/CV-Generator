export type TemplateVarValues = Record<string, string | number | null | undefined>;

/** Resolve {{token}} placeholders (case- and whitespace-insensitive). Unknown tokens stay verbatim. */
export function resolveTemplateVars(text: string, values: TemplateVarValues): string {
  return text.replace(/\{\{\s*([\w-]+)\s*\}\}/g, (match, key: string) => {
    const value = values[key.toLowerCase()];
    return value === undefined || value === null ? match : String(value);
  });
}

export const CONTACT_GENDERS = ['male', 'female'] as const;
export type ContactGender = (typeof CONTACT_GENDERS)[number] | '';

/** French-correct salutation: "cher Prénom" / "chère Prénom", or plain name when gender is unknown. */
export function recipientGreeting(name: string, gender?: string | null): string {
  const trimmed = (name || '').trim();
  if (!trimmed) return '';
  const g = (gender || '').trim().toLowerCase();
  if (g === 'male') return `cher ${trimmed}`;
  if (g === 'female') return `chère ${trimmed}`;
  return trimmed;
}