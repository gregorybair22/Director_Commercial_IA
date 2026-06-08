namespace DirectorComercialIA.Mail.Models;

public enum ContactPhaseNumber
{
    Phase1 = 1,
    Phase2 = 2,
    Phase3 = 3,
    Phase4 = 4,
    Phase5 = 5
}

public enum ContactWorkflowStatus
{
    Active = 0,
    SequenceCompletedNoReply = 1,
    FinalPending = 2,
    ClosedNoReply = 3
}

public enum BounceType
{
    None = 0,
    UnknownUser = 1,
    InvalidRecipient = 2,
    MailboxUnavailable = 3,
    BlockedRejected = 4,
    TemporaryFailure = 5,
    Other = 99
}

public enum EmailSendStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public enum DuplicateImportMode
{
    Ignore = 0,
    Update = 1,
    NewOnly = 2
}
