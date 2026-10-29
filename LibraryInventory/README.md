# Library Inventory System (C# WinForms)

Desktop recreation of the Library Inventory System screenshots. Pure C# / WinForms (.NET 8) with a MySQL/MariaDB database (MySqlConnector).

## Run
Requires the .NET 8 SDK on **Windows** and a running MySQL/MariaDB server.

```bash
# 1. Install the database (safe to run more than once), from the repository root:
mysql -u root < database/00_install.sql

# 2. Build and run:
cd LibraryInventory
dotnet build
dotnet run
```

The installer creates the `LIS` database (utf8mb4) and loads the scripts in `database/` in dependency order: books, students, borrowing, admins, book cart, then the views.

## Database connection
Defaults to `root@127.0.0.1:3306/LIS` with an empty password. Override without editing code:

| Variable | Default |
|---|---|
| `LIS_DB_SERVER` | `127.0.0.1` |
| `LIS_DB_PORT` | `3306` |
| `LIS_DB_NAME` | `LIS` |
| `LIS_DB_USER` | `root` |
| `LIS_DB_PASSWORD` | *(empty)* |

If the database is missing or unreachable, the login screen shows the real error (including the underlying MySQL message) and tells you to run `database/00_install.sql`.

## Accounts
- Admin: `admin` / `admin123` (created on first start when `admins` is empty).
- Students: create an account from **Sign up** with a student ID such as `20251362-C` and a password. The same ID and password are used to log in.
- Passwords are stored as BCrypt hashes; legacy plain-text rows are hashed automatically on startup.

## What works
- Log in / Sign up, sidebar navigation, Log Out
- Browse by category (ALL/CIR/FIC/FIL/REF), search, add up to 3 books to the cart with the `+` button (persisted in `cart_items`)
- Book Cart: remove, select, submit borrow request; Manage tab shows loans, click RETURN to return a book
- Admin: approve/decline requests (dashboard and Requests page), add/edit/delete books and students, filters and search, Print Format (preview/print transaction slips)
- Manage Account: Edit Profile toggle; click the Account Status chip to switch between the Active and Suspended designs
- Deleting a book or student that has borrowing history is blocked with a friendly message instead of silently losing records

## Files
`Program.cs`, `MainForm.cs` (login/sign up), `Shell.cs` (sidebar + page base), `StudentPages.cs`, `AdminPages.cs`, `Widgets.cs` (shared controls), `Theme.cs` (colors/fonts), `Models.cs` (in-memory state and borrowing rules), `Database.cs` / `DB.cs` (MySQL access), `database/` (schema and views).
