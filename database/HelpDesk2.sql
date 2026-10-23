USE HelpDesk;
GO

-- IF OBJECT_ID(N'dbo.RequestComments', N'U') IS NULL
-- BEGIN
--     CREATE TABLE RequestComments (
--         Id         INT IDENTITY CONSTRAINT PK_RequestComments PRIMARY KEY,
--         RequestId  INT NOT NULL CONSTRAINT FK_RequestComments_Requests REFERENCES Requests(Id) ON DELETE CASCADE,
--         EmployeeId INT NOT NULL CONSTRAINT FK_RequestComments_Employees REFERENCES Employees(Id),
--         Text       NVARCHAR(1000) NOT NULL CONSTRAINT CK_RequestComments_Text CHECK (LEN(LTRIM(RTRIM(Text))) > 0),
--         CreatedAt  DATETIME2 NOT NULL CONSTRAINT DF_RequestComments_CreatedAt DEFAULT SYSDATETIME()
--     );
-- END
-- ELSE IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_RequestComments_Text' AND parent_object_id = OBJECT_ID(N'dbo.RequestComments'))
-- BEGIN
--     ALTER TABLE RequestComments ADD CONSTRAINT CK_RequestComments_Text CHECK (LEN(LTRIM(RTRIM(Text))) > 0);
-- END
-- GO

-- IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RequestComments_RequestId' AND object_id = OBJECT_ID(N'dbo.RequestComments'))
--     CREATE INDEX IX_RequestComments_RequestId ON RequestComments(RequestId, CreatedAt);
-- GO
-- SELECT name, type_desc FROM sys.objects
-- WHERE parent_object_id = OBJECT_ID(N'dbo.RequestComments');

-- INSERT INTO RequestComments (RequestId, EmployeeId, Text) VALUES (1, 1, N'   ');
-- INSERT INTO RequestComments (RequestId, EmployeeId, Text) VALUES (999, 1, N'Тест');

-- BEGIN TRAN;
-- INSERT INTO RequestComments (RequestId, EmployeeId, Text) VALUES (1, 1, N'Проверка дат');
-- SELECT TOP 1 * FROM RequestComments ORDER BY Id DESC;
-- ROLLBACK;

-- INSERT INTO RequestComments (RequestId, EmployeeId, Text, CreatedAt)
-- VALUES (1, 2, N'Самый ранний комментарий', DATEADD(DAY, -1, SYSDATETIME()));
-- INSERT INTO RequestComments (RequestId, EmployeeId, Text)
-- VALUES (1, 2, N'Самый поздний комментарий');


-- IF OBJECT_ID(N'dbo.RequestAttachments', N'U') IS NULL
-- BEGIN
--     CREATE TABLE RequestAttachments (
--         Id           INT IDENTITY CONSTRAINT PK_RequestAttachments PRIMARY KEY,
--         RequestId    INT NOT NULL CONSTRAINT FK_RequestAttachments_Requests REFERENCES Requests(Id) ON DELETE CASCADE,
--         OriginalName NVARCHAR(255) NOT NULL,
--         StoredName   NVARCHAR(100) NOT NULL CONSTRAINT UQ_RequestAttachments_StoredName UNIQUE,
--         ContentType  NVARCHAR(100) NOT NULL,
--         SizeBytes    BIGINT NOT NULL CONSTRAINT CK_RequestAttachments_Size
--                      CHECK (SizeBytes > 0 AND SizeBytes <= 5242880),
--         UploadedAt   DATETIME2 NOT NULL CONSTRAINT DF_RequestAttachments_UploadedAt DEFAULT SYSDATETIME()
--     );
--     CREATE INDEX IX_RequestAttachments_RequestId ON RequestAttachments(RequestId);
-- END
-- GO

-- SELECT * FROM RequestAttachments ORDER BY Id DESC;
-- ALTER TABLE RequestAttachments ADD CONSTRAINT CK_tmp CHECK (1 = 0);
-- ALTER TABLE RequestAttachments DROP CONSTRAINT CK_tmp;
