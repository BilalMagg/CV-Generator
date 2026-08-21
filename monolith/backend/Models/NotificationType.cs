namespace CV_Generator.Models;

public enum NotificationType
{
    Welcome,
    EmailVerification,
    PasswordReset,
    PasswordChanged,
    CvGenerated,
    CvExportReady,
    ApplicationCreated,
    ApplicationStatusChanged,
    ApplicationNoResponseOneWeek,
    ApplicationNoResponseTwoWeeks,
    ProfileIncomplete,
    ProfileInactive,
    WeeklyDigest,
    UserReminder
}
