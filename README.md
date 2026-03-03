# Gym Management System – CENG 301

A web-based Gym Management System developed for the CENG 301 – Database Systems course.

The backend is implemented using C# (.NET Minimal API) and the database is Microsoft SQL Server.  
The UI is built with HTML/CSS.

---

## Project Overview

This system is designed to manage:

- Members and membership plans  
- Trainers and group classes  
- Equipment and maintenance tracking  
- Admin login and reporting  
- Analytical queries using stored procedures  

The project includes ER modeling, relational mapping, SQL implementation, and a web-based backend.

---

## My Contributions

Although this was a 4-person team project, I took major technical responsibility in the following areas:

### Trainer & Class Management Module
- Designed the ER sub-model for trainers and classes  
- Defined relationships between members, trainers, and class registrations  
- Implemented SQL tables and constraints for this module  

### Final ER Integration
- Merged all team sub-modules into a unified ER diagram  
- Identified and fixed relationship inconsistencies  

### Full Relational Mapping
- Converted the ER model into a complete relational schema  
- Defined primary keys, foreign keys, and constraints  
- Ensured referential integrity across modules  

### Documentation
- Prepared the complete technical report  
- Documented ER design decisions and relational mapping process  

---

## Technologies Used

- C# (.NET Minimal API)  
- Microsoft SQL Server  
- Stored Procedures  
- ER Modeling  
- Relational Mapping  
- HTML + CSS  
- ADO.NET (SqlConnection / SqlCommand)  

---

## How to Run

1. Open a terminal and navigate to the backend folder:

   cd backend  
   dotnet run  

2. Open the browser and go to:

   http://localhost:5161  

### Admin Login

Username: admin  
Password: admin123  

---

## Note

This project follows a Minimal API structure with all endpoints located in a single `Program.cs` file (not MVC), based on course scope and simplicity.

---

## Developers

Ece Bayyar  
Burçe Nur Kavak  
Selin Gül Bayrı  
İbrahim Said Akıncı