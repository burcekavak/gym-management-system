# Gym Management System – CENG 301

A web-based Gym Management System developed using ASP.NET Core and SQL Server as part of the CENG 301 – Database Systems course.

##  Project Overview

This system is designed to manage:

- Members and membership plans
- Trainers and group classes
- Equipment and maintenance tracking
- Admin login and reporting
- Analytical queries using stored procedures

The project includes ER modeling, relational mapping, SQL implementation, and a web-based backend.

---

##  My Contributions

Although this was a 4-person team project, I took major technical responsibility in the following areas:

###  Trainer & Class Management Module
- Designed the ER sub-model for trainers and classes
- Defined relationships between members, trainers, and class registrations
- Implemented SQL tables and constraints for this module

###  Final ER Integration
- Merged all team sub-modules into a unified ER diagram
- Identified and fixed relationship inconsistencies

###  Full Relational Mapping
- Converted the ER model into a complete relational schema
- Defined primary keys, foreign keys, and constraints
- Ensured referential integrity across modules

###  Documentation
- Prepared the complete technical report
- Documented ER design decisions and relational mapping process

---

##  Technologies Used

- C# (ASP.NET Core)
- SQL Server
- Stored Procedures
- ER Modeling
- Relational Mapping

---

##  How to Run

1. Open `GymManagementSystem.sln` in Visual Studio (Windows environment required).
2. Execute SQL scripts located in `/sql`.
3. Update the connection string in `appsettings.json` if necessary.
4. Run the application.

---

##  Academic Context

This project was developed for a Database Systems course and focuses heavily on database design principles, normalization, and relationship modeling.
