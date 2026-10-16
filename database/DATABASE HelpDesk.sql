-- CREATE DATABASE HelpDesk
-- GO

USE HelpDesk
GO

-- CREATE TABLE Departments (
--     Id    INT IDENTITY PRIMARY KEY,
--     Name  NVARCHAR(100) NOT NULL UNIQUE
-- );

-- CREATE TABLE Employees (
--     Id           INT IDENTITY PRIMARY KEY,
--     FullName     NVARCHAR(100) NOT NULL,
--     Position     NVARCHAR(50)  NOT NULL,
--     Email        NVARCHAR(100) NOT NULL UNIQUE,
--     DepartmentId INT NOT NULL REFERENCES Departments(Id)
-- );

-- CREATE TABLE Clients (
--     Id        INT IDENTITY PRIMARY KEY,
--     FullName  NVARCHAR(100) NOT NULL,
--     Email     NVARCHAR(100) NOT NULL UNIQUE,
--     Phone     NVARCHAR(20)  NULL
-- );

-- CREATE TABLE Categories (
--     Id    INT IDENTITY PRIMARY KEY,
--     Name  NVARCHAR(100) NOT NULL UNIQUE
-- );

-- CREATE TABLE Statuses (
--     Id    INT IDENTITY PRIMARY KEY,
--     Name  NVARCHAR(30) NOT NULL UNIQUE
-- );

-- CREATE TABLE Priorities (
--     Id     INT IDENTITY PRIMARY KEY,
--     Name   NVARCHAR(30) NOT NULL UNIQUE,
--     Level  INT NOT NULL CHECK (Level BETWEEN 1 AND 3)
-- );

-- CREATE TABLE Requests (
--     Id          INT IDENTITY PRIMARY KEY,
--     Title       NVARCHAR(200) NOT NULL,
--     Description NVARCHAR(MAX) NULL,
--     CreatedAt   DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
--     ClosedAt    DATETIME2 NULL,
--     ClientId    INT NOT NULL REFERENCES Clients(Id),
--     EmployeeId  INT NULL     REFERENCES Employees(Id),
--     CategoryId  INT NOT NULL REFERENCES Categories(Id),
--     StatusId    INT NOT NULL REFERENCES Statuses(Id),
--     PriorityId  INT NOT NULL REFERENCES Priorities(Id),
--     CONSTRAINT CK_Requests_Dates CHECK (ClosedAt IS NULL OR ClosedAt >= CreatedAt)
-- );

-- CREATE TABLE RequestComments (
--     Id         INT IDENTITY PRIMARY KEY,
--     RequestId  INT NOT NULL REFERENCES Requests(Id) ON DELETE CASCADE,
--     EmployeeId INT NOT NULL REFERENCES Employees(Id),
--     Text       NVARCHAR(1000) NOT NULL,
--     CreatedAt  DATETIME2 NOT NULL DEFAULT SYSDATETIME()
-- );
-- GO

INSERT INTO Departments (Name) VALUES
    (N'Техподдержка'),
    (N'Сетевой отдел');

INSERT INTO Employees (FullName, Position, Email, DepartmentId) VALUES
    (N'Олег Сидоров', N'Инженер', 'oleg@company.ru', 1),
    (N'Мария Ким', N'Старший инженер', 'maria@company.ru', 2);

INSERT INTO Clients (FullName, Email, Phone) VALUES
    (N'Иван Петров', 'ivan@mail.ru', '+79001112233'),
    (N'Анна Смирнова', 'anna@mail.ru', NULL);

INSERT INTO Categories (Name) VALUES
    (N'Оборудование'),
    (N'Доступы'),
    (N'Сеть');

INSERT INTO Statuses (Name) VALUES
    (N'Новая'),
    (N'В работе'),
    (N'Закрыта');

INSERT INTO Priorities (Name, Level) VALUES
    (N'Низкий', 1),
    (N'Средний', 2),
    (N'Высокий', 3);

INSERT INTO Requests (Title, Description, ClientId, EmployeeId, CategoryId, StatusId, PriorityId) VALUES
    (N'Не работает принтер', N'Не печатает с утра', 1, 1, 1, 2, 2),
    (N'Нет доступа к почте', NULL, 2, NULL, 2, 1, 3),
    (N'Пропадает Wi-Fi', N'В переговорной', 1, 2, 3, 2, 1);

INSERT INTO RequestComments (RequestId, EmployeeId, Text) VALUES
    (1, 1, N'Заменил картридж, проверяю печать'),
    (3, 2, N'Перезагрузил точку доступа');