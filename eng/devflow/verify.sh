#!/bin/bash
# Verifies the MAUI sample end to end through .NET MAUI DevFlow (#262): builds and launches
# samples/SampleApp.Maui (Debug, with the DevFlow agent), visits every page and exercises the grid with real
# taps and drags at computed cell coordinates, asserting the grid's state through the sample's
# com.kumikoui.sample agent extension and saving a screenshot per step. See eng/devflow/README.md.
#
#   eng/devflow/verify.sh [--platform maccatalyst|ios|android] [--device <udid or name>] [--no-build]
#                         [--agent-port <port>] [--out <dir>] [--keep-running]
#
# Exit code: 0 when every check passed, 1 when one failed, 2 when the app could not be built or reached.

set -u

# One compound command: bash parses the whole script before running it, so editing the file during a run is safe.
{

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
PROJECT="$ROOT/samples/SampleApp.Maui/SampleApp.Maui.csproj"
BUNDLE_ID=com.companyname.sampleapp.maui
PLATFORM=maccatalyst
DEVICE=""
BUILD=1
AGENT_PORT=""
OUT=""
KEEP=0

while [ $# -gt 0 ]; do
	case "$1" in
		--platform) PLATFORM="$2"; shift 2 ;;
		--device) DEVICE="$2"; shift 2 ;;
		--no-build) BUILD=0; shift ;;
		--agent-port) AGENT_PORT="$2"; shift 2 ;;
		--out) OUT="$2"; shift 2 ;;
		--keep-running) KEEP=1; shift ;;
		-h|--help) sed -n '2,11p' "$0"; exit 0 ;;
		*) echo "unknown option: $1" >&2; exit 2 ;;
	esac
done

MAUI=${MAUI:-$(command -v maui || true)}
if [ -z "$MAUI" ]; then
	echo "The maui CLI was not found. Install it (see eng/devflow/README.md) or set MAUI=/path/to/maui." >&2
	exit 2
fi
for tool in jq curl; do
	command -v "$tool" >/dev/null || { echo "$tool is required" >&2; exit 2; }
done

OUT=${OUT:-"$ROOT/artifacts/devflow/$PLATFORM-$(date +%Y%m%d-%H%M%S)"}
mkdir -p "$OUT"

# shellcheck source=lib.sh
. "$(dirname "$0")/lib.sh"

# ── Build, launch, connect ──

expected_version=$(sed -n 's:.*<MauiDevFlowVersion>\(.*\)</MauiDevFlowVersion>.*:\1:p' "$ROOT/Directory.Packages.props")
cli_version=$("$MAUI" --version 2>/dev/null | head -1)
case "$cli_version" in
	"$expected_version"*) ;;
	*) echo "warning: maui CLI $cli_version, agent package $expected_version; install the matching CLI if commands fail." ;;
esac

APP_PID=""
SIM_UDID=""

