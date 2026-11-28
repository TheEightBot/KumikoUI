using System.Windows.Input;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// The drawn column chooser (#335): a panel that lists the columns with a check box to show or hide each, a grip to
/// reorder it within its pane, a search box, Show all and Reset. It opens with <see cref="ShowColumnChooser()"/> (or
/// <see cref="ShowColumnChooserCommand"/>), from the column menu's Columns... item, from a header right click or long
/// press (<see cref="ColumnChooserOnHeaderContext"/>) and from a shortcut (<see cref="ColumnChooserShortcut"/>). It is
/// drawn by Core, so it looks the same on every head and matches the MAUI grid; this file forwards to it.
/// </summary>
public partial class KumikoDataGrid
{
    private ICommand? _showColumnChooserCommand;

    /// <summary>Wires the chooser to the caret blink and soft keyboard of the filter popup; both are the grid's own.</summary>
    private void InitializeColumnChooser()
    {
        _inputController.ColumnChooserOpened += OnColumnChooserOpened;
        _inputController.ColumnChooserClosed += OnColumnChooserClosed;
        _dataSource.ColumnsChanged += OnDataSourceColumnsChanged;
    }

    /// <summary>Whether the column chooser is open.</summary>
    public bool IsColumnChooserOpen => _inputController.IsColumnChooserOpen;

    /// <summary>Raised when the column chooser opens.</summary>
    public event EventHandler? ColumnChooserOpened;

    /// <summary>Raised when the column chooser closes, however it closes (Escape, its close button, a tap outside it).</summary>
    public event EventHandler? ColumnChooserClosed;

    /// <summary>
    /// Raised when a column was shown, hidden, frozen or moved, by the user (the chooser, a header drag, the column menu)
    /// or in code, and once for a Reset; see <see cref="KumikoSource.ColumnsChanged"/>. Save the layout
    /// (<see cref="GetLayoutState"/>) from here to keep what the user chose.
    /// </summary>
    public event EventHandler<KumikoColumnsChangedEventArgs>? ColumnsChanged;

    /// <summary>
    /// Opens the column chooser at the top right of the grid's rows, below the header, or gives it the keyboard when it
    /// is open. Any other popup closes.
    /// </summary>
    public void ShowColumnChooser()
    {
        _inputController.ShowColumnChooser(_scroll, _selection, _style, _dataSource);
        Invalidate();
    }

    /// <summary>
    /// Opens the column chooser below <paramref name="anchor"/> (above it when there is no room below), for example the
    /// bounds of a "Columns" button laid over the grid.
    /// </summary>
    /// <param name="anchor">The rectangle to open at, in the grid's own coordinates (device-independent units from its top left).</param>
    public void ShowColumnChooser(Windows.Foundation.Rect anchor)
    {
        _inputController.ShowColumnChooser(_scroll, _selection, _style, _dataSource,
            new KumikoRect((float)anchor.X, (float)anchor.Y, (float)anchor.Width, (float)anchor.Height));
        Invalidate();
    }

    /// <summary>Closes the column chooser. Does nothing when it is not open.</summary>
    public void CloseColumnChooser()
    {
        _inputController.CloseColumnChooser();
        Invalidate();
    }

    /// <summary>A command that opens the column chooser (<see cref="ShowColumnChooser()"/>), for a toolbar button bound in XAML.</summary>
    public ICommand ShowColumnChooserCommand => _showColumnChooserCommand ??= new ShowChooserCommand(this);

    /// <summary>
    /// The key that opens the column chooser while the grid has the keyboard and no cell editor is open, or null (the
    /// default) for none; for example <c>new KumikoShortcut(KumikoKey.C, InputModifiers.Control | InputModifiers.Alt)</c>
    /// for Ctrl+Alt+C (Command+Option+C on a Mac). Off by default because Ctrl+Alt is AltGr on Windows, which types a
    /// character with C on some keyboard layouts. Plain property (not a dependency property).
    /// </summary>
    public KumikoShortcut? ColumnChooserShortcut
    {
        get => _inputController.ColumnChooserShortcut;
        set => _inputController.ColumnChooserShortcut = value;
    }

    // ── ColumnChooserOnHeaderContext ──

    /// <summary>Identifies the <see cref="ColumnChooserOnHeaderContext"/> dependency property.</summary>
    public static readonly DependencyProperty ColumnChooserOnHeaderContextProperty =
        DependencyProperty.Register(
            nameof(ColumnChooserOnHeaderContext), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) =>
                ((KumikoDataGrid)d)._inputController.ColumnChooserOnHeaderContext = (bool)e.NewValue));

    /// <summary>
    /// Whether a right click or a long press on a column header opens the column chooser at that header, when no
    /// <see cref="ContextMenuRequested"/> handler set <see cref="KumikoContextRequestedEventArgs.Handled"/>. With
    /// <see cref="IsColumnMenuEnabled"/> on, the column menu opens instead, and its Columns... item opens the chooser.
    /// Default: false.
    /// </summary>
    public bool ColumnChooserOnHeaderContext
    {
        get => (bool)GetValue(ColumnChooserOnHeaderContextProperty);
        set => SetValue(ColumnChooserOnHeaderContextProperty, value);
    }

    // ── Wiring ──

    /// <summary>The chooser's search box blinks its caret and takes the soft keyboard, as the filter popup's does.</summary>
    private void OnColumnChooserOpened(object? sender, EventArgs e)
    {
        OnFilterPopupOpened(sender, e);
        ColumnChooserOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnColumnChooserClosed(object? sender, EventArgs e)
    {
        OnFilterPopupClosed(sender, e);
        Invalidate();
        ColumnChooserClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDataSourceColumnsChanged(object? sender, KumikoColumnsChangedEventArgs e)
    {
        _inputController.OnColumnsChanged(_selection, _dataSource); // an edit on a column that was hidden ends
        Invalidate();
        ColumnsChanged?.Invoke(this, e);
    }

    /// <summary><see cref="ShowColumnChooserCommand"/>: always executable.</summary>
    private sealed class ShowChooserCommand(KumikoDataGrid grid) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => grid.ShowColumnChooser();
    }
}
