# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| 2.x | Yes |
| 1.x | No |

Security fixes are released as new 2.x NuGet versions. Version 1.x is unsupported because its upload, cleanup, and concurrency model has been replaced.

## Reporting a vulnerability

Do not open a public issue for undisclosed vulnerabilities. Email `alihmaidi095@gmail.com` with:

- A description of the vulnerability and affected package version
- Reproduction steps or a minimal proof of concept
- Expected impact
- Any suggested mitigation

Reports will be acknowledged and investigated before public disclosure.

## Deployment guidance

- Keep the filesystem root outside any publicly served directory.
- Grant the application identity only the database and filesystem permissions it requires.
- Authenticate callers and authorize every operation against the upload ID.
- Apply proxy, server, rate, and request-body limits independently of package validation.
- Treat filenames as untrusted metadata and never use them to construct storage paths.
- Scan completed content when required by the application's threat model.
- Do not place credentials in storage keys, logs, exception messages, or package configuration.