launch_maccatalyst() {
	if [ $BUILD = 1 ]; then
		echo "Building the sample for Mac Catalyst (Debug)..."
		dotnet build "$PROJECT" -f net10.0-maccatalyst -c Debug -v:q -nologo >"$OUT/build.log" 2>&1 \
			|| { tail -20 "$OUT/build.log"; echo "build failed: $OUT/build.log" >&2; exit 2; }
	fi
	local app
	app=$(ls -d "$ROOT"/samples/SampleApp.Maui/bin/Debug/net10.0-maccatalyst/*/SampleApp.Maui.app | head -1)
	[ -d "$app" ] || { echo "no Mac Catalyst build in samples/SampleApp.Maui/bin/Debug" >&2; exit 2; }
	"$app/Contents/MacOS/SampleApp.Maui" >"$OUT/app.log" 2>&1 &
	APP_PID=$!
}

launch_ios() {
	if [ -z "$DEVICE" ]; then
		# A simulator of our own, so another project's app on a shared simulator is not replaced. An iPad: the
		# All Components grid's frozen columns are wider than a phone in portrait (#268).
		DEVICE=$(xcrun simctl list devices available -j | jq -r '.devices[][] | select(.name == "KumikoUI-DevFlow-iPad") | .udid' | head -1)
		if [ -z "$DEVICE" ]; then
			# The newest iOS runtime and the newest 11-inch iPad Pro it supports.
			local runtime type
			runtime=$(xcrun simctl list runtimes available -j | jq -c '[.runtimes[] | select(.platform == "iOS")] | last')
			type=$(echo "$runtime" | jq -r '[.supportedDeviceTypes[] | select(.name | test("^iPad Pro 11-inch \\(M[0-9]+\\)$"))] | first | .identifier')
			runtime=$(echo "$runtime" | jq -r .identifier)
			echo "Creating simulator KumikoUI-DevFlow-iPad ($type, $runtime)..."
			DEVICE=$(xcrun simctl create KumikoUI-DevFlow-iPad "$type" "$runtime")
		fi
	fi
	SIM_UDID=$(xcrun simctl list devices -j | jq -r --arg d "$DEVICE" '.devices[][] | select(.udid == $d or .name == $d) | .udid' | head -1)
	[ -n "$SIM_UDID" ] || { echo "no simulator matches '$DEVICE'" >&2; exit 2; }
	xcrun simctl boot "$SIM_UDID" 2>/dev/null
	xcrun simctl bootstatus "$SIM_UDID" -b >/dev/null

	if [ $BUILD = 1 ]; then
		echo "Building the sample for the iOS Simulator (Debug)..."
		dotnet build "$PROJECT" -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64 -v:q -nologo \
			>"$OUT/build.log" 2>&1 || { tail -20 "$OUT/build.log"; echo "build failed: $OUT/build.log" >&2; exit 2; }
	fi
	local app="$ROOT/samples/SampleApp.Maui/bin/Debug/net10.0-ios/iossimulator-arm64/SampleApp.Maui.app"
	[ -d "$app" ] || { echo "no iOS Simulator build at $app" >&2; exit 2; }
	xcrun simctl terminate "$SIM_UDID" "$BUNDLE_ID" 2>/dev/null
	xcrun simctl install "$SIM_UDID" "$app" || exit 2
	APP_PID=$(xcrun simctl launch "$SIM_UDID" "$BUNDLE_ID" | awk '{ print $NF }')
}

# An Android device or emulator (--device <adb serial>, optional when exactly one is connected). The Debug build is
# installed with -t:Install (a Fast Deployment APK has no assemblies in it), launched, and the agent's port (9223 in
# the app) is forwarded to a free local port: the broker does not see an agent on a device.
ANDROID_SERIAL_ARG=""
launch_android() {
	command -v adb >/dev/null || { echo "adb is required for --platform android" >&2; exit 2; }
	if [ -z "$DEVICE" ]; then
		DEVICE=$(adb devices | awk 'NR > 1 && $2 == "device" { print $1 }')
		[ "$(echo "$DEVICE" | grep -c .)" = 1 ] || { echo "pass --device <adb serial>: $(echo $DEVICE | tr '\n' ' ')" >&2; exit 2; }
	fi
	ANDROID_SERIAL_ARG=$DEVICE
	adb -s "$DEVICE" wait-for-device
	if [ $BUILD = 1 ]; then
		echo "Building and installing the sample on $DEVICE (Debug)..."
		dotnet build "$PROJECT" -f net10.0-android -c Debug -t:Install "-p:AdbTarget=-s $DEVICE" -v:q -nologo \
			>"$OUT/build.log" 2>&1 || { tail -20 "$OUT/build.log"; echo "build failed: $OUT/build.log" >&2; exit 2; }
	fi
	adb -s "$DEVICE" shell am force-stop "$BUNDLE_ID"
	adb -s "$DEVICE" logcat -c
	adb -s "$DEVICE" shell am start -n "$(adb -s "$DEVICE" shell cmd package resolve-activity --brief "$BUNDLE_ID" | tail -1 | tr -d '\r')" >/dev/null
	APP_PID=android
	AGENT_PORT=$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1])')
	adb -s "$DEVICE" forward "tcp:$AGENT_PORT" tcp:9223 >/dev/null
	# A Debug app on an emulator takes a while to start (JIT).
	local i
	for i in $(seq 1 120); do
		[ -n "$(ext page 2>/dev/null | jq -r '.type // empty' 2>/dev/null)" ] && return 0
		sleep 1
	done
	echo "the app's DevFlow agent did not answer on $DEVICE (forwarded to port $AGENT_PORT)" >&2
	exit 2
}

# Waits for the agent of the app we launched (by process id) to register with the DevFlow broker.
connect() {
	local i port
	for i in $(seq 1 90); do
		port=$("$MAUI" devflow list 2>/dev/null | jq -r --argjson pid "${APP_PID:-0}" '.[] | select(.processId == $pid) | .port' 2>/dev/null | head -1)
		if [ -n "$port" ]; then
			AGENT_PORT=$port
			# The agent registers before the app has bound its window and the extension can answer.
			[ -n "$(ext page 2>/dev/null | jq -r '.type // empty' 2>/dev/null)" ] && return 0
		fi
		sleep 1
	done
	echo "the app's DevFlow agent did not connect (pid $APP_PID); see $OUT/app.log" >&2
	exit 2
}

cleanup() {
	[ $KEEP = 1 ] && return
	[ -n "$APP_PID" ] || return
	if [ "$PLATFORM" = ios ]; then
		xcrun simctl terminate "$SIM_UDID" "$BUNDLE_ID" 2>/dev/null
	elif [ "$PLATFORM" = android ]; then
		adb -s "$ANDROID_SERIAL_ARG" shell am force-stop "$BUNDLE_ID"
		adb -s "$ANDROID_SERIAL_ARG" forward --remove "tcp:$AGENT_PORT" 2>/dev/null
	else
		kill "$APP_PID" 2>/dev/null
	fi
}

if [ -z "$AGENT_PORT" ]; then
	case "$PLATFORM" in
		maccatalyst) launch_maccatalyst ;;
		ios) launch_ios ;;
		android) launch_android ;;
		*) echo "unsupported platform: $PLATFORM (use maccatalyst, ios or android)" >&2; exit 2 ;;
	esac
	trap cleanup EXIT
	[ "$PLATFORM" = android ] || connect
fi
echo "Connected to the DevFlow agent on port $AGENT_PORT; screenshots in $OUT"
df ui status >"$OUT/status.json" 2>&1

# A fixed window on Mac Catalyst, narrow enough that the grid scrolls sideways (frozen-column checks).
if [ "$PLATFORM" = maccatalyst ]; then
	dfm ui resize 1000 800 >/dev/null
	settle 1
fi

# ── Helpers for the checks ──

# wait_until <description> <jq condition over the state> [seconds]: poll the state until the condition holds.
wait_until() {
	local i limit=$(( ${3:-10} * 4 ))
	for i in $(seq 1 $limit); do
		[ "$(state "$2" 2>/dev/null)" = "true" ] && { pass "$1"; return 0; }
		sleep 0.25
	done
	fail "$1" "$2 still false after ${3:-10}s"
	return 1
}

# column_numbers <column> <count>: the first <count> display texts of a column as plain numbers, one per line.
column_numbers() {
	ext rows "{\"grid\":${GRID_INDEX:-0},\"column\":\"$1\",\"count\":$2}" | jq -r '.rows[].text // empty' | tr -cd '0-9.\n-'
}

# is_sorted <asc|desc>: stdin numbers are sorted.
is_sorted() {
	awk -v dir="$1" 'NR > 1 { if ((dir == "asc" && $1 + 0 < prev) || (dir == "desc" && $1 + 0 > prev)) bad = 1 } { prev = $1 + 0 } END { print bad ? "false" : "true" }'
}

header_x() { ext header "{\"grid\":${GRID_INDEX:-0},\"column\":\"$1\"}" | jq -r '.bounds.x'; }
cell_text() { ext cell "{\"grid\":${GRID_INDEX:-0},\"row\":$1,\"column\":\"$2\"}" | jq -r '.text'; }

# luminance <#RRGGBB>: perceived brightness 0..255.
luminance() {
	local hex=${1#\#}
	echo $(( (299 * 16#${hex:0:2} + 587 * 16#${hex:2:2} + 114 * 16#${hex:4:2}) / 1000 ))
}

# ── Pages ──

test_navigation() {
	section "Navigation: every page loads with a populated grid"
	local route
	for route in MainPage GroupingPage LargeDataPage ThemingPage MvvmActionsPage CustomFontsPage StylingPage EventsPage; do
		navigate "//$route"
		local json
		json=$(ext state '{}')
		check "$route: route" "$(echo "$json" | jq -r .page.route)" "//$route"
		check_true "$route: has a grid" '(.grids | length) >= 1' "$json"
		check_true "$route: grid has a size" '.grid.width > 0 and .grid.height > 0' "$json"
		[ "$route" != LargeDataPage ] && check_true "$route: grid has rows" '.grid.rowCount > 0' "$json"
		shot "page-$route"
	done
}

# frozen_stays_put <column> <x before the scroll> <mayUnpin>: a frozen column keeps its x while the grid scrolls sideways.
# Where the grid is too narrow for the frozen panes (mayUnpin, from the extension) Core unpins columns (#268) and a
# column that moved is then expected; anywhere else it must not move.
frozen_stays_put() {
	local now
	now=$(header_x "$1")
	if [ "$now" = "$2" ]; then
		pass "frozen $1 stays put"
	elif [ "$3" = true ]; then
		pass "frozen $1 scrolled with the columns: the frozen panes are wider than the grid allows, so Core unpinned it (x $2 -> $now)"
	else
		fail "frozen $1 stays put" "expected '$2', got '$now'"
	fi
}

test_main_page() {
	section "All Components: summaries, sorting, selection"
	navigate //MainPage
	local json items
	json=$(ext state '{}')
	items=$(echo "$json" | jq -r .grid.itemCount)
	check_true "top summary row (Averages) has values" '.grid.summaries.top | length > 0 and (.[0].values | length) > 0' "$json"
	check_true "bottom summary counts every row" ".grid.summaries.bottom[0].values | to_entries | map(.value) | any(test(\"$items\"))" "$json"
	check_true "two frozen rows" '.grid.frozenRowCount == 2' "$json"

	local p
	p=$(header_point Salary center)
	tap_at $p; settle
	check "header tap sorts ascending" "$(state '.grid.sort | map("\(.header) \(.direction)") | join(",")')" "Salary Ascending"
	check "rows are in ascending salary order" "$(column_numbers Salary 12 | is_sorted asc)" "true"
	tap_at $p; settle
	check "second tap sorts descending" "$(state '.grid.sort | map("\(.header) \(.direction)") | join(",")')" "Salary Descending"
	check "rows are in descending salary order" "$(column_numbers Salary 12 | is_sorted desc)" "true"
	shot main-sorted-descending

	tap_cell 3 Name
	json=$(ext state '{}')
	check "tap selects the row" "$(echo "$json" | jq -c .grid.selection.rows)" "[3]"
	check "tap moves the current cell" "$(echo "$json" | jq -r '.grid.currentCell | "\(.row),\(.header)"')" "3,Name"
	tap_cell 5 Department
	check "a second tap replaces the selection" "$(state '.grid.selection.rows | tostring')" "[5]"
	shot main-selection

	section "All Components: editing"
	local before after
	double_tap_cell 4 Name
	check_true "double tap opens a text editor on Name" '.grid.edit.isEditing and .grid.edit.editor == "DrawnTextBox" and .grid.edit.cell.header == "Name"'
	shot main-edit-text
	extm edit '{"action":"type","text":"DevFlow Edit"}' >/dev/null
	extm edit '{"action":"commit"}' >/dev/null
	settle
	check "text edit commits" "$(cell_text 4 Name)" "DevFlow Edit"

	before=$(state '.grid.summaries.bottom[0].values.Salary')
	double_tap_cell 4 Salary
	check_true "double tap opens an editor on the numeric Salary" '.grid.edit.isEditing and .grid.edit.cell.header == "Salary"'
	extm edit '{"action":"type","text":"123456"}' >/dev/null
	extm edit '{"action":"commit"}' >/dev/null
	settle
	# The grid is sorted by salary, so the edited row moves; follow it by the name given it above.
	local moved
	moved=$(ext find '{"column":"Name","text":"DevFlow Edit"}' | jq -r .row)
	check "numeric edit commits and formats" "$(cell_text "$moved" Salary)" "\$123,456"
	if [ "$moved" != 4 ]; then pass "the edited row moved to its sorted place (row 4 -> $moved)"; else fail "the edited row moved to its sorted place" "still row 4"; fi
	after=$(state '.grid.summaries.bottom[0].values.Salary')
	if [ "$before" != "$after" ]; then pass "salary total follows the edit ($before -> $after)"; else fail "salary total follows the edit" "still $after"; fi

	double_tap_cell 4 "Hire Date"
	check_true "double tap opens the date picker" '.grid.edit.isEditing and .grid.edit.editor == "DrawnDatePicker"'
	shot main-edit-date
	extm edit '{"action":"set","value":"2020-01-15"}' >/dev/null
	extm edit '{"action":"commit"}' >/dev/null
	settle
	check "date edit commits" "$(cell_text 4 "Hire Date")" "2020-01-15"

	before=$(cell_text 6 Active)
	tap_cell 6 Active
	after=$(cell_text 6 Active)
	if [ "$before" != "$after" ]; then pass "tap toggles the Active check box ($before -> $after)"; else fail "tap toggles the Active check box" "still $after"; fi
	tap_cell 6 Active
	check "a second tap toggles it back" "$(cell_text 6 Active)" "$before"
	check_true "no editor left open" '.grid.edit.isEditing | not'

	section "All Components: columns"
	# The header's resize grip and drop targets are read straight from the header geometry (not header_point, which
	# scrolls a column to its start when its center is under a frozen pane): on a phone the scrolling pane is narrower
	# than a column plus the room a drag needs, so the grid is scrolled sideways until the points are on screen.
	local width grip y grid_width need gripx
	width=$(state '.grid.columns[] | select(.header == "Department") | .width')
	grid_width=$(state '.grid.width')
	grip=$(header_point Department resizeGrip)
	gripx=${grip% *}
	# The drag ends 60 to the right of the grip; keep 20 more clear of the grid's right edge.
	need=$(awk -v x="$gripx" -v w="$grid_width" 'BEGIN { n = x + 60 + 20 - w; print (n > 0 ? n : 0) }')
	if [ "$need" != 0 ]; then
		extm scroll "{\"horizontal\":$(awk -v h="$(state '.grid.scroll.horizontal')" -v n="$need" 'BEGIN { print h + n }')}" >/dev/null; settle
		grip=$(ext header '{"column":"Department"}' | jq -r '.resizeGrip | "\(.x) \(.y)"')
	fi
	check "the resize grip is where the hit tester expects it" \
		"$(ext header '{"column":"Department"}' | jq -r .resizeGrip.region)" "HeaderResizeGrip"
	drag ${grip% *} ${grip#* } "$(awk -v x="${grip% *}" 'BEGIN { print x + 60 }')" ${grip#* }; settle
	# Within a DIP, or on Android within the touch slop (8 DIPs plus a pixel): adb input events land on whole pixels, which
	# are not whole DIPs at Android's densities, and a swipe starts moving the column only once it is past the slop.
	local tolerance=1
	[ "$PLATFORM" = android ] && tolerance=10
	check_true "dragging the grip widens Department by 60 (within $tolerance DIP)" \
		"(.grid.columns[] | select(.header == \"Department\") | .width) - $width - 60 | fabs <= $tolerance"
	shot main-resized
	extm scroll '{"horizontal":0}' >/dev/null; settle

	# Reorder: press Hire Date's header and release on the left half of Salary's. A drop only picks a slot among the
	# columns of the pane (the pointer is kept within it), so Salary may be partly under the frozen pane. Where Hire
	# Date's center is off screen (a phone: the scrolling pane is narrower than two columns) scroll until Salary's
	# left edge is 40 under the pane, press Hire Date's visible part and release just inside the pane.
	local salary_x hire_x frozen_edge start_x drop_x
	if awk -v c="$(ext header '{"column":"Hire Date"}' | jq -r '.center.x')" -v w="$grid_width" 'BEGIN { exit !(c > w - 20) }'; then
		y=$(ext header '{"column":"Hire Date"}' | jq -r '.center.y')
		frozen_edge=$(ext header '{"column":"Name"}' | jq -r '.bounds.x + .bounds.width')
		salary_x=$(header_x Salary)
		extm scroll "{\"horizontal\":$(awk -v h="$(state '.grid.scroll.horizontal')" -v s="$salary_x" -v f="$frozen_edge" 'BEGIN { print h + s - f + 40 }')}" >/dev/null; settle
		hire_x=$(header_x "Hire Date")
		start_x=$(awk -v x="$hire_x" 'BEGIN { print x + 28 }')
		drop_x=$(awk -v f="$frozen_edge" 'BEGIN { print f + 6 }')
	else
		p=$(header_point "Hire Date" center)
		y=${p#* }
		start_x=${p% *}
		drop_x=$(awk -v x="$(header_x Salary)" 'BEGIN { print x + 10 }')
	fi
	drag $start_x $y $drop_x $y; settle
	check_true "dragging Hire Date onto Salary's left edge moves it before Salary" \
		'(.grid.columns[] | select(.header == "Hire Date") | .displayIndex) < (.grid.columns[] | select(.header == "Salary") | .displayIndex)'
	shot main-reordered
	extm scroll '{"horizontal":0}' >/dev/null; settle

	section "All Components: scrolling and frozen panes"
	local id_x name_x city_x level_x row0_y row5_y may_unpin
	id_x=$(header_x Id); name_x=$(header_x Name); city_x=$(header_x City); level_x=$(header_x Level)
	# On a grid too narrow for its frozen columns (a phone, #268) Core unpins some, and they scroll with the others.
	may_unpin=$(state '.grid.frozenPanes.mayUnpin')
	if [ "$(state '.grid.scroll.horizontal')" != "0" ]; then extm scroll '{"horizontal":0}' >/dev/null; settle; level_x=$(header_x Level); fi
	p=$(cell_point 6 Level)
	drag ${p% *} ${p#* } "$(awk -v x="${p% *}" 'BEGIN { print x - 250 }')" ${p#* }; settle 1
	if [ "$(state '.grid.scroll.horizontal > 0')" = "true" ]; then
		pass "a sideways drag scrolls horizontally ($(state '.grid.scroll.horizontal'))"
		frozen_stays_put Id "$id_x" "$may_unpin"
		frozen_stays_put Name "$name_x" "$may_unpin"
		frozen_stays_put City "$city_x" "$may_unpin"
		if awk -v a="$(header_x Level)" -v b="$level_x" 'BEGIN { exit !(a < b) }'; then pass "scrolling column Level moved left"; else fail "scrolling column Level moved left" "x $level_x -> $(header_x Level)"; fi
	else
		fail "a sideways drag scrolls horizontally" "offset $(state '.grid.scroll.horizontal'), max may be 0 at this window size"
	fi
	shot main-scrolled-horizontally

	row0_y=$(ext cell '{"row":0,"column":"Name"}' | jq -r .bounds.y)
	row5_y=$(ext cell '{"row":5,"column":"Name"}' | jq -r .bounds.y)
	p=$(cell_point 8 Level)
	drag ${p% *} ${p#* } ${p% *} "$(awk -v y="${p#* }" 'BEGIN { print y - 200 }')"; settle 1
	if [ "$(state '.grid.scroll.vertical > 0')" = "true" ]; then pass "an upward drag scrolls down ($(state '.grid.scroll.vertical'))"; else fail "an upward drag scrolls down" "offset 0"; fi
	check "frozen row 0 stays put" "$(ext cell '{"row":0,"column":"Name"}' | jq -r .bounds.y)" "$row0_y"
	if [ "$(ext cell '{"row":5,"column":"Name"}' | jq -r .bounds.y)" != "$row5_y" ]; then pass "scrolling row 5 moved"; else fail "scrolling row 5 moved"; fi
	shot main-scrolled-vertically
	extm scroll '{"vertical":0,"horizontal":0}' >/dev/null

	section "All Components: filtering"
	local popup first
	p=$(header_point Department filterIcon)
	tap_at $p; settle
	popup=$(ext popup '{}')
	check "the funnel opens the filter panel" "$(echo "$popup" | jq -r .type)" "DrawnFilterPopup"
	shot main-filter-open
	first=$(echo "$popup" | jq -r '.items[0].value')
	tap_at $(echo "$popup" | jq -r '.parts.deselectAll | "\(.x + .width / 2) \(.y + .height / 2)"'); settle
	check "Deselect All unchecks every value" "$(ext popup '{}' | jq -r '.checkedValues | length')" "0"
	tap_at $(ext popup '{}' | jq -r '.items[0].bounds | "\(.x + 20) \(.y + .height / 2)"'); settle
	check "tapping a value checks it" "$(ext popup '{}' | jq -c .checkedValues)" "[\"$first\"]"
	tap_at $(ext popup '{}' | jq -r '.parts.ok | "\(.x + .width / 2) \(.y + .height / 2)"'); settle
	json=$(ext state '{}')
	check "OK applies a Department filter" "$(echo "$json" | jq -c .grid.filters)" '["Department"]'
	check_true "the filter hides rows" ".grid.rowCount < $items" "$json"
	check "every remaining row is $first" \
		"$(ext rows '{"column":"Department","count":25}' | jq -r --arg v "$first" '[.rows[].text] | all(. == $v)')" "true"
	shot main-filtered
	tap_at $p; settle
	tap_at $(ext popup '{}' | jq -r '.parts.clear | "\(.x + .width / 2) \(.y + .height / 2)"'); settle
	json=$(ext state '{}')
	check "Clear removes the filter" "$(echo "$json" | jq -c .grid.filters)" "[]"
	check "all rows are back" "$(echo "$json" | jq -r .grid.rowCount)" "$items"
}

test_grouping_page() {
	section "Grouping: group, collapse, expand"
	navigate //GroupingPage
	tap_button "Group by Department"
	local json rows
	json=$(ext state '{}')
	check "Group by Department groups the grid" "$(echo "$json" | jq -c .grid.groups)" '["Department"]'
	check_true "group header rows are drawn" '.grid.groupHeaderRows > 0' "$json"
	shot grouping-grouped

	local group y
	group=$(ext group-row '{"index":0}')
	rows=$(state '.grid.rowCount')
	y=$(echo "$group" | jq -r '.bounds.y + .bounds.height / 2')
	tap_at 40 "$y"; settle
	check "tapping a group header collapses it" "$(ext group-row '{"index":0}' | jq -r .isExpanded)" "false"
	check "collapsing hides its rows" "$(state '.grid.rowCount')" "$((rows - $(echo "$group" | jq -r .itemCount)))"
	shot grouping-collapsed-one
	tap_at 40 "$y"; settle
	check "tapping it again expands it" "$(ext group-row '{"index":0}' | jq -r .isExpanded)" "true"

	tap_button "Collapse All"
	check_true "Collapse All collapses every group" '.grid.collapsedGroups == .grid.groupHeaderRows and .grid.rowCount == .grid.groupHeaderRows'
	shot grouping-collapsed-all
	tap_button "Expand All"
	check_true "Expand All expands every group" '.grid.collapsedGroups == 0'

	tap_button "Group by Both"
	check "Group by Both nests two levels" "$(state '.grid.groups | join(",")')" "Department,Level"
	shot grouping-two-levels
	tap_button "Clear Groups"
	check "Clear Groups ungroups" "$(state '.grid.groups | length')" "0"

	local allow
	allow=$(state '.grid.allowFiltering')
	tap_button "Toggle Filtering"
	check "Toggle Filtering flips AllowFiltering" "$(state '.grid.allowFiltering')" "$([ "$allow" = true ] && echo false || echo true)"
	tap_button "Toggle Filtering"
}

test_large_data_page() {
	section "Large Data: 100K rows"
	navigate //LargeDataPage
	tap_button "Load 100K"
	wait_until "Load 100K fills the grid" '.grid.rowCount == 100000' 30
	extm scroll '{"row":99990}' >/dev/null; settle 1
	check_true "scrolls to the last rows" '.grid.scroll.vertical > 3000000'
	check "the last row is drawable" "$(ext cell '{"row":99999,"column":0}' | jq -r .onScreen)" "true"
	shot large-data-end
	local p
	p=$(cell_point 99995 1)
	tap_at $p; settle
	check "tapping near the end selects that row" "$(state '.grid.selection.rows | tostring')" "[99995]"

	# 100K rows are above the grid's BackgroundRebuildThreshold, so the sort runs on a worker (#254): the old order
	# stays on screen until the new view is swapped in. Wait for the swap before reading rows.
	extm scroll '{"row":0}' >/dev/null; settle
	p=$(header_point Value center)
	tap_at $p
	wait_until "a header tap sorts 100K rows in the background" \
		'.grid.isViewUpdatePending == false and (.grid.sort | map("\(.header) \(.direction)") | join(",")) == "Value Ascending"' 30
	extm scroll '{"row":0}' >/dev/null; settle
	check "100K rows are in ascending value order" "$(column_numbers Value 20 | is_sorted asc)" "true"
	check "the selection followed its item into the new order" "$(state '.grid.selection.rows | length')" "1"
	shot large-data-sorted
}

test_theming_page() {
	section "Theming: built-in themes and custom styles"
	navigate //ThemingPage
	local name bg light dark
	tap_button "Light"
	light=$(state '.grid.colors.background')
	check "Light theme" "$(state '.grid.theme')" "Light"
	if [ "$(luminance "$light")" -gt 128 ]; then pass "Light background is light ($light)"; else fail "Light background is light" "$light"; fi
	shot theme-light
	tap_button "Dark"
	dark=$(state '.grid.colors.background')
	check "Dark theme" "$(state '.grid.theme')" "Dark"
	if [ "$(luminance "$dark")" -lt 128 ]; then pass "Dark background is dark ($dark)"; else fail "Dark background is dark" "$dark"; fi
	shot theme-dark
	local previous=$dark
	for name in "High Contrast" "Ocean Blue" "Forest Green" "Sunset"; do
		tap_button "$name"
		bg=$(state '.grid.colors.background')
		if [ "$bg" != "$previous" ]; then pass "$name applies (background $bg)"; else fail "$name applies" "background still $bg"; fi
		previous=$bg
		shot "theme-$(echo "$name" | tr 'A-Z ' 'a-z-')"
	done

	section "Theming: the app's light/dark theme"
	# Only a grid that has no explicit Theme or GridStyle follows the system (FollowSystemTheme).
	local route found=""
	for route in CustomFontsPage MainPage GroupingPage LargeDataPage; do
		navigate "//$route"
		[ "$(state '.grid.followSystemTheme')" = "true" ] && { found=$route; break; }
	done
	if [ -z "$found" ]; then
		skip "a grid follows the app's light/dark theme" "every sample grid sets a Theme or GridStyle"
	elif ! dfm theme set dark --scope app >/dev/null || ! settle 1 || [ "$(df theme get | jq -r .effectiveTheme)" != dark ]; then
		# On the iOS Simulator DevFlow changes the simulator's appearance instead, which the running app did not pick up.
		skip "a grid follows the app's light/dark theme" "DevFlow could not switch the app's theme on $PLATFORM"
		dfm theme set system --scope app >/dev/null
	else
		bg=$(state '.grid.colors.background')
		if [ "$(luminance "$bg")" -lt 128 ]; then pass "$found: the grid turns dark with the app ($bg)"; else fail "$found: the grid turns dark with the app" "$bg"; fi
		shot app-theme-dark
		dfm theme set light --scope app >/dev/null; settle 1
		bg=$(state '.grid.colors.background')
		if [ "$(luminance "$bg")" -gt 128 ]; then pass "$found: and light again ($bg)"; else fail "$found: and light again" "$bg"; fi
		dfm theme set system --scope app >/dev/null
	fi
}

test_mvvm_page() {
	section "MVVM Actions: commands, selection binding, action buttons"
	navigate //MvvmActionsPage
	tap_cell 2 Name
	check "a tap selects the row" "$(state '.grid.selection.rows | tostring')" "[2]"
	tap_button "Clear"
	check "Clear (SelectedItem = null from the view model) clears the selection" "$(state '.grid.selection.count')" "0"
	shot mvvm

	local cell
	cell=$(ext cell '{"row":1,"column":"Actions"}')
	if [ "$(echo "$cell" | jq -r .onScreen)" != true ]; then
		extm scroll '{"column":"Actions"}' >/dev/null; settle
		cell=$(ext cell '{"row":1,"column":"Actions"}')
	fi
	tap_at $(echo "$cell" | jq -r '.bounds | "\(.x + .width / 4) \(.y + .height / 2)"'); settle 1
	check "the Details action button opens the details page" "$(ext page '{}' | jq -r .type)" "EmployeeDetailsPage"
	shot mvvm-details
	tap_button "Close"
	settle 1
	check "Close returns to the grid" "$(ext page '{}' | jq -r .type)" "MvvmActionsPage"

	# Last, because it can leave the app unresponsive: setting the two-way bound SelectedItem from the view model
	# loops between the grid and the view model (#267).
	tap_button "Select first"
	if app_alive; then
		check "Select first (SelectedItem from the view model) selects row 0" "$(state '.grid.selection.selectedIndex')" "0"
	else
		fail "Select first (SelectedItem from the view model) selects row 0" "the app stopped responding (UI thread busy)"
	fi
}

test_custom_fonts_page() {
	section "Custom Fonts: two grids"
	navigate //CustomFontsPage
	local json
	json=$(ext state '{}')
	check "two grids on the page" "$(echo "$json" | jq -r '.grids | length')" "2"
	check_true "the Japanese grid has rows" '.grid.rowCount > 0' "$json"
	check_true "the icon grid has rows" '.grid.rowCount > 0' "$(ext state '{"grid":1}')"
	shot custom-fonts
}

test_navigation
test_main_page
test_grouping_page
test_large_data_page
test_theming_page
test_custom_fonts_page
test_mvvm_page

printf '\n%d passed, %d failed, %d skipped. Screenshots: %s\n' "$PASSED" "$FAILED" "$SKIPPED" "$OUT"
if [ "$FAILED" -gt 0 ]; then
	printf 'Failures:%s\n' "$FAILURES"
	exit 1
fi
exit 0
}
