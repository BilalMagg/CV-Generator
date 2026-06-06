/// <reference types="cypress" />

declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace Cypress {
    interface Chainable {
      /**
       * Log in via POST /api/auth/login. Sets the auth cookie on the test browser.
       * Use in beforeEach for any spec that needs an authenticated session.
       */
      login(email: string, password: string): Chainable<void>;

      /**
       * Reads credentials from cypress/fixtures/users.json (login user) and signs in.
       */
      loginAsUser(): Chainable<void>;

      /**
       * Disables native HTML5 validation on every form on the current page so that
       * the app's own validation logic is what we test.
       */
      disableNativeValidation(): Chainable<void>;
    }
  }
}

Cypress.Commands.add('login', (email: string, password: string) => {
  cy.session(
    [email, password],
    () => {
      cy.request({
        method: 'POST',
        url: '/api/auth/login',
        body: { email, password },
        failOnStatusCode: false,
      }).then((res) => {
        expect(res.status, `login(${email}) status`).to.eq(200);
        expect(res.body?.success, `login(${email}) success flag`).to.eq(true);
      });
    },
    {
      validate() {
        cy.request({
          url: '/api/auth/me',
          failOnStatusCode: false,
        }).its('status').should('eq', 200);
      },
    }
  );
});

Cypress.Commands.add('loginAsUser', () => {
  cy.fixture('users').then((users) => {
    cy.login(users.login.email, users.login.password);
  });
});

Cypress.Commands.add('disableNativeValidation', () => {
  cy.get('form').each(($form) => {
    $form.attr('novalidate', '');
  });
});

export {};
