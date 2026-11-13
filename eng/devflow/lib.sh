# Helpers for eng/devflow/verify.sh: drive the MAUI sample through the maui devflow CLI and the sample's
# com.kumikoui.sample agent extension (samples/SampleApp.Maui/DevFlow/KumikoDevFlowExtension.cs).
# Sourced, not run. Works with the macOS system bash (3.2) and jq.

EXT=com.kumikoui.sample

# ── CLI ──

# df <args...>: maui devflow against the selected agent. Prints stdout; returns the CLI's exit code.
df() {
	"$MAUI" devflow "$@" --agent-port "$AGENT_PORT"
}

# dfm <args...>: a mutating maui devflow command (tap, gesture, navigate, a POST extension tool).
# Every CLI process claims the app's mutation lease under a new id and keeps it for the lease timeout (10 s), so
# the next process would be refused as "another session". Take the lease over and release it first, through the
# agent's lease endpoint, so each command starts with a free lease.
dfm() {
	free_lease
	df "$@"
}

free_lease() {
	local body='"leaseId":"eng-devflow-verify","holderKind":"cli","label":"eng/devflow verify.sh"'
	curl -s -X POST "http://127.0.0.1:$AGENT_PORT/api/v1/agent/lease" -H 'content-type: application/json' \
		-d "{\"action\":\"claim\",\"force\":true,$body}" >/dev/null
	curl -s -X POST "http://127.0.0.1:$AGENT_PORT/api/v1/agent/lease" -H 'content-type: application/json' \
		-d "{\"action\":\"release\",$body}" >/dev/null
}

# ext <tool> [json]: call a read-only (GET) tool of the sample extension and print its JSON.
ext() {
	local params="${2:-}"
	[ -z "$params" ] && params='{}'
	df extensions call "$EXT" "$1" "$params"
}

# extm <tool> [json]: call a mutating (POST) tool of the sample extension (scroll, edit).
extm() {
	free_lease
	ext "$@"
}

# state [jq filter]: the current page and grid state, optionally reduced by a jq filter.
state() {
	ext state "{\"grid\":${GRID_INDEX:-0}}" | jq -r "${1:-.}"
}

# page_type: the type name of the page on screen (the top modal page, or Shell's current page).
page_type() {
	ext page | jq -r '.type // empty'
}

# element_id <css selector>: the DevFlow element id of the first match on the current page. Shell keeps pages
# it has shown alive, so an unscoped query also finds their (off-window) elements.
element_id() {
	df ui query --selector "$(page_type) $1" | jq -r '.[0].id // empty'
}

# grid_id: the DevFlow element id of the current page's grid (ids change when a page is rebuilt).
grid_id() {
	df ui query --selector "$(page_type) KumikoDataGrid" | jq -r ".[${GRID_INDEX:-0}].id // empty"
}

# ── Input ──

# tap_at <x> <y>: tap the grid at a point (grid-relative DIPs).
tap_at() {
	if [ -z "${2:-}" ] || [ "$2" = null ]; then
		echo "  (no point to tap: '${1:-}' '${2:-}')"
		return 1
	fi
	dfm ui tap "$(grid_id)" --x "$1" --y "$2" >/dev/null
}

# double_tap_at <x> <y>: double-tap the grid at a point: two taps from one batch process, 30 ms apart.
# (DevFlow's own "ui gesture doubletap" did not register on the canvas on Mac Catalyst, and two separate CLI
# processes are too far apart to count as a double tap.)
double_tap_at() {
	local id
	id=$(grid_id)
	free_lease
	printf 'MAUI tap %s --x %s --y %s\nMAUI tap %s --x %s --y %s\n' "$id" "$1" "$2" "$id" "$1" "$2" \
		| "$MAUI" devflow batch --delay 30 --agent-port "$AGENT_PORT" >/dev/null
}

# drag <x> <y> <to-x> <to-y> [hold-ms]: press at a grid point, move, release (a real touch drag).
drag() {
	local hold=()
	[ -n "${5:-}" ] && hold=(--hold "$5")
	dfm ui gesture drag --element "$(grid_id)" --x "$1" --y "$2" --to-x "$3" --to-y "$4" --duration 400 --steps 16 \
		${hold[@]+"${hold[@]}"} >/dev/null
}

