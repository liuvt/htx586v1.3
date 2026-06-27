SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
  HTX586CONTRACT - nâng cấp tương thích cho database đã tồn tại.
  Database mới được tạo bởi EnsureCreatedAsync từ EF Core model hiện hành.
  Script này chỉ bổ sung các thành phần cần cho danh sách hành khách,
  chữ ký và thông tin PDF khi nâng cấp từ bản cũ.
*/

IF OBJECT_ID(N'dbo.Contracts', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Contracts', N'PdfFileUrl') IS NULL
        ALTER TABLE dbo.Contracts ADD PdfFileUrl nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Contracts', N'PdfSha256') IS NULL
        ALTER TABLE dbo.Contracts ADD PdfSha256 nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Contracts', N'PdfGeneratedAt') IS NULL
        ALTER TABLE dbo.Contracts ADD PdfGeneratedAt datetime2 NULL;
    IF COL_LENGTH(N'dbo.Contracts', N'ContractHash') IS NULL
        ALTER TABLE dbo.Contracts ADD ContractHash nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Contracts', N'ActualPassengerCount') IS NULL
        ALTER TABLE dbo.Contracts ADD ActualPassengerCount int NULL;
    IF COL_LENGTH(N'dbo.Contracts', N'SecondDriverName') IS NULL
        ALTER TABLE dbo.Contracts ADD SecondDriverName nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.Contracts', N'SecondDriverLicenseClass') IS NULL
        ALTER TABLE dbo.Contracts ADD SecondDriverLicenseClass nvarchar(max) NULL;
END;

IF OBJECT_ID(N'dbo.ContractPassengers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContractPassengers
    (
        Id uniqueidentifier NOT NULL,
        ContractId uniqueidentifier NOT NULL,
        SortOrder int NOT NULL,
        FullName nvarchar(200) NOT NULL,
        BirthYear int NULL,
        Note nvarchar(500) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ContractPassengers_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy nvarchar(max) NULL,
        UpdatedAt datetime2 NULL,
        UpdatedBy nvarchar(max) NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ContractPassengers_IsDeleted DEFAULT 0,
        DeletedAt datetime2 NULL,
        DeletedBy nvarchar(max) NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_ContractPassengers PRIMARY KEY (Id),
        CONSTRAINT FK_ContractPassengers_Contracts_ContractId
            FOREIGN KEY (ContractId) REFERENCES dbo.Contracts(Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ContractPassengers')
      AND name = N'IX_ContractPassengers_ContractId_SortOrder'
)
BEGIN
    CREATE UNIQUE INDEX IX_ContractPassengers_ContractId_SortOrder
        ON dbo.ContractPassengers(ContractId, SortOrder);
END;

IF OBJECT_ID(N'dbo.ContractSignatures', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContractSignatures
    (
        Id uniqueidentifier NOT NULL,
        ContractId uniqueidentifier NOT NULL,
        Party int NOT NULL,
        SignerName nvarchar(200) NOT NULL,
        SignerPhone nvarchar(20) NULL,
        SignatureFileUrl nvarchar(500) NOT NULL,
        SignatureVectorJson nvarchar(max) NULL,
        SignatureHash nvarchar(128) NOT NULL,
        ContractHashAtSigning nvarchar(128) NOT NULL,
        DeviceSignedAt datetime2 NOT NULL,
        ServerSignedAt datetime2 NOT NULL,
        Latitude decimal(10,7) NULL,
        Longitude decimal(10,7) NULL,
        LocationAccuracy float NULL,
        LocationAddress nvarchar(500) NULL,
        LocationError nvarchar(max) NULL,
        IpAddress nvarchar(64) NULL,
        DeviceId nvarchar(200) NULL,
        DeviceName nvarchar(max) NULL,
        OperatingSystem nvarchar(max) NULL,
        BrowserName nvarchar(max) NULL,
        AppVersion nvarchar(max) NULL,
        ConsentText nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ContractSignatures_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy nvarchar(max) NULL,
        UpdatedAt datetime2 NULL,
        UpdatedBy nvarchar(max) NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ContractSignatures_IsDeleted DEFAULT 0,
        DeletedAt datetime2 NULL,
        DeletedBy nvarchar(max) NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_ContractSignatures PRIMARY KEY (Id),
        CONSTRAINT FK_ContractSignatures_Contracts_ContractId
            FOREIGN KEY (ContractId) REFERENCES dbo.Contracts(Id),
        CONSTRAINT CK_ContractSignatures_Latitude
            CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
        CONSTRAINT CK_ContractSignatures_Longitude
            CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ContractSignatures')
      AND name = N'UX_ContractSignatures_Contract_Party'
)
BEGIN
    CREATE UNIQUE INDEX UX_ContractSignatures_Contract_Party
        ON dbo.ContractSignatures(ContractId, Party)
        WHERE IsDeleted = 0;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ContractSignatures')
      AND name = N'IX_ContractSignatures_ServerSignedAt'
)
BEGIN
    CREATE INDEX IX_ContractSignatures_ServerSignedAt
        ON dbo.ContractSignatures(ServerSignedAt DESC);
END;
