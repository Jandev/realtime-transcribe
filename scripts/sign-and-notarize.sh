#!/usr/bin/env bash
# sign-and-notarize.sh — Code-sign, notarize, and staple the macOS release artifact.
#
# Usage:
#   ./scripts/sign-and-notarize.sh <publish-dir> <version> <output-pkg>
#
#   <publish-dir>  Directory produced by `dotnet publish` (contains *.app).
#   <version>      Marketing version string used in the pkg (e.g. "1.0.42").
#   <output-pkg>   Destination path for the signed/notarized .pkg file.
#
# Required environment variables (set via GitHub Actions secrets):
#   MACOS_CERTIFICATE            Base64-encoded Developer ID certificate bundle (.p12).
#   MACOS_CERTIFICATE_PWD        Password that protects the .p12 file.
#   MACOS_KEYCHAIN_PASSWORD      Password used for the ephemeral build keychain.
#   APPLE_ID                     Apple ID (email) used for notarization.
#   APPLE_APP_SPECIFIC_PASSWORD  App-specific password generated at appleid.apple.com.
#   APPLE_TEAM_ID                10-character Apple Developer Team ID.
#
# Optional environment variables:
#   APP_BUNDLE_ID                Application bundle identifier (default: com.jandev.realtimetranscribe).
#
# Exit codes:
#   0  Success — artifact is signed, notarized, and stapled.
#   1  Missing argument or environment variable.
#   2  Signing identity not found in keychain.
#   3  codesign failed.
#   4  pkgbuild failed.
#   5  pkgbuild signing failed.
#   6  Notarization submission failed.
#   7  Notarization did not succeed (rejected or timed out).
#   8  Stapling failed.

set -euo pipefail

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
log()  { echo "[sign-and-notarize] $*"; }
fail() { echo "[sign-and-notarize] ERROR: $*" >&2; exit "${2:-1}"; }

require_env() {
    local var="$1"
    if [[ -z "${!var:-}" ]]; then
        fail "Required environment variable '$var' is not set." 1
    fi
}

