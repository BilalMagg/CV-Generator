/// <reference types="cypress" />

interface ApplicationFixture {
  companyName: string;
  positionTitle: string;
  offerSource: string;
  notes: string;
}

describe('Applications — create a new application', () => {
  let appData: ApplicationFixture;

  before(() => {
    cy.fixture('application').then((data: ApplicationFixture) => {
      appData = data;
    });
  });

  beforeEach(() => {
    cy.loginAsUser();
    cy.intercept('POST', '/api/applications').as('createApp');
    cy.intercept('GET', '/api/applications/*').as('getApp');
  });

  it('rejects submission while company name and title are empty', () => {
    cy.visit('/applications/new');
    cy.get('button.create-btn').should('be.disabled');
    cy.get('input[name="companyName"]').type(appData.companyName);
    cy.get('button.create-btn').should('be.disabled');
  });

  it('creates an application from fixture data and navigates to its detail page', () => {
    cy.visit('/applications/new');
    cy.disableNativeValidation();

    cy.get('input[name="companyName"]').type(appData.companyName);
    cy.get('input[name="positionTitle"]').type(appData.positionTitle);
    cy.get('select[name="offerSource"]').select(appData.offerSource);
    cy.get('textarea[name="notes"]').type(appData.notes);

    cy.get('button.create-btn').should('not.be.disabled').click();

    cy.wait('@createApp', { timeout: 30000 }).then((interception) => {
      const status = interception.response?.statusCode;
      const body = interception.response?.body;
      expect(status, 'create status').to.be.oneOf([200, 201]);
      expect(body?.success, 'create success flag').to.eq(true);
      expect(body?.data?.id, 'returned id').to.be.a('string');
      expect(body?.data?.companyName).to.eq(appData.companyName);
      expect(body?.data?.positionTitle).to.eq(appData.positionTitle);

      cy.location('pathname', { timeout: 10000 })
        .should('match', new RegExp(`^/applications/${body.data.id}$`));
    });
  });
});
