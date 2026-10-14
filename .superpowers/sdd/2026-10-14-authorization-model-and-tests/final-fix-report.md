# Final fix report
1. AuthorizationSetup.cs: configured group parsed once with Guid.Parse in ConfigureRequireAdmin; IsMemberOf(ClaimsPrincipal, Guid) compares Guid.TryParse(claim) == id. Validation message unchanged. RED: both Ping_WithDecoratedConfiguredGroupId_Returns200 cases ("{..}", " .. ") failed before fix; GREEN after (21/21 repo tests).
2. AdminEndpointTests.cs: added theory (UseGroups), AppRoles-only 403 test (literal GUID), Groups-only 403 test (roles ["Admin"]); added `using Microsoft.AspNetCore.Hosting`.
3. README.md: qualifier sentences, `bash ./scripts/get-token.sh admin`, Linux TLS note.
4. Api.http header updated.
5. template-ci.yml: permissions contents: read.
6. Spec §4.5 Api.http bullet reworded (@AccessToken, bash get-token.sh).
Verification: dotnet test 21 passed; verify-template AppRoles true true true OK (16 tests); Groups true false true OK (20 tests); yaml ok.
