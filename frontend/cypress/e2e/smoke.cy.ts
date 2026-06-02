describe('Smoke tests', () => {
  it('should load the home page', () => {
    cy.visit('/');
    cy.contains('Propel').should('be.visible');
  });

  it('should load the login page', () => {
    cy.visit('/login');
    cy.url().should('include', '/login');
  });
});
