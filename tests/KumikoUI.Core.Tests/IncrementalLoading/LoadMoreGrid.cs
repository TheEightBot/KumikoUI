using System.Collections.ObjectModel;
using System.ComponentModel;
using KumikoUI.Core.Data;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.IncrementalLoading;

/// <summary>An item of the incremental-loading tests.</summary>
internal sealed class LoadItem
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>
/// An <see cref="IKumikoIncrementalSource"/> whose batches the test completes or fails by hand. A batch appends to the
/// bound collection one <c>Add</c> at a time, as most app collections do.
/// </summary>
internal sealed class FakeIncrementalSource : IKumikoIncrementalSource, INotifyPropertyChanged
{
    private readonly ObservableCollection<LoadItem> _items;
    private bool _hasMoreItems = true;

    public FakeIncrementalSource(ObservableCollection<LoadItem> items) => _items = items;

    public sealed record Call(int Count, CancellationToken Token, TaskCompletionSource<int> Completion);

    public List<Call> Calls { get; } = new();

    public Call Last => Calls[^1];

    /// <summary>Replaces the pending-task behavior: each request is answered by this function.</summary>
    public Func<int, CancellationToken, Task<int>>? Respond { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasMoreItems
    {
        get => _hasMoreItems;
        set => _hasMoreItems = value; // silent: the loader must not poll its way out of Completed
    }

    public void NotifyHasMoreItems(bool value)
    {
        _hasMoreItems = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMoreItems)));
    }

    public Task<int> LoadMoreItemsAsync(int count, CancellationToken cancellationToken)
    {
        if (Respond is { } respond)
        {
            Calls.Add(new Call(count, cancellationToken, new TaskCompletionSource<int>()));
            return respond(count, cancellationToken);
        }
        var completion = new TaskCompletionSource<int>();
        Calls.Add(new Call(count, cancellationToken, completion));
        return completion.Task;
    }

    /// <summary>Appends <paramref name="count"/> items, then completes the last request with that count.</summary>
    public void Complete(int count, bool? hasMore = null, Func<int, LoadItem>? make = null)
    {
        Append(count, make);
        if (hasMore is { } more) _hasMoreItems = more;
        Last.Completion.SetResult(count);
    }

    public void Append(int count, Func<int, LoadItem>? make = null)
    {
        for (int i = 0; i < count; i++)
        {
            int n = _items.Count;
            _items.Add(make?.Invoke(n) ?? new LoadItem { Name = $"R{n:D3}", Value = n });
        }
    }

    public void Fail(Exception error) => Last.Completion.SetException(error);
}

/// <summary>
/// A grid (source, renderer, input controller) bound to an observable collection with a
/// <see cref="FakeIncrementalSource"/>. Work the loader schedules after a frame is queued and run by <see cref="Pump"/>.
/// </summary>
/// <remarks>
/// Geometry: 300 x 400 viewport, header 40 px, rows 36 px, so 10 rows fit the scrolling area and the default
/// threshold is 10 rows. Columns: Name 150, Value 150.
/// </remarks>
internal sealed class LoadMoreGrid
{
    public const float Width = 300f;
    public const float Height = 400f;

    public ObservableCollection<LoadItem> Items { get; } = new();
    public FakeIncrementalSource Fake { get; }
    public KumikoSource Source { get; } = new();
    public KumikoStyle Style { get; } = new();
    public ScrollState Scroll { get; } = new() { ViewportWidth = Width, ViewportHeight = Height };
    public KumikoSelectionModel Selection { get; }
    public KumikoRenderer Renderer { get; } = new();
    public KumikoInputController Input { get; } = new();
    public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };
    public KumikoColumn Value { get; } = new() { Header = "Value", PropertyName = "Value", Width = 150, ColumnType = KumikoColumnType.Numeric };
    public Queue<(SendOrPostCallback Callback, object? State)> Posted { get; } = new();
    public long ClockMs { get; set; }

    public List<string> Events { get; } = new();
    public List<KumikoLoadMoreRequestedEventArgs> Requests { get; } = new();
    public List<KumikoLoadMoreCompletedEventArgs> Completions { get; } = new();

    public LoadMoreGrid(int rows = 0, bool attachSource = true, Action<LoadMoreGrid>? configure = null)
    {
        Fake = new FakeIncrementalSource(Items);
        Fake.Append(rows);
        Selection = new KumikoSelectionModel(Source);
        Source.SetColumns(new[] { Name, Value });
        Source.SetItems(Items);
        Loader.Scheduler = (callback, state) => Posted.Enqueue((callback, state));
        Loader.LoadMoreRequested += (_, e) => { Requests.Add(e); Events.Add($"Requested:{e.Trigger}"); };
        Loader.LoadMoreCompleted += (_, e) => { Completions.Add(e); Events.Add($"Completed:{e.State}"); };
        Loader.StateChanged += (_, _) => Events.Add($"State:{Loader.State}");
        Renderer.InputController = Input;
        Input.AnimationsEnabled = true; // as the hosts do, unless the platform asks for reduced motion
        Renderer.LoadMoreClock = () => ClockMs;
        configure?.Invoke(this);
        if (attachSource) Source.IncrementalSource = Fake;
        Events.Clear();
    }

    public KumikoIncrementalLoader Loader => Source.IncrementalLoading;

    public RecordingDrawingContext Render()
    {
        var ctx = new RecordingDrawingContext();
        Renderer.Render(ctx, Source, Scroll, Selection, Style);
        return ctx;
    }

    /// <summary>Runs the work the loader scheduled after the frames drawn so far.</summary>
    public int Pump()
    {
        int ran = 0;
        while (Posted.TryDequeue(out var work))
        {
            work.Callback(work.State);
            ran++;
        }
        return ran;
    }

    /// <summary>A frame, then the work it scheduled.</summary>
    public RecordingDrawingContext Frame()
    {
        var ctx = Render();
        Pump();
        return ctx;
    }

    /// <summary>Scrolls to the very bottom (after a render, so the content height is known).</summary>
    public void ScrollToEnd()
    {
        Render();
        Scroll.OffsetY = Scroll.MaxOffsetY;
    }

    /// <summary>The top of the load-more row in viewport coordinates.</summary>
    public float LoadMoreRowTop => Style.HeaderHeight + Source.RowCount * Style.RowHeight - Scroll.OffsetY;

    public KumikoKeyEventArgs Key(KumikoKey key, InputModifiers modifiers = InputModifiers.None)
    {
        var e = new KumikoKeyEventArgs { Key = key, Modifiers = modifiers };
        Input.HandleKey(e, Scroll, Selection, Style, Source);
        return e;
    }

    public void Pointer(InputAction action, float x, float y, PointerButton button = PointerButton.Primary)
    {
        var e = new KumikoPointerEventArgs { X = x, Y = y, Action = action, Button = button, ClickCount = 1 };
        Input.HandlePointer(e, Scroll, Selection, Style, Source);
    }

    public void Tap(float x, float y)
    {
        Pointer(InputAction.Pressed, x, y);
        Pointer(InputAction.Released, x, y);
    }

    public KumikoHitTestResult Hit(float x, float y) =>
        Input.HitTester.HitTest(x, y, Scroll, Style, Source.Columns, Source.RowCount, Source);
}
