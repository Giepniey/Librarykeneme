# Library Inventory System (C# WinForms)

Desktop recreation of the Library Inventory System screenshots. Pure C# / WinForms (.NET 8) with a MySQL database (MySqlConnector); all data is persisted in MySQL and a one-time migration moves the legacy JSON data into it.

## Run
Requires the .NET 8 SDK on **Windows**.

```
cd LibraryInventory
dotnet build
dotnet run
```

## Demo logins (any password works)
- `admin` -> Admin screens (Dashboard, Manage Books, Manage Students, Requests, History)
- any other name -> Student screens (Browse Books, Book Cart, History, Manage Account)

## What works
- Log in / Sign up (placeholder validation only), sidebar navigation, Log Out
- Browse by category (ALL/CIR/FIC/FIL/REF), search, add up to 3 books to the cart with the `+` button
- Book Cart: remove, select, submit borrow request; Manage tab shows loans, click RETURN to return a book
- Admin: approve/decline requests (dashboard and Requests page), add/edit/delete books and students, filters and search, Print Format (preview/print transaction slips)
- Manage Account: Edit Profile toggle; click the Account Status chip to switch between the Active and Suspended designs

## Files
`Program.cs`, `MainForm.cs` (login/sign up), `Shell.cs` (sidebar + page base), `StudentPages.cs`, `AdminPages.cs`, `Widgets.cs` (shared controls), `Theme.cs` (colors/fonts), `Models.cs` (sample data).
