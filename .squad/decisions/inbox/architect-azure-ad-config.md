# Azure AD / Entra ID Configuration — Existing Setup Audit

**Date:** 2025-07-25  
**Author:** Architect  
**Status:** Reference (no code changes)

## Summary

The TodoExtended app already has a complete Microsoft Identity Platform integration. This document captures the existing configuration and provides step-by-step instructions for setting up the Azure Portal side (app registration) and filling in the local secrets.

## Existing Architecture

- **Auth library:** `Microsoft.Identity.Web` v4.5.0 (OIDC + Graph + UI packages)
- **Auth schemes:** OpenID Connect (primary, Blazor UI) + custom API key scheme (REST API)
- **Token caching:** SQLite-backed `IDistributedCache` via custom `SqliteDistributedCache`
- **Graph scopes:** `Tasks.ReadWrite`, `User.Read`
- **Tenant:** `consumers` (personal Microsoft accounts only)
- **Page protection:** `@attribute [Authorize]` on protected pages, `<AuthorizeView>` for conditional UI

## What Needs Azure Portal Setup

1. App registration in Microsoft Entra ID
2. Redirect URI: `https://localhost:{port}/signin-oidc`
3. Client secret generation
4. API permissions: `Tasks.ReadWrite`, `User.Read` (delegated, Microsoft Graph)
5. Copy Client ID and Client Secret into `appsettings.local.json`

## Decision

No code changes needed. The integration is complete and follows Microsoft.Identity.Web best practices. Only the Azure Portal app registration and local secrets file need to be configured per-environment.
