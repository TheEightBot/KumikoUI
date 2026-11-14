using System.Collections;
using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Maui;
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Maui.DevFlow.Agent.Core;
using HttpRequest = Microsoft.Maui.DevFlow.Agent.Core.HttpRequest;
using HttpResponse = Microsoft.Maui.DevFlow.Agent.Core.HttpResponse;

namespace SampleApp.Maui;

/// <summary>
/// Debug-only .NET MAUI DevFlow wiring for the sample (#262): the DevFlow agent plus a <c>com.kumikoui.sample</c>
/// agent extension. KumikoUI draws the grid on one canvas, so DevFlow's visual tree stops at the
/// <see cref="KumikoDataGrid"/>; the extension reports what the canvas holds (rows, columns, sort, filters, groups,
/// selection, current cell, edit state, scroll offsets, summaries, colors) and where cells and headers are drawn,
/// so a script can tap them with <c>maui devflow ui tap --x --y</c> and check the result. Only the grid's public
/// API is used.
/// </summary>
/// <remarks>
/// Call the tools with <c>maui devflow extensions call com.kumikoui.sample &lt;tool&gt; '{"grid":0,...}'</c>.
/// Every tool takes an optional <c>grid</c>: the index of the grid on the current page (in visual tree order) or its
/// <c>AutomationId</c>. Coordinates are device-independent units relative to the grid's top-left corner, which is
/// what <c>ui tap --x --y</c> expects. This file is compiled into Debug builds only (SampleApp.Maui.csproj).
/// </remarks>
public static class KumikoDevFlowExtension
{
	/// <summary>The extension namespace the tools are registered under.</summary>
	public const string Namespace = "com.kumikoui.sample";

	/// <summary>Adds the DevFlow agent with the KumikoUI sample extension.</summary>
	public static MauiAppBuilder AddKumikoDevFlow(this MauiAppBuilder builder) =>
		builder.AddMauiDevFlowAgent(options =>
		{
			// Taps and drags at a point inside the grid (ui tap --x --y, ui gesture drag) need synthesized touches
			// on iOS and Mac Catalyst: the grid is one canvas with no child elements to target.
			options.EnableSyntheticTouch = true;

			var extension = options.RegisterExtension(
				Namespace,
				"KumikoUI sample: grid state and cell geometry for canvas-drawn KumikoDataGrid instances",
				"1.0.0",
				["page", "state", "cell", "header", "hit-test", "rows", "find", "group-row", "popup", "scroll", "edit"]);

			var readOnly = new ExtensionToolAnnotations { ReadOnly = true, Idempotent = true, Category = "kumikoui" };

			extension.MapTool("page",
				"The page on screen (the top modal page, or Shell's current page): title, type, Shell route and how many grids it has. Works on pages without a grid.",
				"GET", "page", async _ => HttpResponse.Json(await MainThread.InvokeOnMainThreadAsync(() =>
				{
					var page = CurrentPage();
					return new { title = page?.Title, type = page?.GetType().Name, route = Route(), grids = FindGrids(page).Count };
				})), annotations: readOnly);

			extension.MapTool("state",
				"Current page and the state of one grid: rows, columns, sort, filters, groups, selection, current cell, edit, scroll, summaries and colors. Params: grid.",
				"GET", "state", request => OnMainThread(request, ReadState), annotations: readOnly);

			extension.MapTool("cell",
				"Bounds (grid-relative DIPs) and display text of a cell. Params: grid, row (flat view index), column (index, header or property name).",
				"GET", "cell", request => OnMainThread(request, ReadCell), annotations: readOnly);

			extension.MapTool("header",
				"Bounds (grid-relative DIPs) of a column header. Params: grid, column (index, header or property name).",
				"GET", "header", request => OnMainThread(request, ReadHeader), annotations: readOnly);

			extension.MapTool("hit-test",
				"What Core's hit tester finds at a grid-relative point: region, row and column. Params: grid, x, y.",
				"GET", "hit-test", request => OnMainThread(request, HitTest), annotations: readOnly);

			extension.MapTool("rows",
				"Display texts of a column for a range of rows in the flat view. Params: grid, column, start (default 0), count (default 10).",
				"GET", "rows", request => OnMainThread(request, ReadRows), annotations: readOnly);

			extension.MapTool("find",
				"The first row whose display text in a column equals a value (rows move when a sorted column is edited). Params: grid, column, text.",
				"GET", "find", request => OnMainThread(request, FindRow), annotations: readOnly);

			extension.MapTool("group-row",
				"The n-th group header row: its row index, bounds, text, item count and expanded state. Params: grid, index (default 0).",
				"GET", "group-row", request => OnMainThread(request, ReadGroupRow), annotations: readOnly);

			extension.MapTool("scroll",
				"Scrolls the grid through its public API. Params: grid, and any of row, column (index, header or property name), vertical, horizontal (DIPs).",
				"POST", "scroll", request => OnMainThread(request, Scroll),
				annotations: new ExtensionToolAnnotations { Idempotent = true, Category = "kumikoui" });

			extension.MapTool("popup",
				"The grid's open drawn popup (filter panel, dropdown, calendar), if any: its type and bounds; for the filter " +
				"panel also the bounds of its parts (sort rows, search, select/deselect all, OK, cancel, clear), the first list " +
				"items and the checked values. Params: grid.",
				"GET", "popup", request => OnMainThread(request, ReadPopup), annotations: readOnly);

			extension.MapTool("edit",
				"Drives the open cell editor, standing in for the keyboard DevFlow cannot send to a canvas. Params: grid, action " +
				"(type: replace the editor text with 'text'; set: set a date, number or list editor to 'value'; commit; cancel).",
				"POST", "edit", request => OnMainThread(request, Edit),
				annotations: new ExtensionToolAnnotations { Category = "kumikoui" });
		});