# ---------------------------------------------------------------------------
# Arguments
# ---------------------------------------------------------------------------
if [[ $# -lt 3 ]]; then
    fail "Usage: $0 <publish-dir> <version> <output-pkg>" 1
fi

PUBLISH_DIR="$1"
VERSION="$2"
OUTPUT_PKG="$3"
APP_BUNDLE_ID="${APP_BUNDLE_ID:-com.jandev.realtimetranscribe}"

# ---------------------------------------------------------------------------
# Validate required secrets
# ---------------------------------------------------------------------------
require_env MACOS_CERTIFICATE
require_env MACOS_CERTIFICATE_PWD
require_env MACOS_KEYCHAIN_PASSWORD
require_env APPLE_ID
require_env APPLE_APP_SPECIFIC_PASSWORD
require_env APPLE_TEAM_ID

# ---------------------------------------------------------------------------
# Locate .app bundle
# ---------------------------------------------------------------------------
log "Locating .app bundle in: $PUBLISH_DIR"
APP_BUNDLE=$(find "$PUBLISH_DIR" -maxdepth 2 -name "*.app" -type d | head -n 1)
if [[ -z "$APP_BUNDLE" ]]; then
    fail "No .app bundle found under '$PUBLISH_DIR'." 1
fi
APP_NAME=$(basename "$APP_BUNDLE")
log "Found app bundle: $APP_BUNDLE"

# ---------------------------------------------------------------------------
# Set up ephemeral keychain
# ---------------------------------------------------------------------------
KEYCHAIN_NAME="build-$(uuidgen).keychain-db"
KEYCHAIN_PATH="$HOME/Library/Keychains/$KEYCHAIN_NAME"
CERT_FILE="$(mktemp /tmp/certificate.XXXXXX.p12)"

cleanup() {
    log "Cleaning up keychain and temporary files..."
    security delete-keychain "$KEYCHAIN_PATH" 2>/dev/null || true
    rm -f "$CERT_FILE"
}
trap cleanup EXIT

log "Creating ephemeral keychain: $KEYCHAIN_NAME"
security create-keychain -p "$MACOS_KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
security unlock-keychain -p "$MACOS_KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"

# Prepend the new keychain to the search list so codesign can find the identity.
CURRENT_KEYCHAINS=$(security list-keychains -d user | tr -d '"' | tr '\n' ' ')
# shellcheck disable=SC2086
security list-keychains -d user -s "$KEYCHAIN_PATH" $CURRENT_KEYCHAINS

log "Importing Developer ID certificate..."
echo "$MACOS_CERTIFICATE" | base64 --decode > "$CERT_FILE"
security import "$CERT_FILE" \
    -k "$KEYCHAIN_PATH" \
    -P "$MACOS_CERTIFICATE_PWD" \
    -T /usr/bin/codesign \
    -T /usr/bin/pkgbuild \
    -T /usr/bin/productbuild

# Allow codesign to access the key without a passphrase dialog.
security set-key-partition-list \
    -S "apple-tool:,apple:,codesign:" \
    -s \
    -k "$MACOS_KEYCHAIN_PASSWORD" \
    "$KEYCHAIN_PATH"

# ---------------------------------------------------------------------------
# Resolve signing identities
# ---------------------------------------------------------------------------
log "Resolving Developer ID Application identity..."
APP_IDENTITY=$(security find-identity -v -p codesigning "$KEYCHAIN_PATH" \
    | grep "Developer ID Application" \
    | head -n 1 \
    | sed 's/.*"\(Developer ID Application.*\)".*/\1/')

if [[ -z "$APP_IDENTITY" ]]; then
    fail "No 'Developer ID Application' identity found in keychain." 2
fi
log "App identity: $APP_IDENTITY"

log "Resolving Developer ID Installer identity..."
INSTALLER_IDENTITY=$(security find-identity -v "$KEYCHAIN_PATH" \
    | grep "Developer ID Installer" \
    | head -n 1 \
    | sed 's/.*"\(Developer ID Installer.*\)".*/\1/')

if [[ -z "$INSTALLER_IDENTITY" ]]; then
    log "WARNING: No 'Developer ID Installer' identity found. Package signing will be skipped."
    INSTALLER_IDENTITY=""
fi

# ---------------------------------------------------------------------------
# Deep-sign the .app bundle
# ---------------------------------------------------------------------------
log "Code-signing .app bundle: $APP_BUNDLE"
ENTITLEMENTS_FILE="$(dirname "$0")/entitlements.plist"
if [[ -f "$ENTITLEMENTS_FILE" ]]; then
    codesign \
        --deep \
        --force \
        --options runtime \
        --entitlements "$ENTITLEMENTS_FILE" \
        --sign "$APP_IDENTITY" \
        --keychain "$KEYCHAIN_PATH" \
        "$APP_BUNDLE" \
        || fail "codesign of .app bundle (with entitlements) failed." 3
else
    log "WARNING: entitlements.plist not found at '$ENTITLEMENTS_FILE'; signing without explicit entitlements."
    codesign \
        --deep \
        --force \
        --options runtime \
        --sign "$APP_IDENTITY" \
        --keychain "$KEYCHAIN_PATH" \
        "$APP_BUNDLE" \
        || fail "codesign of .app bundle failed." 3
fi

log "Verifying .app signature..."
codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE" \
    || fail "codesign --verify of .app bundle failed." 3

# ---------------------------------------------------------------------------
# Build .pkg installer
# ---------------------------------------------------------------------------
PKG_UNSIGNED="$(mktemp /tmp/unsigned.XXXXXX.pkg)"
log "Building .pkg installer: $PKG_UNSIGNED"
pkgbuild \
    --root "$PUBLISH_DIR" \
    --identifier "$APP_BUNDLE_ID" \
    --version "$VERSION" \
    --install-location "/Applications" \
    "$PKG_UNSIGNED" \
    || fail "pkgbuild failed." 4

# ---------------------------------------------------------------------------
# Sign the .pkg installer (if Installer identity is available)
# ---------------------------------------------------------------------------
if [[ -n "$INSTALLER_IDENTITY" ]]; then
    log "Signing .pkg with Developer ID Installer: $INSTALLER_IDENTITY"
    productsign \
        --sign "$INSTALLER_IDENTITY" \
        --keychain "$KEYCHAIN_PATH" \
        "$PKG_UNSIGNED" \
        "$OUTPUT_PKG" \
        || fail "productsign failed." 5
    rm -f "$PKG_UNSIGNED"
else
    log "WARNING: Signing .pkg with Application identity (no Installer identity found)."
    productsign \
        --sign "$APP_IDENTITY" \
        --keychain "$KEYCHAIN_PATH" \
        "$PKG_UNSIGNED" \
        "$OUTPUT_PKG" \
        || fail "productsign (with app identity) failed." 5
    rm -f "$PKG_UNSIGNED"
fi

log "Built signed package: $OUTPUT_PKG"

# ---------------------------------------------------------------------------
# Notarize
# ---------------------------------------------------------------------------
log "Submitting package to Apple Notary Service..."
NOTARIZE_LOG="$(mktemp /tmp/notarize-log.XXXXXX.json)"

xcrun notarytool submit "$OUTPUT_PKG" \
    --apple-id "$APPLE_ID" \
    --password "$APPLE_APP_SPECIFIC_PASSWORD" \
    --team-id "$APPLE_TEAM_ID" \
    --output-format json \
    --wait \
    > "$NOTARIZE_LOG" \
    || { cat "$NOTARIZE_LOG" >&2; fail "notarytool submit failed." 6; }

NOTARIZE_STATUS=$(jq -r '.status // ""' "$NOTARIZE_LOG")
log "Notarization status: $NOTARIZE_STATUS"

if [[ "$NOTARIZE_STATUS" != "Accepted" ]]; then
    cat "$NOTARIZE_LOG" >&2
    fail "Notarization was not accepted (status: $NOTARIZE_STATUS)." 7
fi

# ---------------------------------------------------------------------------
# Staple
# ---------------------------------------------------------------------------
log "Stapling notarization ticket to package..."
xcrun stapler staple "$OUTPUT_PKG" \
    || fail "stapler staple failed." 8

log "Verifying stapled package..."
xcrun stapler validate "$OUTPUT_PKG" \
    || fail "stapler validate failed after stapling." 8

log "SUCCESS: $OUTPUT_PKG is signed, notarized, and stapled."
