# Security Policy

## Supported Versions

Security updates for Koy are provided on the latest stable release:

| Version | Status |
| --- | --- |
| Latest release | Supported |

## Security Architecture

Koy follows these principles to protect user security and privacy on the Windows 11 desktop:

1. **No Telemetry, No Cloud Account:** Koy never sends user data, screenshots or file information to external servers.
2. **Local Data Storage:** Settings (`%AppData%\Koy\settings.json`) and Shelf items (`%AppData%\Koy\shelf.json`) are kept only on the user's local disk.
3. **Minimal Network Requests:** Network access is used for only two purposes:
   - Fetching keyless weather data from the **Open-Meteo** API (`api.open-meteo.com`) when the weather panel is opened (no device location permission is requested; only the city name in settings is used).
   - Checking for releases through **Velopack** on the official GitHub Releases (`github.com/koyhq/koy`).
4. **SHA256 Integrity Verification:** Every GitHub release is published with a `SHA256SUMS.txt` file to verify the integrity of the installer and portable packages.

## Reporting a Vulnerability

If you find a security vulnerability in Koy (e.g. privilege escalation, unsafe file handling, a P/Invoke memory bug):

1. **Please do not share it as a public GitHub issue.**
2. Instead, create a private report using **[Security Advisories → Report a vulnerability](https://github.com/koyhq/koy/security/advisories/new)** in the GitHub repository.
3. Include how the bug is triggered, the affected component (`Core`, `Platform`, `App`) and a proposed fix if you have one.

Your report will be reviewed within **72 hours**, and a patch release will be published for confirmed vulnerabilities.