	// ── Tools ──

	private static object ReadState(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var source = grid.DataSource;
		var style = grid.GridStyle;
		var selection = grid.Selection;
		var edit = grid.EditSession;
		var current = selection.CurrentCell;

		int groupHeaders = 0, collapsed = 0;
		for (int row = 0; row < source.RowCount; row++)
		{
			if (source.GetGroupHeaderInfo(row) is not { } info) continue;
			groupHeaders++;
			if (!info.IsExpanded) collapsed++;
		}

		return new Dictionary<string, object?>
		{
			["page"] = DescribePage(page),
			["grids"] = grids.Select((g, i) => new { index = i, automationId = g.AutomationId, description = g.GridDescription }).ToArray(),
			["grid"] = new Dictionary<string, object?>
			{
				["index"] = IndexOf(grids, grid),
				["automationId"] = grid.AutomationId,
				["width"] = grid.Width,
				["height"] = grid.Height,
				["itemCount"] = grid.ItemsSource is ICollection items ? items.Count : (int?)null,
				["rowCount"] = source.RowCount,
				// A background rebuild (#254) is under way: rows still show the previous view until it is swapped in.
				["isViewUpdatePending"] = source.IsViewUpdatePending,
				["backgroundRebuildThreshold"] = grid.BackgroundRebuildThreshold,
				["frozenRowCount"] = source.EffectiveFrozenRowCount,
				["headerHeight"] = style.HeaderHeight,
				["rowHeight"] = style.RowHeight,
				["isReadOnly"] = grid.IsReadOnly,
				["allowSorting"] = grid.AllowSorting,
				["allowFiltering"] = grid.AllowFiltering,
				["theme"] = grid.Theme.ToString(),
				["followSystemTheme"] = grid.FollowSystemTheme,
				["colors"] = new
				{
					background = Hex(style.BackgroundColor),
					header = Hex(style.HeaderBackgroundColor),
					text = Hex(style.CellTextColor),
				},
				["columns"] = source.Columns.Select((c, i) => new
				{
					index = i,
					displayIndex = source.ToDisplayIndex(i),
					header = c.Header,
					propertyName = c.PropertyName,
					type = c.ColumnType.ToString(),
					width = c.Width,
					visible = c.IsVisible,
					frozen = c.IsFrozen ? "left" : c.IsFrozenRight ? "right" : "none",
					sort = c.SortDirection.ToString(),
					sortOrder = c.SortOrder,
					filtered = c.ActiveFilter?.IsActive == true,
				}).ToArray(),
				["sort"] = source.ActiveSortColumns.Select(c => new { header = c.Header, direction = c.SortDirection.ToString() }).ToArray(),
				["filters"] = source.ActiveFilters.Select(f => f.Column.Header).ToArray(),
				["groups"] = source.GroupDescriptions.Select(g => g.PropertyName).ToArray(),
				["groupHeaderRows"] = groupHeaders,
				["collapsedGroups"] = collapsed,
				["selection"] = new
				{
					mode = grid.GridSelectionMode.ToString(),
					count = selection.SelectedRows.Count,
					rows = selection.SelectedRows.Order().Take(50).ToArray(),
					selectedIndex = grid.SelectedIndex,
				},
				["currentCell"] = CellInfo(source, current),
				["edit"] = new
				{
					isEditing = edit.IsEditing,
					cell = edit.IsEditing ? CellInfo(source, edit.EditCell) : null,
					editor = edit.ActiveEditor?.GetType().Name,
				},
				["scroll"] = new { vertical = grid.VerticalOffset, horizontal = grid.HorizontalOffset },
				["popupOpen"] = grid.InputController.ActivePopup != null,
				["summaries"] = new
				{
					top = source.ComputedTopSummaries.Select(DescribeSummary).ToArray(),
					bottom = source.ComputedBottomSummaries.Select(DescribeSummary).ToArray(),
				},
			},
		};
	}

