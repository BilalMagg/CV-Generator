/// <reference types="cypress" />

type EntityKey = 'experiences' | 'projects' | 'skills' | 'educations';

interface CareerFixture {
  experiences: Record<string, string>;
  projects: Record<string, string>;
  skills: Record<string, string | number>;
  educations: Record<string, string>;
}

const fillField = (name: string, value: string | number) => {
  if (value === '' || value === null || value === undefined) return;
  cy.get(`[name="${name}"]`).then(($el) => {
    const tag = $el.prop('tagName')?.toString().toLowerCase();
    const type = ($el.attr('type') || '').toLowerCase();

    if (tag === 'select') {
      cy.wrap($el).select(String(value));
    } else if (type === 'date' || type === 'number') {
      cy.wrap($el).clear().type(String(value));
    } else if (tag === 'textarea') {
      cy.wrap($el).clear().type(String(value));
    } else {
      cy.wrap($el).clear().type(String(value));
    }
  });
};

describe('My Career — add entities', () => {
  let career: CareerFixture;

  before(() => {
    cy.fixture('career').then((data: CareerFixture) => {
      career = data;
    });
  });

  beforeEach(() => {
    cy.loginAsUser();
  });

  const cases: EntityKey[] = ['experiences', 'projects', 'skills', 'educations'];

  cases.forEach((entity) => {
    it(`adds a new ${entity} entry from fixture and lands on the list`, () => {
      const alias = `create_${entity}`;
      cy.intercept('POST', `/api/user-content/${entity}`).as(alias);

      cy.visit(`/my-career/${entity}/add`);
      cy.disableNativeValidation();

      cy.get('form.dynamic-form').should('be.visible');

      const data = career[entity];
      Object.entries(data).forEach(([key, value]) => {
        fillField(key, value as string | number);
      });

      cy.get('form.dynamic-form button[type="submit"]').click();

      cy.wait(`@${alias}`, { timeout: 30000 }).then((interception) => {
        const status = interception.response?.statusCode;
        expect(status, `${entity} create status`).to.be.oneOf([200, 201]);
      });

      cy.location('pathname', { timeout: 10000 }).should('eq', `/my-career/${entity}`);
    });
  });
});
