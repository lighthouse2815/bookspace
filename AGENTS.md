# AGENTS.md

BookSpace is an independent social reading product. It must remain fully usable
without Bookstore or any other commerce service.

## Repository layout

- `backend/` - ASP.NET Core Web API following Clean Architecture
- `frontend/` - React, TypeScript, Vite and Tailwind CSS
- `docs/` - product contract, domain model, API contract and acceptance tests
- `scripts/` - local development and verification helpers

## Architecture rules

- Domain code must not depend on EF Core, ASP.NET Core, HTTP or external vendors.
- Application code owns use cases and abstractions.
- Infrastructure implements persistence, tokens, time, password hashing and
  optional external providers.
- API endpoints remain thin and delegate business rules to Application/Domain.
- React pages do not call Axios directly. They use feature services and hooks.
- Use `Guid` identifiers and UTC timestamps.
- Use soft deletion for user-created content where restoration or moderation is
  meaningful.
- Do not share databases or signing secrets with Bookstore.
- Bookstore integration must be optional, timeout-bounded and failure-tolerant.
- All user-facing validation and error messages are written in Vietnamese.

## Verification

Backend:

```powershell
cd backend
dotnet build BookSpace.slnx
dotnet test BookSpace.slnx
```

Frontend:

```powershell
cd frontend
npm install
npm run lint
npm run build
```

Full repository:

```powershell
.\scripts\verify.ps1
```

Runtime API smoke:

```powershell
.\scripts\smoke-api.ps1
```
