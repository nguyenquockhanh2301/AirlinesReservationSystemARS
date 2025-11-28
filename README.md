# Airlines Reservation System (ARS)

A .NET 9 (ASP.NET Core MVC + Identity + EF Core MySQL) application for searching flights, booking single or multi‑leg itineraries, managing seats, payments, refunds, and rescheduling. Recently extended with a lightweight JSON Web Token (JWT) layer for securing API-style endpoints while preserving cookie/Identity auth for Razor views.

## Features
- Flight search: one‑way, round‑trip, multi‑city with pagination.
- Dynamic pricing via `PricingPolicy` (extensible).
- Reservations: single flight or multi‑leg (`Reservation` + `ReservationLeg`).
- Seat management: persistent aircraft seat templates -> per‑schedule `FlightSeat` generation.
- Payments and refunds workflow.
- Rescheduling support with cost difference calculation.
- Admin tools: user booking lookup, cancellation flow.
- Background cleanup service for past schedules/flights.
- Identity (roles: Admin, Customer) + session state for booking context.
- JWT auth for API endpoints (currently `SeatController` + `/api/auth/login`).

## Tech Stack
- .NET 9 / ASP.NET Core MVC
- Entity Framework Core + Pomelo MySQL provider
- ASP.NET Core Identity (int PK) with roles
- Session + Distributed Memory Cache
- MySQL database (schema initialized via `CreateFreshDatabase.sql` or EF migrations already present)
- Background Hosted Service for cleanup
- JWT (HMAC SHA256) via `Microsoft.AspNetCore.Authentication.JwtBearer`

## Project Structure (key parts)
```
ARS/
  Program.cs                // Startup, Identity, JWT, session
  appsettings.json          // Connection string, Email, PayPal, JWT settings
  Data/                     // DbContext & migrations
  Models/                   // Domain entities
  ViewModels/               // MVC binding/view models
  Controllers/              // MVC + API controllers
  Services/                 // Seat, Email, JWT token service, cleanup
  wwwroot/                  // Static assets
CreateFreshDatabase.sql     // Optional database recreation script
```

## Quick Start (Development)
```powershell
# 1. Clone or open the workspace (already open).
# 2. Ensure MySQL running and create empty DB matching connection string.
#    Adjust connection in appsettings.json if needed.
# 3. (Optional) Create .env for secrets overriding appsettings values.
New-Item .env -ItemType File ; Add-Content .env "JWT_SECRET=REPLACE_ME_32_CHARS"
# 4. Build
dotnet build .\ARS\ARS.csproj
# 5. Run
dotnet run --project .\ARS\ARS.csproj
```
Navigate to: `https://localhost:PORT/` (check console output for the port).

### Admin User (Development)
On first run in Development an admin user is auto-created if absent, using env/config values:
- Email: `ADMIN_EMAIL` (fallback `admin@example.com`)
- Password: `ADMIN_PASSWORD` (fallback `Admin123!`)
Change these in `.env` or user secrets before first run.

## Database Initialization
Two approaches:
1. Use the existing migrations (already in `Migrations/`) – database must match expected schema.
2. Run `CreateFreshDatabase.sql` manually for a clean reset (contains schema + seed logic as per your workflow). After manual creation, you typically do not call `dotnet ef database update`.

## JWT Authentication (Option A Implementation)
- Config section in `appsettings.json`:
```json
"JWT": {
  "Issuer": "ARSIssuer",
  "Audience": "ARSAudience",
  "Secret": "CHANGE_ME_DEV_SECRET_32CHARS_MINIMUM_123456",
  "AccessTokenMinutes": 30
}
```
Replace `Secret` with a strong 32+ char value (or set `JWT__Secret` env var).

### Login Endpoint
```
POST /api/auth/login
Content-Type: application/json
{
  "email": "admin@example.com",
  "password": "Admin123!"
}
```
Response:
```json
{
  "accessToken": "<JWT>",
  "expiresInSeconds": 1800,
  "email": "admin@example.com"
}
```
### Secured Endpoint Example
```
GET /api/seat/map/{scheduleId}
Authorization: Bearer <JWT>
```
Other seat operations (`reserve`, `reserve/leg`, `cancel`, `cancel/leg`) also require the header.

### Adding JWT to New API Controllers
1. Add `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` to controller/class.
2. Return 401/403 automatically if missing/invalid token.
3. Access user claims via `User.Claims` (e.g. role checks).

## Environment Variables (.env Loader)
`Program.cs` includes a simple `.env` loader supporting `KEY=VALUE` and `export KEY=VALUE`. Place file at project root. Values override when building configuration via `Environment.GetEnvironmentVariable`.
Sensitive data (PayPal, Email, JWT secret) should move from `appsettings.json` into `.env` or secure secret store for production.

## Key Models
- `Flight`, `Schedule`, `FlightSeat`, `SeatLayout`, `Seat` for operational data.
- `Reservation` + `ReservationLeg` for multi-leg itineraries.
- `Payment`, `Refund` for financial flow.

## API Endpoints (Current JWT Scope)
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/auth/login` | POST | Obtain JWT access token |
| `/api/seat/map/{scheduleId}` | GET | Seat map + availability |
| `/api/seat/reserve` | POST | Reserve seat for reservation |
| `/api/seat/reserve/leg` | POST | Reserve seat for reservation leg |
| `/api/seat/cancel` | POST | Cancel seat for reservation |
| `/api/seat/cancel/leg` | POST | Cancel seat for reservation leg |

(Expand with more API endpoints as needed.)

## Development Tips
- Use short-lived tokens (≤30 mins) for simplicity; re-login to renew.
- Keep cookie-based flows (Razor forms) unchanged; do not mix JWT into views unnecessarily.
- Add authorization policies later for granular role control.
- Address nullable warnings incrementally; focus on real runtime null risks.

## Security Notes
- Replace placeholder JWT secret immediately.
- Consider HTTPS only; already enforced via `UseHttpsRedirection`.
- Future: add refresh tokens only when external/mobile clients require silent renewal.
- Seat endpoints were previously anonymous; now protected to prevent unauthorized seat locking.
- Avoid putting sensitive PII (address, phone) directly into JWT claims.

## Roadmap (Suggested)
- Role-based policies (e.g., Admin-only management API).
- Refresh token table for mobile clients.
- Centralized auditing/logging for reservation changes.
- Hardening email service (queue + retry).
- Reduce direct SQL execution warnings in `FlightCleanupService`.

## Troubleshooting
| Issue | Cause | Fix |
|-------|-------|-----|
| 401 on seat endpoints | Missing/expired JWT | Re-login, include `Authorization` header |
| Token rejected | Mismatched Issuer/Audience | Align config values client/server |
| Cannot login admin | Admin not seeded yet | Ensure Development environment & correct `.env` vars |
| Null reference warnings | Nullable reference types | Incrementally add null checks | 

## License
Internal / proprietary (no license header added). Add one if distributing externally.

---
Generated README with current JWT integration (Option A). Update as features evolve.