# cell_point <row> <column>: "x y" of a cell's center, scrolling it into view first when it is off screen.
cell_point() {
	local json
	json=$(ext cell "{\"grid\":${GRID_INDEX:-0},\"row\":$1,\"column\":\"$2\"}")
	if [ "$(echo "$json" | jq -r .onScreen)" != "true" ]; then
		extm scroll "{\"grid\":${GRID_INDEX:-0},\"row\":$1,\"column\":\"$2\"}" >/dev/null
		settle
		json=$(ext cell "{\"grid\":${GRID_INDEX:-0},\"row\":$1,\"column\":\"$2\"}")
	fi
	echo "$json" | jq -r '"\(.center.x) \(.center.y)"'
}

# tap_cell <row> <column> / double_tap_cell <row> <column>
tap_cell() { local p; p=$(cell_point "$1" "$2"); tap_at ${p% *} ${p#* }; settle; }
double_tap_cell() { local p; p=$(cell_point "$1" "$2"); double_tap_at ${p% *} ${p#* }; settle; }

# header_point <column> <part>: "x y" of a header part: center, filterIcon or resizeGrip.
header_point() {
	local json
	json=$(ext header "{\"grid\":${GRID_INDEX:-0},\"column\":\"$1\"}")
	if [ "$(echo "$json" | jq -r .onScreen)" != "true" ]; then
		extm scroll "{\"grid\":${GRID_INDEX:-0},\"column\":\"$1\"}" >/dev/null
		settle
		json=$(ext header "{\"grid\":${GRID_INDEX:-0},\"column\":\"$1\"}")
	fi
	echo "$json" | jq -r ".$2 | \"\(.x) \(.y)\""
}

# tap_button <text>: tap a button on the current page by its text. Returns 1 when there is none.
tap_button() {
	local id
	id=$(element_id "Button[Text=\"$1\"]")
	if [ -z "$id" ]; then
		echo "  (no button '$1' on $(page_type))"
		return 1
	fi
	dfm ui tap "$id" >/dev/null
	settle
}

# navigate <route>: go to a Shell route and wait for the page to settle.
navigate() {
	dfm ui navigate "$1" >/dev/null
	settle 1
}

settle() { sleep "${1:-0.4}"; }

# ── Screenshots ──

SHOT_INDEX=0
# shot <name>: save a screenshot of the app as NN-name.png in $OUT.
shot() {
	SHOT_INDEX=$((SHOT_INDEX + 1))
	local file
	file=$(printf '%s/%02d-%s.png' "$OUT" "$SHOT_INDEX" "$1")
	df ui screenshot --output "$file" --overwrite >/dev/null 2>&1 || echo "  (screenshot $1 failed)"
}

# ── Results ──

PASSED=0
FAILED=0
FAILURES=""
CURRENT_SECTION=""

section() {
	CURRENT_SECTION="$1"
	printf '\n== %s\n' "$1"
}

pass() {
	PASSED=$((PASSED + 1))
	printf '  PASS  %s\n' "$1"
}

fail() {
	FAILED=$((FAILED + 1))
	printf '  FAIL  %s%s\n' "$1" "${2:+ ($2)}"
	FAILURES="$FAILURES
  [$CURRENT_SECTION] $1${2:+ ($2)}"
}

SKIPPED=0
skip() {
	SKIPPED=$((SKIPPED + 1))
	printf '  SKIP  %s (%s)\n' "$1" "$2"
}

# app_alive: the app answers within 5 seconds (its UI thread is not stuck).
app_alive() {
	[ -n "$(curl -s --max-time 5 "http://127.0.0.1:$AGENT_PORT/api/v1/ext/$EXT/page")" ]
}

# check <description> <actual> <expected>: equality.
check() {
	if [ "$2" = "$3" ]; then pass "$1"; else fail "$1" "expected '$3', got '$2'"; fi
}

# check_true <description> <jq expression over the state JSON> [state JSON]: the expression is true.
check_true() {
	local json="${3:-}"
	[ -z "$json" ] && json=$(ext state "{\"grid\":${GRID_INDEX:-0}}")
	local result
	result=$(echo "$json" | jq -r "$2" 2>&1)
	if [ "$result" = "true" ]; then pass "$1"; else fail "$1" "$2 -> $result"; fi
}

# check_cmd <description> <command...>: the command succeeds.
check_cmd() {
	local description="$1"
	shift
	local output
	if output=$("$@" 2>&1); then pass "$description"; else fail "$description" "$(echo "$output" | tail -1)"; fi
}
