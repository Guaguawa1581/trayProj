/*
    MainApi 測試資料：建立資料庫 AppDb、API 用的登入帳號 app、資料表 dbo.Products 與範例資料。

    以系統管理員身分（sa，或本機管理員的 Windows 驗證）在 SSMS 開啟執行，或用 sqlcmd：
        sqlcmd -S localhost -E -C -i src\Servers\MainApi\Database\CreateTestData.sql

    可以重複執行：資料庫與帳號已存在就沿用；dbo.Products 每次都會刪除重建，資料回到初始狀態。

    執行完在管理介面「資料庫」頁填入：伺服器 localhost（具名執行個體如 localhost\SQLEXPRESS）、
    資料庫 AppDb、帳號 app、密碼為下方 CREATE LOGIN 的密碼。SQL Server 要啟用「SQL Server 及 Windows 驗證模式」。
*/
USE master;
GO

IF DB_ID(N'AppDb') IS NULL
    CREATE DATABASE AppDb;
GO

-- BackendTray 以 SQL Server 驗證連線。只供測試，正式環境請改用自己的密碼。
IF SUSER_ID(N'app') IS NULL
    CREATE LOGIN app WITH PASSWORD = N'App_Test#2026', DEFAULT_DATABASE = AppDb;
GO

USE AppDb;
GO

IF USER_ID(N'app') IS NULL
    CREATE USER app FOR LOGIN app;

-- API 只需要讀寫資料，不給建立或修改資料表的權限。
ALTER ROLE db_datareader ADD MEMBER app;
ALTER ROLE db_datawriter ADD MEMBER app;
GO

DROP TABLE IF EXISTS dbo.Products;
GO

CREATE TABLE dbo.Products
(
    Id        INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Name      NVARCHAR(100)      NOT NULL,
    Category  NVARCHAR(50)       NOT NULL,
    Price     DECIMAL(10, 2)     NOT NULL CONSTRAINT CK_Products_Price CHECK (Price >= 0),
    Stock     INT                NOT NULL CONSTRAINT DF_Products_Stock DEFAULT (0) CONSTRAINT CK_Products_Stock CHECK (Stock >= 0),
    IsActive  BIT                NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0)       NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0)       NULL
);

CREATE INDEX IX_Products_Category ON dbo.Products (Category);
GO

INSERT INTO dbo.Products (Name, Category, Price, Stock, IsActive) VALUES
    (N'無糖綠茶 600ml',           N'飲料',     25,  120, 1),
    (N'全脂鮮乳 936ml',           N'飲料',     95,   40, 1),
    (N'美式黑咖啡 罐裝',          N'飲料',     35,   80, 1),
    (N'洋芋片 經典原味',          N'零食',     39,   60, 1),
    (N'海苔脆片',                 N'零食',     30,    0, 1),
    (N'花生牛軋糖 200g',          N'零食',    120,   25, 1),
    (N'中性原子筆 0.5mm 藍',      N'文具',     15,  300, 1),
    (N'A5 方格筆記本',            N'文具',     45,   90, 1),
    (N'便利貼 76x76mm',           N'文具',     35,  150, 1),
    (N'抽取式衛生紙 100抽x12包',  N'生活用品', 189,  30, 1),
    (N'洗碗精 1000g',             N'生活用品',  79,   45, 1),
    (N'環保購物袋',               N'生活用品',  49,   10, 0);
GO

SELECT Id, Name, Category, Price, Stock, IsActive, CreatedAt FROM dbo.Products ORDER BY Id;
GO
