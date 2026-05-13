namespace NotificationService.Domain.Enums;

public enum NotificationType
{
    // Account
    Welcome,
    EmailVerification,
    PasswordReset,
    PasswordChanged,

    // CV
    CvGenerated,
    CvExportReady,

    // Applications
    ApplicationCreated,
    ApplicationStatusChanged,
    ApplicationNoResponseOneWeek,
    ApplicationNoResponseTwoWeeks,

    // Profile
    ProfileIncomplete,
    ProfileInactive,

    // Digest
    WeeklyDigest
}