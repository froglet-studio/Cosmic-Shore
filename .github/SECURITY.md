# Security policy

Cosmic Shore is developed in the open by Froglet Inc. This repository is public, so anything committed
here, including its full history, can be read by anyone.

## Reporting a vulnerability

Please report security problems **privately**, not in a public issue, pull request or discussion.

Use GitHub's private reporting: open the repository's **Security** tab and choose
**Report a vulnerability**
(https://github.com/froglet-studio/Cosmic-Shore/security/advisories/new). Only the maintainers can see
the report.

Include what you found, where (a file, a build, an endpoint), how to reproduce it and what an attacker
could do with it. We aim to acknowledge a report within 5 working days and will keep you updated while we
fix it. Please give us a reasonable time to ship a fix before you disclose it publicly.

## Supported versions

Only the latest released build of the game and the `bleeding-edge` branch receive security fixes.

## What must never be committed

Push protection blocks most of these automatically, but it does not catch everything:

- API keys, access tokens, passwords, service-account files or `.env` files
- Signing material: Android keystores, Apple certificates and provisioning profiles, Steam credentials
- Unity Gaming Services secrets (Cloud Code, Economy or Remote Config service-account keys)
- Player data of any kind

If a secret is committed by mistake, treat it as leaked: revoke or rotate it first, then remove it from
the code. Deleting the commit does not undo the leak, because the public history and forks keep it.