	private static object ReadCell(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var source = grid.DataSource;
		int row = args.Int("row") ?? throw new ArgumentException("row is required");
		int column = ResolveColumn(source, args.Text("column"));
		if (row < 0 || row >= source.RowCount) throw new ArgumentException($"row {row} is outside 0..{source.RowCount - 1}");

		var scroll = Snapshot(grid);
		var style = grid.GridStyle;
		float x = KumikoScroller.GetColumnX(source.Columns, style, scroll, column);
		float y = KumikoScroller.GetRowY(source, style, scroll, row);
		var c = source.Columns[column];
		return new
		{
			row,
			column,
			header = c.Header,
			text = source.IsNonDataRow(row) ? null : source.GetCellDisplayText(row, c),
			isGroupHeader = source.IsGroupHeaderRow(row),
			bounds = Rect(x, y, c.Width, style.RowHeight),
			center = new { x = x + c.Width / 2, y = y + style.RowHeight / 2 },
			// Inside the grid and not under a frozen pane, summary row or popup: the hit tester finds this cell there.
			onScreen = IsOnScreen(grid, x, y, c.Width, style.RowHeight)
				&& Hit(grid, x + c.Width / 2, y + style.RowHeight / 2) is { } hit
				&& hit.RowIndex == row && hit.ColumnIndex == column,
		};
	}

	private static object ReadHeader(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var source = grid.DataSource;
		int column = ResolveColumn(source, args.Text("column"));
		var style = grid.GridStyle;
		float x = KumikoScroller.GetColumnX(source.Columns, style, Snapshot(grid), column);
		float y = source.IsGroupingActive ? style.GroupPanelHeight : 0;
		var c = source.Columns[column];
		float middle = y + style.HeaderHeight / 2;
		// The filter funnel is drawn 10 DIPs wide, the cell padding inside the right edge, and left of the 12 DIP sort
		// arrow when the column is sorted: the point is its middle. The resize grip straddles the edge. Each point
		// comes with the region Core's hit tester reports for it, so a caller can tell a stale layout from a bug.
		float sortSpace = c.SortDirection != KumikoSortDirection.None ? 12 : 0;
		float filterX = x + c.Width - style.CellPadding - sortSpace - 5, gripX = x + c.Width - 2;
		return new
		{
			column,
			header = c.Header,
			bounds = Rect(x, y, c.Width, style.HeaderHeight),
			center = new { x = x + c.Width / 2, y = middle, region = Region(grid, x + c.Width / 2, middle) },
			filterIcon = new { x = filterX, y = middle, region = Region(grid, filterX, middle) },
			resizeGrip = new { x = gripX, y = middle, region = Region(grid, gripX, middle) },
			onScreen = IsOnScreen(grid, x, y, c.Width, style.HeaderHeight)
				&& Hit(grid, x + c.Width / 2, middle).ColumnIndex == column,
		};
	}

	private static object HitTest(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		float x = (float)(args.Double("x") ?? throw new ArgumentException("x is required"));
		float y = (float)(args.Double("y") ?? throw new ArgumentException("y is required"));
		var hit = Hit(grid, x, y);
		return new { region = hit.Region.ToString(), row = hit.RowIndex, column = hit.ColumnIndex, header = hit.Column?.Header };
	}

