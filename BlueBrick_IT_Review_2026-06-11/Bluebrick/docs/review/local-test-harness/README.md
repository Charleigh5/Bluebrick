# Local Test Harness Quarantine

This folder contains only the source files from the untracked `IntegrationTest/` harness:

- `IntegrationTest.csproj`
- `Program.cs`

It is intentionally quarantined from the main source tree in the review package because it reads live registry and environment state. One scenario temporarily clears and restores the `AssistantApiKey` registry value and API key environment variables while testing assistant mode resolution.

Do not treat this harness as production source until it is reviewed, moved into the normal test project structure, and adjusted to avoid live local machine side effects by default.
