# Hockney — Runner / DevOps

## Project Context

- **Project:** TodoExtended — Alternative view for Microsoft To Do with extended features
- **Stack:** .NET 10, Blazor Web App, Interactive Server, Microsoft Identity + Graph API
- **User:** Marek Fišera
- **App path:** src/TodoExtended.Web
- **Default URL:** http://localhost:5016

## Learnings

- App starts successfully with `dotnet watch` and is responsive on http://localhost:5016 (redirects to https://localhost:7065)
- HTTPS endpoint responds with HTTP/2 on port 7065
- dotnet watch process detaches cleanly and runs in background without issues
- Compilation and hot-reload are operational