	/// <summary>What Core's hit tester finds at a grid-relative point, as the grid's own input would.</summary>
	private static KumikoHitTestResult Hit(KumikoDataGrid grid, float x, float y)
	{
		var source = grid.DataSource;
		return new KumikoHitTester { AllowFiltering = grid.AllowFiltering }
			.HitTest(x, y, Snapshot(grid), grid.GridStyle, source.Columns, source.RowCount, source);
	}

	private static string Region(KumikoDataGrid grid, float x, float y) => Hit(grid, x, y).Region.ToString();

	private static object ReadRows(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var source = grid.DataSource;
		var column = source.Columns[ResolveColumn(source, args.Text("column"))];
		int start = Math.Max(0, args.Int("start") ?? 0);
		int end = Math.Min(source.RowCount, start + Math.Max(0, args.Int("count") ?? 10));
		var rows = new List<object>();
		for (int row = start; row < end; row++)
		{
			var group = source.GetGroupHeaderInfo(row);
			rows.Add(new
			{
				row,
				text = source.IsNonDataRow(row) ? null : source.GetCellDisplayText(row, column),
				group = group?.DisplayText,
			});
		}
		return new { column = column.Header, rowCount = source.RowCount, rows };
	}

	private static object FindRow(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var source = grid.DataSource;
		var column = source.Columns[ResolveColumn(source, args.Text("column"))];
		string text = args.Text("text") ?? throw new ArgumentException("text is required");
		for (int row = 0; row < source.RowCount; row++)
			if (!source.IsNonDataRow(row) && source.GetCellDisplayText(row, column) == text)
				return new { row };
		return new { row = -1 };
	}

	private static object ReadGroupRow(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var source = grid.DataSource;
		int wanted = args.Int("index") ?? 0;
		int seen = 0;
		for (int row = 0; row < source.RowCount; row++)
		{
			if (source.GetGroupHeaderInfo(row) is not { } info) continue;
			if (seen++ != wanted) continue;

			var style = grid.GridStyle;
			float y = KumikoScroller.GetRowY(source, style, Snapshot(grid), row);
			float width = (float)grid.Width;
			return new
			{
				row,
				level = info.Level,
				text = info.DisplayText,
				header = info.HeaderText,
				itemCount = info.ItemCount,
				isExpanded = info.IsExpanded,
				bounds = Rect(0, y, width, style.RowHeight),
				onScreen = IsOnScreen(grid, 0, y, width, style.RowHeight),
			};
		}
		throw new ArgumentException($"there is no group header row {wanted} (found {seen})");
	}

	private static object Scroll(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		bool scrolled = false;
		if (args.Int("row") is { } row) scrolled |= grid.ScrollToRow(row, KumikoScrollPosition.Start);
		if (args.Text("column") is { } column) scrolled |= grid.ScrollToColumn(ResolveColumn(grid.DataSource, column), KumikoScrollPosition.Start);
		if (args.Double("vertical") is { } vertical) { grid.VerticalOffset = vertical; scrolled = true; }
		if (args.Double("horizontal") is { } horizontal) { grid.HorizontalOffset = horizontal; scrolled = true; }
		return new { scrolled, vertical = grid.VerticalOffset, horizontal = grid.HorizontalOffset };
	}

	private static object ReadPopup(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var popup = grid.InputController.ActivePopup;
		if (popup == null) return new { open = false };

		var b = popup.Bounds;
		if (popup is not DrawnFilterPopup filter)
			return new { open = true, type = popup.GetType().Name, bounds = Rect(b.X, b.Y, b.Width, b.Height) };

		// Mirrors DrawnFilterPopup's private layout (padding 6, separator margin 4, sort row 28, search 30,
		// button row 32, list items 24 up to 250): sample test code, kept next to the popup it measures.
		const float padding = 6, margin = 4, sortRow = 28, search = 30, buttonRow = 32, item = 24;
		float x0 = b.X + padding, width = b.Width - padding * 2, half = width / 2;
		float top = b.Y + padding;
		float searchY = top + sortRow + margin + 1 + margin;
		float buttonsY = searchY + search + margin;
		float listY = buttonsY + buttonRow;
		float listHeight = Math.Min(filter.VisibleValues.Count * item, 250);
		float okY = listY + listHeight + margin + 1 + margin + 3;
		float buttonWidth = (width - 8) / 3;
		return new
		{
			open = true,
			type = nameof(DrawnFilterPopup),
			bounds = Rect(b.X, b.Y, b.Width, b.Height),
			parts = new
			{
				sortAscending = Rect(x0, top, half, sortRow),
				sortDescending = Rect(x0 + half, top, half, sortRow),
				search = Rect(x0, searchY + 2, width, search - 4),
				selectAll = Rect(x0, buttonsY, half, buttonRow),
				deselectAll = Rect(x0 + half, buttonsY, half, buttonRow),
				list = Rect(x0, listY, width, listHeight),
				ok = Rect(x0, okY, buttonWidth, buttonRow - 6),
				cancel = Rect(x0 + buttonWidth + 4, okY, buttonWidth, buttonRow - 6),
				clear = Rect(x0 + (buttonWidth + 4) * 2, okY, buttonWidth, buttonRow - 6),
			},
			items = filter.VisibleValues.Take((int)(listHeight / item)).Select((value, i) => new
			{
				value,
				bounds = Rect(x0, listY + i * item - filter.ListScrollOffset, width, item),
			}).ToArray(),
			valueCount = filter.ValueCount,
			checkedValues = filter.CheckedValues,
		};
	}

