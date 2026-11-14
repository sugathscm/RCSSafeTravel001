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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [Alerts] (
        [Id] int NOT NULL IDENTITY,
        [Level] int NOT NULL,
        [Type] nvarchar(60) NOT NULL,
        [Area] nvarchar(120) NOT NULL,
        [Title] nvarchar(240) NOT NULL,
        [Source] nvarchar(120) NOT NULL,
        [PublishedUtc] datetime2 NOT NULL,
        [ExpiresUtc] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Alerts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [WhenUtc] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(200) NOT NULL,
        [Action] nvarchar(400) NOT NULL,
        [Entity] nvarchar(60) NULL,
        [EntityId] nvarchar(40) NULL,
        [Ip] nvarchar(64) NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Slug] nvarchar(40) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Icon] nvarchar(16) NOT NULL,
        [Kind] int NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [EmergencyNumbers] (
        [Id] int NOT NULL IDENTITY,
        [Label] nvarchar(60) NOT NULL,
        [Number] nvarchar(30) NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_EmergencyNumbers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [SiteSettings] (
        [Key] nvarchar(80) NOT NULL,
        [Value] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Key])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [Sponsors] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Role] nvarchar(120) NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_Sponsors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [BusinessName] nvarchar(160) NOT NULL,
        [ContactName] nvarchar(120) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [Status] int NOT NULL,
        [RejectReason] nvarchar(400) NULL,
        [PaidUntil] date NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [ApprovedUtc] datetime2 NULL,
        [Views] int NOT NULL,
        [Calls] int NOT NULL,
        [Directions] int NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [Topics] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Summary] nvarchar(400) NOT NULL,
        [Icon] nvarchar(16) NOT NULL,
        [Tone] nvarchar(20) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Topics] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(120) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [SupplierId] int NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [Listings] (
        [Id] int NOT NULL IDENTITY,
        [CategoryId] int NOT NULL,
        [SupplierId] int NULL,
        [Name] nvarchar(160) NOT NULL,
        [Town] nvarchar(80) NOT NULL,
        [District] nvarchar(60) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [Hours] nvarchar(160) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [Status] int NOT NULL,
        [IsVerified] bit NOT NULL,
        [IsFeatured] bit NOT NULL,
        [RatingAverage] float NOT NULL,
        [RatingCount] int NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Listings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Listings_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Listings_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [Kind] int NOT NULL,
        [SupplierId] int NULL,
        [PayerName] nvarchar(160) NOT NULL,
        [PayerEmail] nvarchar(200) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Status] int NOT NULL,
        [GatewayReference] nvarchar(80) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [PaidUtc] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [ReminderLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [SupplierId] int NOT NULL,
        [RenewalDate] date NOT NULL,
        [DaysBefore] int NOT NULL,
        [Channel] nvarchar(40) NOT NULL,
        [SentUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ReminderLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReminderLogs_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [SupplierDocuments] (
        [Id] int NOT NULL IDENTITY,
        [SupplierId] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [StoragePath] nvarchar(300) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Status] int NOT NULL,
        [UploadedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SupplierDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierDocuments_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] int NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TokenHash] nvarchar(88) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [ExpiresUtc] datetime2 NOT NULL,
        [RevokedUtc] datetime2 NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_IsActive_PublishedUtc] ON [Alerts] ([IsActive], [PublishedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_SupplierId] ON [AspNetUsers] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_WhenUtc] ON [AuditEntries] ([WhenUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_CategoryId] ON [Listings] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_CategoryId] ON [Listings] ([Status], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_District] ON [Listings] ([Status], [District]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_SupplierId] ON [Listings] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_CreatedUtc] ON [Payments] ([CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_Kind_Status] ON [Payments] ([Kind], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_SupplierId] ON [Payments] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReminderLogs_SupplierId_RenewalDate_DaysBefore] ON [ReminderLogs] ([SupplierId], [RenewalDate], [DaysBefore]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierDocuments_SupplierId] ON [SupplierDocuments] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_Email] ON [Suppliers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_PaidUntil] ON [Suppliers] ([PaidUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_Status] ON [Suppliers] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112090908_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112090908_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112134748_Advertisements'
)
BEGIN
    CREATE TABLE [Advertisements] (
        [Id] int NOT NULL IDENTITY,
        [Slot] int NOT NULL,
        [SponsorId] int NOT NULL,
        [Headline] nvarchar(120) NOT NULL,
        [Text] nvarchar(240) NOT NULL,
        [LinkLabel] nvarchar(40) NOT NULL,
        [LinkUrl] nvarchar(300) NOT NULL,
        [ImagePath] nvarchar(200) NULL,
        [IsActive] bit NOT NULL,
        [Clicks] int NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Advertisements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Advertisements_Sponsors_SponsorId] FOREIGN KEY ([SponsorId]) REFERENCES [Sponsors] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112134748_Advertisements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Advertisements_Slot] ON [Advertisements] ([Slot]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112134748_Advertisements'
)
BEGIN
    CREATE INDEX [IX_Advertisements_SponsorId] ON [Advertisements] ([SponsorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112134748_Advertisements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112134748_Advertisements', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112145342_ListingPromotions'
)
BEGIN
    ALTER TABLE [Listings] ADD [PromoText] nvarchar(300) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112145342_ListingPromotions'
)
BEGIN
    ALTER TABLE [Listings] ADD [PromoTitle] nvarchar(80) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112145342_ListingPromotions'
)
BEGIN
    ALTER TABLE [Listings] ADD [PromoUntil] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112145342_ListingPromotions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112145342_ListingPromotions', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112150727_Products'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [ListingId] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(400) NOT NULL,
        [Price] decimal(12,2) NULL,
        [Unit] nvarchar(40) NOT NULL,
        [ImagePath] nvarchar(200) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112150727_Products'
)
BEGIN
    CREATE INDEX [IX_Products_ListingId_SortOrder] ON [Products] ([ListingId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112150727_Products'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112150727_Products', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114033253_SafetyContent'
)
BEGIN
    ALTER TABLE [Topics] ADD [Donts] nvarchar(2000) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114033253_SafetyContent'
)
BEGIN
    ALTER TABLE [Topics] ADD [Dos] nvarchar(2000) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114033253_SafetyContent'
)
BEGIN
    CREATE TABLE [FirstAidGuides] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(120) NOT NULL,
        [Icon] nvarchar(16) NOT NULL,
        [Steps] nvarchar(4000) NOT NULL,
        [VideoUrl] nvarchar(300) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_FirstAidGuides] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114033253_SafetyContent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114033253_SafetyContent', N'10.0.15');
END;

COMMIT;
GO

