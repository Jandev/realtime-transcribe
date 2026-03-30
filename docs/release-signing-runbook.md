# Release Signing Runbook

This document describes the prerequisites, required secrets, rotation guidance, local verification commands, and troubleshooting steps for producing signed, notarized macOS release artifacts for **Realtime Transcribe**.

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Required GitHub Secrets](#required-github-secrets)
4. [Setting Up Secrets](#setting-up-secrets)
5. [Rotation Guidance](#rotation-guidance)
6. [Local Verification Commands](#local-verification-commands)
7. [Manual Smoke-Test Checklist](#manual-smoke-test-checklist)
8. [Notarization Troubleshooting](#notarization-troubleshooting)

---

## Overview

macOS Gatekeeper requires that all software distributed outside the Mac App Store must be:

1. **Code-signed** with a valid Developer ID certificate issued by Apple.
2. **Notarized** by Apple's Notary Service.
3. **Stapled** so that the notarization ticket travels with the artifact (no internet connection required for Gatekeeper assessment on end-user machines).

The release workflow (`.github/workflows/release.yml`) automates all three steps using:

- `scripts/sign-and-notarize.sh` — imports the certificate into an ephemeral keychain, deep-signs the `.app` bundle, builds a `.pkg` installer, signs it, submits to Notary Service, and staples the ticket.
- `scripts/verify-release.sh` — runs `codesign --verify`, `spctl --assess`, and `stapler validate` to confirm the artifact passes Gatekeeper before it is uploaded to GitHub Releases.

---

## Prerequisites

You must be enrolled in the **Apple Developer Program** (individual or organization) to obtain the certificates required for distribution outside the Mac App Store.

### Certificates needed

| Certificate type | Used for | Where to obtain |
|---|---|---|
| **Developer ID Application** | Signing the `.app` bundle | Apple Developer portal → Certificates, Identifiers & Profiles |
| **Developer ID Installer** *(optional but recommended)* | Signing the `.pkg` installer | Apple Developer portal → Certificates, Identifiers & Profiles |

Export **both** certificates (and their private keys) from Keychain Access into a single `.p12` file:

1. Open **Keychain Access** → **My Certificates**.
2. Select both Developer ID certificates (expand to include the private key).
3. Right-click → **Export Items** → choose `.p12` format.
4. Set a strong passphrase — you will need this for the `MACOS_CERTIFICATE_PWD` secret.

### Apple ID and app-specific password

Notarization requires an Apple ID and an **app-specific password**:

1. Sign in at [appleid.apple.com](https://appleid.apple.com).
2. Under **Sign-In and Security**, choose **App-Specific Passwords**.
3. Generate a password labelled something like `CI notarization`.
4. Copy the password immediately — it is shown only once.

### Team ID

Find your 10-character Team ID in the [Apple Developer portal](https://developer.apple.com/account) under **Membership Details**.

---

## Required GitHub Secrets

Configure the following secrets in **Settings → Secrets and variables → Actions** for the repository:

| Secret name | Value | Notes |
|---|---|---|
| `MACOS_CERTIFICATE` | Base64-encoded `.p12` certificate bundle | See command below to encode |
| `MACOS_CERTIFICATE_PWD` | Passphrase for the `.p12` file | Set when exporting from Keychain Access |
| `MACOS_KEYCHAIN_PASSWORD` | A random strong password | Used only for the ephemeral build keychain; generate with `openssl rand -base64 32` |
| `APPLE_ID` | Your Apple ID email address | Must match the certificate owner |
| `APPLE_APP_SPECIFIC_PASSWORD` | App-specific password from appleid.apple.com | Format: `xxxx-xxxx-xxxx-xxxx` |
| `APPLE_TEAM_ID` | 10-character Team ID | Found in Apple Developer portal Membership |

To base64-encode the `.p12` file for the `MACOS_CERTIFICATE` secret:

```bash
base64 -i DeveloperIDCertificates.p12 | pbcopy
# Paste the clipboard value into the MACOS_CERTIFICATE secret.
```

---

## Setting Up Secrets

1. Go to `https://github.com/Jandev/realtime-transcribe/settings/secrets/actions`.
2. Click **New repository secret**.
3. Enter the secret **Name** and **Value** from the table above.
4. Click **Add secret**.
5. Repeat for all six secrets.

To verify the secrets are picked up, push a commit to `main` and watch the **Release** workflow. The **Validate signing secrets are configured** step will print which secrets are missing (without revealing their values).

---

## Rotation Guidance

### Certificate expiry

Developer ID certificates are valid for **5 years**. Set a calendar reminder ~30 days before expiry.

To rotate:

1. Create a new Developer ID Application (and Installer) certificate in the Apple Developer portal.
2. Export the new certificates as a `.p12` bundle.
3. Update `MACOS_CERTIFICATE` and `MACOS_CERTIFICATE_PWD` in GitHub Secrets.
4. Revoke the old certificate in the Apple Developer portal.

### App-specific password

App-specific passwords do not expire automatically but should be rotated if compromised:

1. Revoke the old password at [appleid.apple.com](https://appleid.apple.com) under App-Specific Passwords.
2. Generate a new password.
3. Update `APPLE_APP_SPECIFIC_PASSWORD` in GitHub Secrets.

### Keychain password

`MACOS_KEYCHAIN_PASSWORD` is ephemeral — the keychain is deleted at the end of each CI run. It can be rotated at any time without any coordination:

```bash
openssl rand -base64 32
# Update MACOS_KEYCHAIN_PASSWORD in GitHub Secrets with the new value.
```

---

## Local Verification Commands

Use these commands on a **clean macOS machine** (one where you have never installed or launched the app) to replicate what Gatekeeper does.

### Verify code signature

```bash
# For a .pkg file:
codesign --verify --deep --strict --verbose=2 RealtimeTranscribe-v1.0.42.pkg

# For a .app bundle:
codesign --verify --deep --strict --verbose=2 /Applications/Realtime\ Transcribe.app
```

Expected output: no errors and `valid on disk`.

### Inspect signing details

```bash
codesign --display --verbose=4 RealtimeTranscribe-v1.0.42.pkg
```

Key fields to check:
- `Authority=Developer ID Installer: <Your Name> (<TeamID>)` (or Application)
- `Authority=Developer ID Certification Authority`
- `Authority=Apple Root CA`
- `Timestamp=` (must be present for Gatekeeper acceptance)

### Gatekeeper assessment

```bash
# For a .pkg installer:
spctl --assess --type install --verbose=2 RealtimeTranscribe-v1.0.42.pkg

# For a .app bundle:
spctl --assess --type execute --verbose=2 /Applications/Realtime\ Transcribe.app
```

Expected output: `accepted` with source `Developer ID`.

### Verify notarization staple

```bash
xcrun stapler validate RealtimeTranscribe-v1.0.42.pkg
```

Expected output: `The validate action worked!`

### Run the automated verification script locally

```bash
chmod +x scripts/verify-release.sh
./scripts/verify-release.sh RealtimeTranscribe-v1.0.42.pkg
```

---

## Manual Smoke-Test Checklist

Perform this checklist for the **first signed release** and after any certificate rotation on a clean macOS machine (a machine that has never run the app before, or a VM snapshot):

- [ ] Download the `.pkg` from the GitHub Release page.
- [ ] **Without** right-clicking (i.e., using Finder double-click), open the `.pkg` — macOS should NOT show a "cannot be opened" or malware warning.
- [ ] Run: `spctl --assess --type install --verbose=2 <downloaded>.pkg` — output must say `accepted`.
- [ ] Install the package and launch the app — macOS should NOT show a Gatekeeper quarantine dialog.
- [ ] Verify the app appears in **System Settings → Privacy & Security → Microphone** after first launch.
- [ ] Run: `spctl --assess --type execute --verbose=2 /Applications/Realtime\ Transcribe.app` — output must say `accepted`.
- [ ] Test a short recording and transcription to confirm the app is fully functional.

---

## Notarization Troubleshooting

### "Package Approved" but app still blocked

Ensure the notarization ticket is **stapled**. The `scripts/sign-and-notarize.sh` script staples automatically, but you can verify and re-staple manually:

```bash
xcrun stapler staple RealtimeTranscribe-v1.0.42.pkg
xcrun stapler validate RealtimeTranscribe-v1.0.42.pkg
```

### Notarization rejected: "The signature of the binary is invalid"

The `.app` bundle was not deep-signed or was modified after signing. Ensure:
- `codesign --deep --force --options runtime` is used.
- No files are added to the bundle after signing.

### Notarization rejected: "The executable does not have the hardened runtime enabled"

The `--options runtime` flag must be passed to `codesign`. This is included in `scripts/sign-and-notarize.sh`.

### `notarytool` returns `invalid` status

Retrieve the full log from Apple:

```bash
xcrun notarytool log <submission-id> \
    --apple-id "$APPLE_ID" \
    --password "$APPLE_APP_SPECIFIC_PASSWORD" \
    --team-id "$APPLE_TEAM_ID"
```

The submission ID is printed in the CI log during the **Sign, notarize, and staple release package** step.

### App-specific password rejected

Ensure the password was generated correctly at [appleid.apple.com](https://appleid.apple.com) and that two-factor authentication is enabled on the Apple ID. App-specific passwords require 2FA.

### `security: SecKeychainItemImport: The specified item already exists in the keychain`

This occurs if an old certificate is already installed on the runner. The `sign-and-notarize.sh` script creates a new ephemeral keychain for each run to avoid this.

### Certificate not trusted by Gatekeeper: "CSSMERR_TP_NOT_TRUSTED"

Ensure the certificate was issued directly from the Apple Developer portal (not a self-signed or intermediate cert). The certificate chain must include:
1. Your Developer ID certificate
2. Developer ID Certification Authority (intermediate)
3. Apple Root CA (root)

Download and install the **Developer ID Certification Authority** intermediate certificate from the [Apple PKI page](https://www.apple.com/certificateauthority/) if it is missing.
