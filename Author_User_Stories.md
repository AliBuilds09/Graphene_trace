Software Development Documentation — Individual (Author)

Scope: Login page (front‑end & back‑end) and Patient Dashboard (front‑end & back‑end)

Formatting note: Use Times New Roman 12pt, double‑spaced when exporting to Word/PDF. Start each chapter on a new page.

1.1. User story 1 — Login (Front‑end & Back‑end)

Story
- As a user (Patient/Clinician/Admin), I log in with my username, password, and selected role to access my dashboard.

Acceptance Criteria
- Correct username/password and selected role navigates to the dashboard window.
- Wrong password or unknown username shows an error and stays on login.
- Clinician/Admin with unapproved account sees a pending approval banner; login is denied.
- If `MustChangePassword` is true, prompt for password change before dashboard access.
- On success, the session is set (`Session.CurrentUser`), and the window title reflects user and role.

Implementation Summary
- UI: `MainWindow.xaml` contains `UsernameTextBox`, `PasswordBox`, `RoleComboBox`, `LoginButton`, `RegisterButton`, and `PendingBanner`.
- Event: `LoginButton.Click` in `MainWindow.xaml.cs` reads inputs, validates non‑empty fields, and calls `AuthController.Authenticate(username, password, role, out user)`.
- Auth: `AuthController.Authenticate` delegates to `UsersRepository.VerifyCredentials` (role must match; password hashed via SHA256; user must be active; clinician/admin must be approved). On success sets `Session.CurrentUser`.
- Flow: If `user.MustChangePassword`, open `ChangePasswordWindow` and reload user via `AuthController.GetUserById`. Then open `DashboardWindow(user.Role, user.Username)` and close the login window.
- Failure: If user exists but is not approved (Clinician/Admin), show `PendingBanner`; otherwise show a generic invalid credentials message.

Code References
- `myproject/Views/MainWindow.xaml`
- `myproject/Views/MainWindow.xaml.cs` — `LoginButton.Click` handler; navigate to `DashboardWindow`
- `myproject/Controllers/AuthController.cs` — `Authenticate`, `GetUserById`, `GetUserByUsername`
- `myproject/Data/UsersRepository.cs` — `VerifyCredentials`; Admin approval enforcement; password hashing
- `myproject/Models/Session.cs` — `CurrentUser`, `IsAdmin/IsClinician/IsPatient`

1.2. User story 2 — Patient Dashboard (Front‑end)

Story
- As a patient, I view my session data in heatmap and trends, read comments, and log out from the top bar.

Acceptance Criteria
- Heatmap grid displays a 32×32 matrix with sensor values.
- Trend chart shows metrics over time and responds to time‑filter changes.
- Comments list shows author, role, timestamp, text, and optional clinician reply.
- Comment input accepts non‑empty text and appends to the list for the current user.
- Clinician reply controls are hidden for patients and visible for clinicians.
- Logout button returns to the login page.

Implementation Summary
- UI: `DashboardWindow.xaml` comprises left heatmap panel, right scroll area with Metrics, Trend, Alerts, and Comments, plus a top bar with user context and buttons (including Logout).
- Binding: `CommentsList.ItemsSource = CommentsController.Comments`; `AlertsList.ItemsSource = AlertsController.Alerts`.
- Role‑based UI: In `DashboardWindow.xaml.cs` `OnLoaded`, hide clinician import/report buttons for patients; show clinician reply panel only when `Session.IsClinician`; show admin pending panel only for `Session.IsAdmin`.
- Interactions: Buttons wire up to recompute metrics, open reports, submit comments, and reply (reply restricted to Clinician/Admin).

Code References
- `myproject/Views/DashboardWindow.xaml` — Heatmap (`UniformGrid`), `TrendCanvas`, `CommentsList`, `CommentInput`, `SubmitCommentButton`, `LogoutButton`
- `myproject/Views/DashboardWindow.xaml.cs` — `OnLoaded`, role‑based visibility, metrics/trend drawing, comment submission wiring
- `myproject/Controllers/CommentsController.cs` — comments collection and API
- `myproject/Controllers/AlertsController.cs` — alerts collection and API

1.3. User story 3 — Patient Dashboard (Back‑end)

Story
- As a patient, I can load my session data (read‑only), see computed metrics, and add comments; clinicians can import CSVs and reply to patient comments; admins manage approvals.

Acceptance Criteria
- Patients can view playback/heatmaps/trends; import controls are hidden.
- Clinicians can import CSVs (`LoadFramesFromFiles/Folder/All`) and generate reports; patients cannot trigger imports.
- Admins cannot view clinical data through import APIs (guarded); can view pending registrations and approve/reject users.
- Adding a comment requires non‑empty text; a reply can only be added by Clinician/Admin; unauthorized reply throws an error.
- Alerts are generated when metrics exceed the configured threshold.

Implementation Summary
- Import: `CsvImportController.EnsureCanViewClinicalData` denies Admin access; allows Patient/Clinician. `LoadFramesFromFiles`, `LoadFramesFromFolder`, `LoadAllFrames` parse CSV into `SensorData` frames, normalize values to 1..255, and produce 32×32 matrices.
- Metrics/Alerts: Dashboard computes peak pressure and contact area percent; `AlertsController.AddAlert` adds alert entries when thresholds are exceeded.
- Comments: `CommentsController.AddComment(author, role, text)` appends to observable collection; `AddReply(comment, reply)` throws if not Clinician/Admin.
- Role Enforcement: `Session.IsAdmin/IsClinician/IsPatient` drives UI visibility and permissible actions; `UsersRepository.VerifyCredentials` enforces approval for elevated roles.

Code References
- `myproject/Controllers/CsvImportController.cs` — import APIs and parsing
- `myproject/Controllers/CommentsController.cs` — add comment and restricted reply
- `myproject/Controllers/AlertsController.cs` — alert generation
- `myproject/Data/UsersRepository.cs` — approval checks and credential verification
- `myproject/Models/Session.cs` — role helpers used across UI and controllers

Readability & Source Referencing
- Code uses consistent indentation and minimal whitespace; obsolete commented‑out blocks removed.
- Variables are declared and scoped locally; brief comments explain complex operations (e.g., metrics computation, CSV parsing).
- No external code copied; if any snippet is later incorporated from third‑party sources, it will be cited in References.

Repository Evidence (to attach in final submission)
- Include GitHub/Git screenshots showing check‑ins authored by the SID holder for login and patient dashboard work.