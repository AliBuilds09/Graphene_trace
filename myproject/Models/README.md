# Models

Data classes for domain entities and DTOs.

## User

Fields:
- `UserId` (Guid) — primary key
- `Username` (string, unique)
- `PasswordHash` (string) — SHA256 hex; prefer over legacy `Password`
- `Role` (string) — allowed values only: `Admin`, `Clinician`, `Patient`
- `CreatedAt` (DateTime UTC)
- `IsActive` (bool)
- `CreatedByAdminId` (Guid?)
- `LastPasswordReset` (DateTime?)

Constraints:
- Role is validated at set-time. Invalid values throw and should be rejected.
- See `Data/UsersSchema.sql` for a SQLite table with `CHECK(role IN (...))` enforcement.

Notes:
- Use `SetPassword(string)` to set `PasswordHash` safely.
- `Password` (plaintext) exists for backward compatibility but will be phased out.