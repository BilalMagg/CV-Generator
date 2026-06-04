/// <reference types="cypress" />

interface ReminderFixture {
  title: string;
  message: string;
  reminderOffset: string;
  daysFromNow: number;
}

const toDateTimeLocal = (d: Date): string => {
  const pad = (n: number) => String(n).padStart(2, '0');
  return (
    `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` +
    `T${pad(d.getHours())}:${pad(d.getMinutes())}`
  );
};

describe('Calendar — create a reminder', () => {
  let reminder: ReminderFixture;

  before(() => {
    cy.fixture('reminder').then((data: ReminderFixture) => {
      reminder = data;
    });
  });

  beforeEach(() => {
    cy.loginAsUser();
    cy.intercept('POST', '/api/reminders').as('createReminder');
    cy.intercept('GET', '/api/reminders/*').as('listReminders');
  });

  it('opens the reminder modal from the upcoming panel', () => {
    cy.visit('/applications/calendar');
    cy.contains('button.add-reminder-btn', 'Add').click();
    cy.get('.modal-content').should('be.visible');
    cy.get('.modal-header h3').should('contain.text', 'Create Reminder');
  });

  it('rejects submission when title and event date are missing', () => {
    cy.visit('/applications/calendar');
    cy.contains('button.add-reminder-btn', 'Add').click();
    cy.contains('button.btn-submit', /Create Reminder/i).click();
    cy.get('.modal-body .alert-error').should('contain.text', 'Title and event date are required');
  });

  it('creates a reminder from fixture data and shows the success banner', () => {
    cy.visit('/applications/calendar');
    cy.wait('@listReminders', { timeout: 15000 });

    cy.contains('button.add-reminder-btn', 'Add').click();
    cy.get('.modal-content').should('be.visible');

    const eventDate = new Date();
    eventDate.setDate(eventDate.getDate() + reminder.daysFromNow);
    eventDate.setHours(10, 0, 0, 0);

    cy.get('.modal-body input[type="text"]').type(reminder.title);
    cy.get('.modal-body input[type="datetime-local"]').type(toDateTimeLocal(eventDate));
    cy.get('.modal-body select').select(reminder.reminderOffset);
    cy.get('.modal-body textarea').type(reminder.message);

    cy.contains('button.btn-submit', /Create Reminder/i).click();

    cy.wait('@createReminder', { timeout: 30000 }).then((interception) => {
      const status = interception.response?.statusCode;
      expect(status, 'create reminder status').to.be.oneOf([200, 201]);
      expect(interception.request.body?.title).to.eq(reminder.title);
      expect(interception.request.body?.reminderOffset).to.eq(reminder.reminderOffset);
    });

    cy.wait('@listReminders', { timeout: 15000 });
    cy.contains('.upcoming-list .item-type', reminder.title, { timeout: 10000 }).should('be.visible');
  });
});
