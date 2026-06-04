/// <reference types="cypress" />

const uniqueSuffix = () => Date.now().toString(36) + Math.random().toString(36).slice(2, 6);

interface UsersFixture {
  login: { email: string; password: string };
  signup: { firstName: string; lastName: string; email: string; password: string };
}

describe('Auth flow — sign in & sign up', () => {
  let users: UsersFixture;

  before(() => {
    cy.fixture('users').then((data: UsersFixture) => {
      users = data;
    });
  });

  beforeEach(() => {
    cy.intercept('POST', '/api/auth/login').as('loginReq');
    cy.intercept('POST', '/api/auth/register').as('registerReq');
    cy.intercept('GET', '/api/auth/me').as('meReq');
  });

  describe('Login page rendering', () => {
    it('shows the login page with sign-in active by default', () => {
      cy.visit('/login');
      cy.location('pathname').should('eq', '/login');
      cy.get('form.sign-in-form h2').should('contain.text', 'Sign in');
      cy.get('form.sign-up-form h2').should('contain.text', 'Sign Up');
      cy.get('section.container').should('not.have.class', 'sign-up-mode');
    });

    it('toggles to sign-up mode and back', () => {
      cy.visit('/login');
      cy.get('.panel.left-panel button.btn.transparent').click();
      cy.get('section.container').should('have.class', 'sign-up-mode');

      cy.get('.panel.right-panel button.btn.transparent').click();
      cy.get('section.container').should('not.have.class', 'sign-up-mode');
    });

    it('opens directly in sign-up mode via ?mode=sign-up', () => {
      cy.visit('/login?mode=sign-up');
      cy.get('section.container').should('have.class', 'sign-up-mode');
    });
  });

  describe('Sign-up validation (client-side)', () => {
    beforeEach(() => {
      cy.visit('/login?mode=sign-up');
      cy.disableNativeValidation();
    });

    it('rejects submission when fields are empty', () => {
      cy.get('form.sign-up-form button[type="submit"]').click();
      cy.get('form.sign-up-form .alert.error').should('contain.text', 'All fields are required');
    });

    it('rejects password shorter than 8 characters', () => {
      cy.get('input[name="signUpFirstName"]').type('Short');
      cy.get('input[name="signUpLastName"]').type('Pwd');
      cy.get('input[name="signUpEmail"]').type(`short+${uniqueSuffix()}@example.com`);
      cy.get('input[name="signUpPassword"]').type('1234567');
      cy.get('input[name="signUpConfirm"]').type('1234567');
      cy.get('form.sign-up-form button[type="submit"]').click();
      cy.get('form.sign-up-form .alert.error').should('contain.text', 'at least 8 characters');
    });

    it('rejects mismatched passwords', () => {
      cy.get('input[name="signUpFirstName"]').type('Mismatch');
      cy.get('input[name="signUpLastName"]').type('User');
      cy.get('input[name="signUpEmail"]').type(`mismatch+${uniqueSuffix()}@example.com`);
      cy.get('input[name="signUpPassword"]').type('P@ssw0rd123!');
      cy.get('input[name="signUpConfirm"]').type('Different123!');
      cy.get('form.sign-up-form button[type="submit"]').click();
      cy.get('form.sign-up-form .alert.error').should('contain.text', 'Passwords do not match');
    });
  });

  describe('Sign-up — register the fixture user', () => {
    it('registers mohssinengu@gmail.com (idempotent: 200 first time, 409 if already exists)', () => {
      cy.visit('/login?mode=sign-up');
      cy.disableNativeValidation();

      cy.get('input[name="signUpFirstName"]').type(users.signup.firstName);
      cy.get('input[name="signUpLastName"]').type(users.signup.lastName);
      cy.get('input[name="signUpEmail"]').type(users.signup.email);
      cy.get('input[name="signUpPassword"]').type(users.signup.password, { log: false });
      cy.get('input[name="signUpConfirm"]').type(users.signup.password, { log: false });

      cy.get('form.sign-up-form button[type="submit"]').click();

      cy.wait('@registerReq', { timeout: 30000 }).then((interception) => {
        const status = interception.response?.statusCode;
        expect(status, 'register status').to.be.oneOf([200, 409]);
        if (status === 200) {
          cy.get('form.sign-up-form .alert.success', { timeout: 5000 })
            .should('contain.text', 'Account created');
        } else {
          cy.get('form.sign-up-form .alert.error', { timeout: 5000 })
            .should('contain.text', 'already exists');
        }
      });
    });
  });

  describe('Sign-in validation (client-side)', () => {
    beforeEach(() => {
      cy.visit('/login');
      cy.disableNativeValidation();
    });

    it('rejects submission when email and password are empty', () => {
      cy.get('form.sign-in-form button[type="submit"]').click();
      cy.get('form.sign-in-form .alert.error').should('contain.text', 'Please enter your email and password');
    });
  });

  describe('Sign-in — bad credentials', () => {
    it('shows an error for wrong password', () => {
      cy.visit('/login');
      cy.disableNativeValidation();

      cy.get('input[name="signInEmail"]').type(`no-such-user+${uniqueSuffix()}@example.com`);
      cy.get('input[name="signInPassword"]').type('definitely-not-the-right-password');
      cy.get('form.sign-in-form button[type="submit"]').click();

      cy.wait('@loginReq', { timeout: 30000 }).then((interception) => {
        expect(interception.response?.statusCode, 'login status').to.eq(401);
      });

      cy.get('form.sign-in-form .alert.error', { timeout: 5000 })
        .should('contain.text', 'Invalid email or password');
      cy.location('pathname').should('eq', '/login');
    });
  });

  describe('Sign-in — happy path with fixture credentials', () => {
    it('logs in and redirects to /applications', () => {
      cy.visit('/login');
      cy.disableNativeValidation();

      cy.get('input[name="signInEmail"]').type(users.login.email);
      cy.get('input[name="signInPassword"]').type(users.login.password, { log: false });
      cy.get('form.sign-in-form button[type="submit"]').click();

      cy.wait('@loginReq', { timeout: 30000 }).then((interception) => {
        const status = interception.response?.statusCode;
        const body = interception.response?.body;
        expect(status, 'login status').to.eq(200);
        expect(body?.success, 'login success flag').to.eq(true);
        expect(body?.data?.email, 'returned email').to.be.a('string');
      });

      cy.location('pathname', { timeout: 10000 }).should('eq', '/applications');

      cy.request('/api/auth/me').then((res) => {
        expect(res.status).to.eq(200);
        expect(res.body?.success).to.eq(true);
        expect(res.body?.data?.email).to.be.a('string');
      });
    });
  });
});
