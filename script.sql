Build started...
Build succeeded.
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [User] (
    [Id] int NOT NULL IDENTITY,
    [UserName] nvarchar(max) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Tournaments] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [AdminUserId] int NOT NULL,
    CONSTRAINT [PK_Tournaments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tournaments_User_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [User] ([Id])
);
GO

CREATE TABLE [Players] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [RatingsCentralId] int NOT NULL,
    [Rating] int NOT NULL,
    [StDev] int NOT NULL,
    [Group] nvarchar(max) NULL,
    [TournamentId] int NOT NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_Players] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Players_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]),
    CONSTRAINT [FK_Players_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id])
);
GO

CREATE TABLE [Games] (
    [Id] int NOT NULL IDENTITY,
    [Player1Id] int NOT NULL,
    [Player2Id] int NOT NULL,
    [ScorePlayer1] int NOT NULL,
    [ScorePlayer2] int NOT NULL,
    [TournamentId] int NOT NULL,
    [Group] nvarchar(max) NULL,
    [Date] datetime2 NOT NULL,
    CONSTRAINT [PK_Games] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Games_Players_Player1Id] FOREIGN KEY ([Player1Id]) REFERENCES [Players] ([Id]),
    CONSTRAINT [FK_Games_Players_Player2Id] FOREIGN KEY ([Player2Id]) REFERENCES [Players] ([Id]),
    CONSTRAINT [FK_Games_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id])
);
GO

CREATE INDEX [IX_Games_Player1Id] ON [Games] ([Player1Id]);
GO

CREATE INDEX [IX_Games_Player2Id] ON [Games] ([Player2Id]);
GO

CREATE INDEX [IX_Games_TournamentId] ON [Games] ([TournamentId]);
GO

CREATE INDEX [IX_Players_TournamentId] ON [Players] ([TournamentId]);
GO

CREATE INDEX [IX_Players_UserId] ON [Players] ([UserId]);
GO

CREATE INDEX [IX_Tournaments_AdminUserId] ON [Tournaments] ([AdminUserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250422210825_Lacre', N'8.0.4');
GO

COMMIT;
GO


