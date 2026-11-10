IF DB_ID('EnrollmentDB') IS NULL
    CREATE DATABASE EnrollmentDB;
GO
USE EnrollmentDB;
GO

CREATE TABLE Students (
    StudentId   INT IDENTITY(1,1) PRIMARY KEY,
    FullName    NVARCHAR(150) NOT NULL,
    Email       NVARCHAR(150) NOT NULL UNIQUE,
    CreatedAt   DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);

CREATE TABLE Subjects (
    SubjectId   INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(20)  NOT NULL UNIQUE,
    Name        NVARCHAR(120) NOT NULL,
    Units       INT           NOT NULL DEFAULT 3
);

CREATE TABLE Enrollments (
    EnrollmentId INT IDENTITY(1,1) PRIMARY KEY,
    StudentId    INT NOT NULL REFERENCES Students(StudentId),
    SubjectId    INT NOT NULL REFERENCES Subjects(SubjectId),
    ReceiptNo    NVARCHAR(40) NOT NULL,
    EnrolledAt   DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Student_Subject UNIQUE (StudentId, SubjectId)
);

INSERT INTO Subjects (Code, Name, Units) VALUES
 ('IT101',   'Introduction to Computing', 3),
 ('IT102',   'Programming Fundamentals',  3),
 ('MATH101', 'College Algebra',           3),
 ('ENG101',  'Communication Skills',      3),
 ('IT103',   'Database Management',       3),
 ('PE101',   'Physical Education 1',      2);
GO