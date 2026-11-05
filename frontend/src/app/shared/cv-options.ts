import { CvDocumentDto } from '../models/document.model';

export interface CvVersionOption {
  id: string;
  label: string;
}

/** One CV/version pair used while building the option list. */
interface FlatVersion {
  id: string;
  label: string;
  isActive: boolean;
}

/**
 * Builds the CV-version option list used by pickers (apply/send flows): only ACTIVE
 * versions appear; when none is active (fresh install / everything archived), the full
 * list is returned with `onlyActive=false` so the caller can show a curation hint
 * instead of dead-ending the form.
 */
export function buildCvOptions(
  cvs: CvDocumentDto[],
  label: (cv: CvDocumentDto, versionNumber: number, versionLabel: string) => string = defaultLabel,
): { options: CvVersionOption[]; onlyActive: boolean } {
  const all: FlatVersion[] = [];
  for (const cv of cvs) {
    for (const v of cv.versions) {
      all.push({ id: v.id, label: label(cv, v.versionNumber, v.label), isActive: v.isActive });
    }
  }
  if (all.some((v) => v.isActive)) {
    return { options: all.filter((v) => v.isActive).map(({ id, label: l }) => ({ id, label: l })), onlyActive: true };
  }
  return { options: all.map(({ id, label: l }) => ({ id, label: l })), onlyActive: false };
}

function defaultLabel(cv: CvDocumentDto, versionNumber: number, versionLabel: string): string {
  return `${cv.title} — v${versionNumber}${versionLabel ? ' · ' + versionLabel : ''}`;
}

/** Human hint shown under a picker whose list fell back to all versions. */
export const CV_PICKER_FALLBACK_HINT =
  'No active CV versions — showing all. Mark versions active in Documents › CVs to curate this list.';
