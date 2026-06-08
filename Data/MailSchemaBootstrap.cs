using System.Data.Common;

namespace DirectorComercialIA.Data;

public static class MailSchemaBootstrap
{
    public static void EnsureMailTables(DbConnection conn)
    {
        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS MailImportBatches (
                Id INTEGER NOT NULL CONSTRAINT PK_MailImportBatches PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL DEFAULT '',
                CampaignName TEXT NULL,
                ImportedByUserId TEXT NOT NULL DEFAULT '',
                ImportedAt TEXT NOT NULL,
                TotalRows INTEGER NOT NULL DEFAULT 0,
                ValidRows INTEGER NOT NULL DEFAULT 0,
                InvalidRows INTEGER NOT NULL DEFAULT 0,
                NewRows INTEGER NOT NULL DEFAULT 0,
                UpdatedRows INTEGER NOT NULL DEFAULT 0,
                IgnoredDuplicates INTEGER NOT NULL DEFAULT 0
            );",
            @"CREATE TABLE IF NOT EXISTS MailContacts (
                Id INTEGER NOT NULL CONSTRAINT PK_MailContacts PRIMARY KEY AUTOINCREMENT,
                FirstName TEXT NOT NULL DEFAULT '',
                LastName TEXT NOT NULL DEFAULT '',
                Company TEXT NOT NULL DEFAULT '',
                Email TEXT NOT NULL DEFAULT '',
                Country TEXT NOT NULL DEFAULT '',
                Language TEXT NOT NULL DEFAULT '',
                CustomerType TEXT NOT NULL DEFAULT '',
                JobTitle TEXT NOT NULL DEFAULT '',
                Phone TEXT NOT NULL DEFAULT '',
                Website TEXT NOT NULL DEFAULT '',
                City TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                AssignedUserId TEXT NULL,
                CurrentPhase INTEGER NOT NULL DEFAULT 1,
                CurrentStatus INTEGER NOT NULL DEFAULT 0,
                IsResponded INTEGER NOT NULL DEFAULT 0,
                IsDoNotContact INTEGER NOT NULL DEFAULT 0,
                IsExcluded INTEGER NOT NULL DEFAULT 0,
                IsBounced INTEGER NOT NULL DEFAULT 0,
                BounceType INTEGER NOT NULL DEFAULT 0,
                BounceReason TEXT NULL,
                IsHardBounce INTEGER NOT NULL DEFAULT 0,
                AdminOverrideAllowNextSend INTEGER NOT NULL DEFAULT 0,
                ImportBatchId INTEGER NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastReplyCheckAt TEXT NULL,
                LastReplyReceivedAt TEXT NULL,
                Send1At TEXT NULL,
                Send2At TEXT NULL,
                Send3At TEXT NULL,
                Send4At TEXT NULL,
                Send5At TEXT NULL,
                FOREIGN KEY (ImportBatchId) REFERENCES MailImportBatches(Id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MailContacts_Email ON MailContacts(Email);
            CREATE INDEX IF NOT EXISTS IX_MailContacts_CurrentPhase ON MailContacts(CurrentPhase);
            CREATE INDEX IF NOT EXISTS IX_MailContacts_ImportBatchId ON MailContacts(ImportBatchId);",
            @"CREATE TABLE IF NOT EXISTS MailContactComments (
                Id INTEGER NOT NULL CONSTRAINT PK_MailContactComments PRIMARY KEY AUTOINCREMENT,
                ContactId INTEGER NOT NULL,
                UserId TEXT NOT NULL DEFAULT '',
                Comment TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (ContactId) REFERENCES MailContacts(Id) ON DELETE CASCADE
            );",
            @"CREATE TABLE IF NOT EXISTS MailContactPhaseHistories (
                Id INTEGER NOT NULL CONSTRAINT PK_MailContactPhaseHistories PRIMARY KEY AUTOINCREMENT,
                ContactId INTEGER NOT NULL,
                FromPhase INTEGER NOT NULL,
                ToPhase INTEGER NOT NULL,
                Reason TEXT NOT NULL DEFAULT '',
                UserId TEXT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (ContactId) REFERENCES MailContacts(Id) ON DELETE CASCADE
            );",
            @"CREATE TABLE IF NOT EXISTS MailEmailTemplates (
                Id INTEGER NOT NULL CONSTRAINT PK_MailEmailTemplates PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL DEFAULT '',
                PhaseNumber INTEGER NOT NULL,
                Language TEXT NOT NULL DEFAULT 'en',
                SubjectTemplate TEXT NOT NULL DEFAULT '',
                BodyTemplate TEXT NOT NULL DEFAULT '',
                IsDefault INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS MailEmailSendLogs (
                Id INTEGER NOT NULL CONSTRAINT PK_MailEmailSendLogs PRIMARY KEY AUTOINCREMENT,
                ContactId INTEGER NOT NULL,
                PhaseNumber INTEGER NOT NULL,
                GmailMessageId TEXT NULL,
                GmailThreadId TEXT NULL,
                Subject TEXT NOT NULL DEFAULT '',
                BodySnapshot TEXT NOT NULL DEFAULT '',
                SentByUserId TEXT NOT NULL DEFAULT '',
                SentAt TEXT NOT NULL,
                SendStatus INTEGER NOT NULL DEFAULT 0,
                ErrorMessage TEXT NULL,
                DeliveryStatus TEXT NULL,
                BounceDetected INTEGER NOT NULL DEFAULT 0,
                BounceType INTEGER NOT NULL DEFAULT 0,
                BounceReason TEXT NULL,
                IsHardBounce INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ContactId) REFERENCES MailContacts(Id) ON DELETE CASCADE
            );",
            @"CREATE TABLE IF NOT EXISTS MailEmailReplyLogs (
                Id INTEGER NOT NULL CONSTRAINT PK_MailEmailReplyLogs PRIMARY KEY AUTOINCREMENT,
                ContactId INTEGER NOT NULL,
                GmailMessageId TEXT NULL,
                GmailThreadId TEXT NULL,
                FromEmail TEXT NOT NULL DEFAULT '',
                Subject TEXT NOT NULL DEFAULT '',
                BodySnippet TEXT NOT NULL DEFAULT '',
                ReceivedAt TEXT NOT NULL,
                DetectedAt TEXT NOT NULL,
                FOREIGN KEY (ContactId) REFERENCES MailContacts(Id) ON DELETE CASCADE
            );",
            @"CREATE TABLE IF NOT EXISTS MailGmailConnections (
                Id INTEGER NOT NULL CONSTRAINT PK_MailGmailConnections PRIMARY KEY AUTOINCREMENT,
                ConnectedEmail TEXT NOT NULL DEFAULT '',
                DisplayName TEXT NULL,
                RefreshTokenProtected TEXT NOT NULL DEFAULT '',
                ConnectedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );"
        };

        foreach (var sql in commands)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
