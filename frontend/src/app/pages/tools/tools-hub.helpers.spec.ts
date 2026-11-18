import { extractFirstLinkedin, guessCompanyFromText } from './tools-hub.component';

describe('extractFirstLinkedin', () => {
  it('finds a bare linkedin in/profile URL and normalizes it', () => {
    expect(extractFirstLinkedin('ping me at linkedin.com/in/leila-benali')).toBe(
      'https://linkedin.com/in/leila-benali',
    );
  });

  it('keeps an absolute URL as-is', () => {
    expect(extractFirstLinkedin('https://www.linkedin.com/in/sara_dev')).toBe(
      'https://www.linkedin.com/in/sara_dev',
    );
  });

  it('returns empty when there is no linkedin url', () => {
    expect(extractFirstLinkedin('works at Capgemini, +212 600 000 000')).toBe('');
  });
});

describe('guessCompanyFromText', () => {
  it('extracts the company after "recruiter at"', () => {
    expect(guessCompanyFromText('She is a tech recruiter at Capgemini Morocco.')).toBe('Capgemini Morocco');
  });

  it('extracts the company after "works at"', () => {
    expect(guessCompanyFromText('He works at Inetum Maroc')).toBe('Inetum Maroc');
  });

  it('extracts the company after "talent acquisition at"', () => {
    expect(guessCompanyFromText('Talent acquisition at Orange. Hiring a data engineer.')).toBe('Orange');
  });

  it('stops at lowercase following words', () => {
    expect(guessCompanyFromText('Hiring at Capgemini for a PFE internship.')).toBe('Capgemini');
  });

  it('returns empty when no role-company pattern matches', () => {
    expect(guessCompanyFromText('Please send your CV to hr@capgemini.com')).toBe('');
  });
});
