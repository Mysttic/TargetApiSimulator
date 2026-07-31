#!/usr/bin/env bash
#
# Checks a running TargetApiSimulator against its documented contract.
#
# Start the simulator first, then point this script at it. Every check prints PASS or FAIL and
# the script exits with 1 if anything failed, so it can be used as a CI gate.
#
#   ./TargetApiSimulator --urls http://localhost:5000
#   ./smoke-test.sh
#   ./smoke-test.sh http://localhost:8080

set -uo pipefail

BASE_URL="${1:-http://localhost:5000}"
BASE_URL="${BASE_URL%/}"
TARGET="$BASE_URL/api/target"

PASSED=0
FAILED=0

if [[ -t 1 ]]; then
  GREEN=$'\033[32m'; RED=$'\033[31m'; CYAN=$'\033[36m'; GREY=$'\033[90m'; RESET=$'\033[0m'
else
  GREEN=''; RED=''; CYAN=''; GREY=''; RESET=''
fi

assert() {
  local name="$1" expected="$2" actual="$3"
  if [[ "$expected" == "$actual" ]]; then
    printf '  %sPASS%s  %s\n' "$GREEN" "$RESET" "$name"
    PASSED=$((PASSED + 1))
  else
    printf '  %sFAIL%s  %s\n' "$RED" "$RESET" "$name"
    printf '        %sexpected: %s%s\n' "$GREY" "$expected" "$RESET"
    printf '        %sactual:   %s%s\n' "$GREY" "$actual" "$RESET"
    FAILED=$((FAILED + 1))
  fi
}

# Prints "<status> <body>" for a POST with the given body.
post() {
  local body="$1" ctype="${2:-application/json}"
  curl -s -o /tmp/tas_body.$$ -w '%{http_code}' \
    -X POST "$TARGET" -H "Content-Type: $ctype" --data-binary "$body" 2>/dev/null
  printf ' '
  cat /tmp/tas_body.$$ 2>/dev/null
  rm -f /tmp/tas_body.$$
}

printf '\n%sTargetApiSimulator smoke test -> %s%s\n\n' "$CYAN" "$BASE_URL" "$RESET"

if ! curl -sf -o /dev/null --max-time 5 "$BASE_URL/healthz"; then
  printf '%sCannot reach %s - is the simulator running?%s\n' "$RED" "$BASE_URL" "$RESET"
  printf '%sStart it with: ./TargetApiSimulator --urls %s%s\n' "$GREY" "$BASE_URL" "$RESET"
  exit 2
fi

echo 'Service endpoints'
assert 'GET /healthz returns 200' '200' \
  "$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/healthz")"
assert 'GET /healthz body' '{"status":"ok"}' "$(curl -s "$BASE_URL/healthz")"

VERSION_BODY="$(curl -s "$BASE_URL/version")"
assert 'GET /version returns 200' '200' \
  "$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/version")"
if [[ "$VERSION_BODY" =~ \"version\":\"[^\"]+\" ]]; then
  assert 'GET /version reports a version' 'ok' 'ok'
else
  assert 'GET /version reports a version' 'ok' "$VERSION_BODY"
fi
printf '        %sversion: %s%s\n' "$GREY" "$VERSION_BODY" "$RESET"

echo
echo 'Valid payloads'
for payload in '{"key":"value"}' '[1,2,3]' '{}' '[]' '123' '"str"' 'true' 'null'; do
  assert "POST $payload -> 200 true" '200 true' "$(post "$payload")"
done

echo
echo 'Rejected payloads'
for payload in 'not json at all' '{"a":}' '{"a":1,}' '{"a":1} // c' '{' ''; do
  label="${payload:-(empty body)}"
  assert "POST $label -> 400" '400 {"ErrorMessage": "This is not JSON"}' "$(post "$payload")"
done

echo
echo 'Contract details'
assert 'Response declares JSON content type' 'application/json; charset=utf-8' \
  "$(curl -s -D - -o /dev/null -X POST "$TARGET" -H 'Content-Type: application/json' \
      --data-binary '{"a":1}' | awk 'BEGIN{IGNORECASE=1} /^content-type:/{sub(/^[^:]*: /,""); print}' | tr -d '\r')"

DEEP="$(printf '[%.0s' $(seq 1 65))$(printf ']%.0s' $(seq 1 65))"
assert 'Nesting deeper than 64 -> 400' '400' \
  "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$TARGET" \
      -H 'Content-Type: application/json' --data-binary "$DEEP")"

assert 'Content-Type is ignored -> 200' '200' \
  "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$TARGET" \
      -H 'Content-Type: text/plain' --data-binary '{"a":1}')"

assert 'GET /api/target -> 405' '405' \
  "$(curl -s -o /dev/null -w '%{http_code}' -X GET "$TARGET")"
assert '405 advertises Allow: POST' 'POST' \
  "$(curl -s -D - -o /dev/null -X GET "$TARGET" \
      | awk 'BEGIN{IGNORECASE=1} /^allow:/{sub(/^[^:]*: /,""); print}' | tr -d '\r')"

assert 'Unknown path -> 404' '404' \
  "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/does-not-exist" --data-binary '{}')"

BIG="$(mktemp)"
{ printf '{"a":"'; head -c 1100000 /dev/zero | tr '\0' 'x'; printf '"}'; } > "$BIG"
assert 'Body over 1 MB -> 413' '413' \
  "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$TARGET" \
      -H 'Content-Type: application/json' --data-binary @"$BIG")"
rm -f "$BIG"

echo
echo 'Failure injection (optional feature)'
SIM="$(curl -s -o /dev/null -w '%{http_code}' -X POST "$TARGET" \
        -H 'Content-Type: application/json' -H 'X-Sim-Status: 503' --data-binary '{"a":1}')"
if [[ "$SIM" == "503" ]]; then
  printf '  %sINFO%s  X-Sim-* control headers are enabled; forced 503 works\n' "$CYAN" "$RESET"
else
  printf '  %sINFO  X-Sim-* control headers are off (the shipped default)%s\n' "$GREY" "$RESET"
  printf '        %sEnable with: Simulator__EnableControlHeaders=true%s\n' "$GREY" "$RESET"
fi

echo
if [[ "$FAILED" -eq 0 ]]; then
  printf '%sAll %d checks passed.%s\n' "$GREEN" "$PASSED" "$RESET"
  exit 0
fi

printf '%s%d passed, %d FAILED.%s\n' "$RED" "$PASSED" "$FAILED" "$RESET"
exit 1
