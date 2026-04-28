#!/usr/bin/env bash
# verify-release.sh — Verify that a macOS release artifact is correctly signed,
#                     notarized, and passes Gatekeeper assessment.
#
# Usage:
#   ./scripts/verify-release.sh <artifact-path>
#
#   <artifact-path>  Path to a signed .app bundle or .pkg file.
#
# Exit codes:
#   0  All checks passed.
#   1  Missing argument.
#   2  codesign verification failed.
#   3  spctl assessment failed (Gatekeeper would reject the artifact).
#   4  Notarization ticket not found / stapler validation failed.

set -euo pipefail

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
log()     { echo "[verify-release] $*"; }
log_ok()  { echo "[verify-release] ✓ $*"; }
log_err() { echo "[verify-release] ✗ $*" >&2; }
fail()    { log_err "$1"; exit "${2:-1}"; }

# ---------------------------------------------------------------------------
# Arguments
# ---------------------------------------------------------------------------
if [[ $# -lt 1 ]]; then
    fail "Usage: $0 <artifact-path>" 1
fi

ARTIFACT="$1"

if [[ ! -e "$ARTIFACT" ]]; then
    fail "Artifact not found: $ARTIFACT" 1
fi

ARTIFACT_NAME=$(basename "$ARTIFACT")
EXTENSION="${ARTIFACT_NAME##*.}"

log "Verifying: $ARTIFACT"
CODESIGN_OK=0
SPCTL_OK=0
STAPLER_OK=0

# ---------------------------------------------------------------------------
# 1. codesign verification
# ---------------------------------------------------------------------------
log "Running codesign verification..."
if codesign --verify --deep --strict --verbose=2 "$ARTIFACT" 2>&1; then
    log_ok "codesign --verify passed."
    CODESIGN_OK=1
else
    log_err "codesign --verify FAILED for: $ARTIFACT"
fi

# ---------------------------------------------------------------------------
# 2. Display signing details for audit
# ---------------------------------------------------------------------------
log "Signing details:"
codesign --display --verbose=4 "$ARTIFACT" 2>&1 || true

# ---------------------------------------------------------------------------
# 3. Gatekeeper (spctl) assessment
# ---------------------------------------------------------------------------
log "Running Gatekeeper (spctl) assessment..."

SPCTL_TYPE="execute"
if [[ "$EXTENSION" == "pkg" ]]; then
    SPCTL_TYPE="install"
fi

if spctl --assess --type "$SPCTL_TYPE" --verbose=2 "$ARTIFACT" 2>&1; then
    log_ok "spctl assessment passed (Gatekeeper: ACCEPTED)."
    SPCTL_OK=1
else
    log_err "spctl assessment FAILED — Gatekeeper would reject this artifact."
fi

# ---------------------------------------------------------------------------
# 4. Notarization ticket stapled check
# ---------------------------------------------------------------------------
log "Checking stapled notarization ticket..."
if xcrun stapler validate "$ARTIFACT" 2>&1; then
    log_ok "Notarization ticket is stapled and valid."
    STAPLER_OK=1
else
    log_err "Notarization ticket check FAILED — ticket may be missing or invalid."
fi

# ---------------------------------------------------------------------------
# Summary — report all failures before exiting with the highest-priority code
# ---------------------------------------------------------------------------
if [[ $CODESIGN_OK -eq 1 && $SPCTL_OK -eq 1 && $STAPLER_OK -eq 1 ]]; then
    log_ok "All verification checks PASSED for: $ARTIFACT_NAME"
    exit 0
fi

FAILED=()
[[ $CODESIGN_OK -eq 0 ]] && FAILED+=("codesign")
[[ $SPCTL_OK -eq 0 ]]    && FAILED+=("spctl/Gatekeeper")
[[ $STAPLER_OK -eq 0 ]]  && FAILED+=("notarization staple")

log_err "${#FAILED[@]} verification check(s) FAILED for $ARTIFACT_NAME: ${FAILED[*]}"

# Return the most-specific exit code for the first failing check.
[[ $CODESIGN_OK -eq 0 ]] && exit 2
[[ $SPCTL_OK -eq 0 ]]    && exit 3
exit 4
