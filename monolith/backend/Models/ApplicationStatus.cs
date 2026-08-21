namespace CV_Generator.Models;

public enum ApplicationStatus
{
    SAVED,
    APPLIED,
    SCREENING,
    INTERVIEW,
    OFFER,
    ACCEPTED,
    REJECTED,
    WITHDRAWN
}

public enum ApplicationOrigin
{
    MANUAL,
    FROM_JOB_OFFER,
    AI_AGENT_AUTO_APPLY,
    IMPORT
}
