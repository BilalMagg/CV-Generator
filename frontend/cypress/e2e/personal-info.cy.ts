/// <reference types="cypress" />

interface ProfileFixture {
  firstName: string;
  lastName: string;
  headline: string;
  phoneNumber: string;
  birthDate: string;
  city: string;
  country: string;
  authorizedCountry: string;
  noticePeriod: string;
  desiredJobTitle: string;
  desiredSalaryMin: number;
  desiredSalaryMax: number;
  bio: string;
  employmentTypeToToggle: string;
  remotePreference: string;
  willingToRelocate: string;
}

const clearAndType = (selector: string, value: string | number | undefined) => {
  if (value === undefined || value === null || value === '') return;
  cy.get(selector).clear().type(String(value));
};

describe('Personal info — load and update profile', () => {
  let profile: ProfileFixture;

  before(() => {
    cy.fixture('profile').then((data: ProfileFixture) => {
      profile = data;
    });
  });

  beforeEach(() => {
    cy.loginAsUser();
    cy.intercept('GET', '/api/users/me').as('getProfile');
    cy.intercept('PUT', '/api/users/*').as('updateProfile');
  });

  it('loads the profile page with the user data', () => {
    cy.visit('/profile');
    cy.wait('@getProfile', { timeout: 20000 });
    cy.get('.pi-page h1').should('contain.text', 'Personal Info');
    cy.get('.avatar-name', { timeout: 10000 }).should('not.contain.text', 'User');
    cy.get('#email').should('be.disabled');
  });

  it('updates the profile with fixture data and shows a success banner', () => {
    cy.visit('/profile');
    cy.wait('@getProfile', { timeout: 20000 });

    cy.get('#firstName', { timeout: 10000 }).should('not.have.value', '');

    clearAndType('#firstName', profile.firstName);
    clearAndType('#lastName', profile.lastName);
    clearAndType('#headline', profile.headline);
    clearAndType('#phone', profile.phoneNumber);
    clearAndType('#birthDate', profile.birthDate);
    clearAndType('#city', profile.city);
    clearAndType('#country', profile.country);
    clearAndType('#authorizedCountry', profile.authorizedCountry);
    cy.get('#noticePeriod').select(profile.noticePeriod);

    clearAndType('#desiredJobTitle', profile.desiredJobTitle);
    clearAndType('#salaryMin', profile.desiredSalaryMin);
    clearAndType('#salaryMax', profile.desiredSalaryMax);
    clearAndType('textarea[name="bio"]', profile.bio);

    cy.contains('.chip-group .chip', profile.employmentTypeToToggle).then(($chip) => {
      if (!$chip.hasClass('active')) {
        cy.wrap($chip).click();
      }
    });
    cy.contains('.chip-group .chip', profile.remotePreference).then(($chip) => {
      if (!$chip.hasClass('active')) {
        cy.wrap($chip).click();
      }
    });
    cy.contains('.chip-group .chip', profile.willingToRelocate).then(($chip) => {
      if (!$chip.hasClass('active')) {
        cy.wrap($chip).click();
      }
    });

    cy.contains('button.btn.btn-primary', /Save changes/i).click();

    cy.wait('@updateProfile', { timeout: 30000 }).then((interception) => {
      const status = interception.response?.statusCode;
      const body = interception.request?.body as Record<string, unknown>;
      expect(status, 'update status').to.be.oneOf([200, 204]);
      expect(body?.headline).to.eq(profile.headline);
      expect(body?.city).to.eq(profile.city);
      expect(body?.country).to.eq(profile.country);
      expect(body?.desiredJobTitle).to.eq(profile.desiredJobTitle);
      expect(body?.bio).to.eq(profile.bio);
    });

    cy.get('.success-banner', { timeout: 5000 }).should('contain.text', 'Profile saved successfully');
  });

  it('adds a professional title', () => {
    cy.visit('/profile');
    cy.wait('@getProfile', { timeout: 20000 });

    const newTitle = `E2E Title ${Date.now().toString(36)}`;
    cy.contains('button.add-title-btn', /Add title/i).click();
    cy.get('.title-form input[type="text"]').type(newTitle);
    cy.contains('.title-form .btn.btn-primary', /Save/i).click();

    cy.get('.titles-list .title-text').should('contain.text', newTitle);
  });
});
