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

CREATE TABLE [AspNetRoles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] int NOT NULL IDENTITY,
    [UserName] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] int NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Players] (
    [UserId] int NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [RatingsCentralId] int NOT NULL,
    [Rating] int NOT NULL,
    [StDev] int NOT NULL,
    [Group] nvarchar(max) NULL,
    [PlayerCode] nvarchar(max) NULL,
    CONSTRAINT [PK_Players] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_Players_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);
GO

CREATE TABLE [Tournaments] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [AdminUserId] int NULL,
    CONSTRAINT [PK_Tournaments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tournaments_AspNetUsers_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [AspNetUsers] ([Id])
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
    [Round] int NOT NULL,
    [GameNo] int NOT NULL,
    [PlayerUserId] int NULL,
    CONSTRAINT [PK_Games] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Games_Players_Player1Id] FOREIGN KEY ([Player1Id]) REFERENCES [Players] ([UserId]),
    CONSTRAINT [FK_Games_Players_Player2Id] FOREIGN KEY ([Player2Id]) REFERENCES [Players] ([UserId]),
    CONSTRAINT [FK_Games_Players_PlayerUserId] FOREIGN KEY ([PlayerUserId]) REFERENCES [Players] ([UserId]),
    CONSTRAINT [FK_Games_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [TournamentPlayers] (
    [TournamentId] int NOT NULL,
    [PlayerId] int NOT NULL,
    CONSTRAINT [PK_TournamentPlayers] PRIMARY KEY ([TournamentId], [PlayerId]),
    CONSTRAINT [FK_TournamentPlayers_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([UserId]) ON DELETE CASCADE,
    CONSTRAINT [FK_TournamentPlayers_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_Games_Player1Id] ON [Games] ([Player1Id]);
GO

CREATE INDEX [IX_Games_Player2Id] ON [Games] ([Player2Id]);
GO

CREATE INDEX [IX_Games_PlayerUserId] ON [Games] ([PlayerUserId]);
GO

CREATE INDEX [IX_Games_TournamentId] ON [Games] ([TournamentId]);
GO

CREATE INDEX [IX_TournamentPlayers_PlayerId] ON [TournamentPlayers] ([PlayerId]);
GO

CREATE INDEX [IX_Tournaments_AdminUserId] ON [Tournaments] ([AdminUserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250617130110_FirstMigration', N'8.0.0');
GO

COMMIT;
GO