	private static object Edit(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args)
	{
		var edit = grid.EditSession;
		if (!edit.IsEditing) throw new InvalidOperationException("no cell is being edited");
		string action = args.Text("action") ?? throw new ArgumentException("action is required");
		var editor = edit.ActiveEditor;

		switch (action)
		{
			case "type":
				// The calls a platform text-input adapter makes: select everything, then insert.
				var input = edit.ActiveTextInput
					?? throw new InvalidOperationException($"{editor?.GetType().Name} takes no text input");
				input.SetSelection(0, input.Text.Length);
				input.InsertText(args.Text("text") ?? string.Empty);
				break;
			case "set":
				string value = args.Text("value") ?? throw new ArgumentException("value is required");
				switch (editor)
				{
					case DrawnDatePicker date:
						date.SelectedDate = DateTime.Parse(value, CultureInfo.InvariantCulture);
						break;
					case DrawnNumericUpDown numeric:
						numeric.Value = double.Parse(value, CultureInfo.InvariantCulture);
						break;
					case DrawnComboBox combo:
						combo.SelectedIndex = int.Parse(value, CultureInfo.InvariantCulture);
						break;
					case DrawnScrollPicker picker:
						picker.SelectedIndex = int.Parse(value, CultureInfo.InvariantCulture);
						break;
					default:
						throw new InvalidOperationException($"set does not support {editor?.GetType().Name}");
				}
				break;
			case "commit":
				return new { committed = edit.CommitEdit(grid.DataSource), isEditing = edit.IsEditing };
			case "cancel":
				edit.CancelEdit();
				break;
			default:
				throw new ArgumentException($"unknown action '{action}'");
		}
		return new { editor = editor?.GetType().Name, text = edit.ActiveTextInput?.Text, isEditing = edit.IsEditing };
	}

	// ── Plumbing ──

	private delegate object Tool(KumikoDataGrid grid, Page? page, IReadOnlyList<KumikoDataGrid> grids, Args args);

	/// <summary>Runs a tool on the UI thread (the grid and its source are UI-thread objects) and wraps the result.</summary>
	private static async Task<HttpResponse> OnMainThread(HttpRequest request, Tool tool)
	{
		try
		{
			var args = Args.From(request);
			var result = await MainThread.InvokeOnMainThreadAsync(() =>
			{
				var page = CurrentPage();
				var grids = FindGrids(page);
				var grid = SelectGrid(grids, args.Text("grid"));
				return tool(grid, page, grids, args);
			});
			return HttpResponse.Json(result);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			return HttpResponse.Json(new { error = ex.Message }, 400);
		}
	}

	private static Page? CurrentPage()
	{
		var root = Application.Current?.Windows.FirstOrDefault()?.Page;
		if (root?.Navigation.ModalStack.LastOrDefault() is { } modal) return Unwrap(modal);
		if (root is Shell shell) return shell.CurrentPage;
		return Unwrap(root);

		static Page? Unwrap(Page? page) => page switch
		{
			Shell s => s.CurrentPage,
			NavigationPage n => n.CurrentPage,
			_ => page,
		};
	}

	private static List<KumikoDataGrid> FindGrids(Page? page)
	{
		var grids = new List<KumikoDataGrid>();
		if (page is IVisualTreeElement root) Collect(root, grids);
		return grids;

		static void Collect(IVisualTreeElement element, List<KumikoDataGrid> found)
		{
			if (element is KumikoDataGrid grid)
			{
				found.Add(grid);
				return;
			}
			foreach (var child in element.GetVisualChildren())
				Collect(child, found);
		}
	}

