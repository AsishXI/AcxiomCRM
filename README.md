# AcxiomCRM

Role-based CRM built with ASP.NET Core MVC, Entity Framework Core (SQLite), ASP.NET Core Identity, Bootstrap and Chart.js.

## Run
1. Install the .NET SDK
2. `dotnet run`
3. Open the http://localhost:xxxx URL shown in the terminal

The database (`app.db`) and the roles and demo users are created automatically on first run.

## Demo logins (password for all: `Test@1234`)
| Role | Email |
|---|---|
| Admin | admin@acxiom.com |
| Manager | manager@acxiom.com |
| SalesExecutive | sales@acxiom.com |

## Features
- Authentication: Register, Login, Logout (ASP.NET Core Identity), password policy, account lockout (5 attempts, 5 minutes)
- Roles: Admin, Manager, SalesExecutive (server-side authorization; sales users see only their own or assigned records)
- Modules: Customers, Leads (status workflow and conversion), Opportunities, Follow-Ups, Audit Log
- Validation: client-side (unobtrusive) and server-side, plus business rules (amount > 0, probability 0-100, close date not in the past, follow-up date not before today, unique email and phone)
- Dashboard: KPI cards and 3 Chart.js charts, scoped by role
- Search and filters on all list pages
- REST API: `/api/customers`, `/api/leads`, `/api/opportunities` (DTOs, validation, authorization, status codes 200/201/400/401/403/404/409)
- Audit log: login, failed login, logout, create, update, delete, convert (with old/new values and IP)
- Security: anti-forgery tokens on all POST forms, EF Core parameterized queries, hashed passwords

## Structure
Controllers, Controllers/Api, Models, Data, Services, Views