	private static KumikoDataGrid SelectGrid(List<KumikoDataGrid> grids, string? key)
	{
		if (grids.Count == 0) throw new InvalidOperationException("the current page has no KumikoDataGrid");
		if (string.IsNullOrEmpty(key)) return grids[0];
		if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
			return index >= 0 && index < grids.Count
				? grids[index]
				: throw new ArgumentException($"grid {index} is outside 0..{grids.Count - 1}");
		return grids.FirstOrDefault(g => g.AutomationId == key)
			?? throw new ArgumentException($"no grid has AutomationId '{key}'");
	}

	private static int IndexOf(IReadOnlyList<KumikoDataGrid> grids, KumikoDataGrid grid)
	{
		for (int i = 0; i < grids.Count; i++)
			if (ReferenceEquals(grids[i], grid)) return i;
		return -1;
	}

	/// <summary>Resolves a column by index, header or property name (case-insensitive).</summary>
	private static int ResolveColumn(KumikoSource source, string? key)
	{
		if (string.IsNullOrEmpty(key)) throw new ArgumentException("column is required");
		var columns = source.Columns;
		if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
			return index >= 0 && index < columns.Count
				? index
				: throw new ArgumentException($"column {index} is outside 0..{columns.Count - 1}");
		for (int i = 0; i < columns.Count; i++)
			if (string.Equals(columns[i].Header, key, StringComparison.OrdinalIgnoreCase)) return i;
		for (int i = 0; i < columns.Count; i++)
			if (string.Equals(columns[i].PropertyName, key, StringComparison.OrdinalIgnoreCase)) return i;
		throw new ArgumentException($"no column matches '{key}'");
	}

	/// <summary>
	/// A copy of the grid's scroll position for the Core geometry helpers: the grid keeps its own
	/// <see cref="ScrollState"/> private, and the public offsets and size are all the helpers read.
	/// </summary>
	private static ScrollState Snapshot(KumikoDataGrid grid) => new()
	{
		ViewportWidth = (float)grid.Width,
		ViewportHeight = (float)grid.Height,
		OffsetX = (float)grid.HorizontalOffset,
		OffsetY = (float)grid.VerticalOffset,
	};

	private static bool IsOnScreen(KumikoDataGrid grid, float x, float y, float width, float height) =>
		float.IsFinite(x) && x >= 0 && y >= 0 && x + width <= grid.Width + 0.5 && y + height <= grid.Height + 0.5;

	private static object? CellInfo(KumikoSource source, CellPosition cell) =>
		!cell.IsValid ? null : new
		{
			row = cell.Row,
			column = cell.Column,
			header = cell.Column < source.Columns.Count ? source.Columns[cell.Column].Header : null,
			text = cell.Row < source.RowCount && cell.Column < source.Columns.Count && !source.IsNonDataRow(cell.Row)
				? source.GetCellDisplayText(cell.Row, source.Columns[cell.Column])
				: null,
		};

	private static object DescribePage(Page? page) => new
	{
		title = page?.Title,
		type = page?.GetType().Name,
		route = Route(),
	};

	private static string? Route() => Shell.Current?.CurrentState?.Location?.OriginalString;

	private static object DescribeSummary(ComputedSummaryRow row) => new { name = row.Name, title = row.Title, values = row.Values };

	private static object Rect(float x, float y, float width, float height) => new { x, y, width, height };

	private static string Hex(KumikoColor color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

	/// <summary>Tool parameters from the query string (GET) or a JSON object body (POST).</summary>
	private sealed class Args
	{
		private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

		public static Args From(HttpRequest request)
		{
			var args = new Args();
			foreach (var (key, value) in request.QueryParams)
				args._values[key] = value;
			if (!string.IsNullOrWhiteSpace(request.Body))
			{
				using var document = System.Text.Json.JsonDocument.Parse(request.Body);
				if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
					foreach (var property in document.RootElement.EnumerateObject())
						args._values[property.Name] = property.Value.ValueKind == System.Text.Json.JsonValueKind.String
							? property.Value.GetString() ?? string.Empty
							: property.Value.GetRawText();
			}
			return args;
		}

		public string? Text(string name) => _values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

		public int? Int(string name) =>
			Text(name) is { } text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;

		public double? Double(string name) =>
			Text(name) is { } text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
	}
}
