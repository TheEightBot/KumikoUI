using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KumikoUI.Core.Input;
using Microsoft.UI.Xaml.Controls;

namespace SampleApp.Uno;

/// <summary>Swipe row actions (#329, ← SampleApp.Maui SwipeActionsPage): leading and trailing drawn items bound to view model commands, with full swipe.</summary>
public sealed partial class SwipeActionsPage : Page
{
    private readonly SwipeActionsViewModel _viewModel = new();

    public SwipeActionsPage()
    {
        this.InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnSwipeEnded(object? sender, KumikoSwipeEndedEventArgs e)
    {
        if (e.InvokedItem is null && e.DataItem is SwipeMessage message)
            _viewModel.Status = $"Closed \"{message.Subject}\" without an action.";
    }
}

/// <summary>A message in the swipe actions demo.</summary>
public sealed class SwipeMessage : INotifyPropertyChanged
{
    private bool _isFlagged;

    public string Sender { get; init; } = "";

    public string Subject { get; init; } = "";

    public bool IsFlagged
    {
        get => _isFlagged;
        set
        {
            if (_isFlagged == value) return;
            _isFlagged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFlagged)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>The view model of the swipe actions demo: the messages and the commands the swipe items run.</summary>
public sealed class SwipeActionsViewModel : INotifyPropertyChanged
{
    private SwipeMessage? _selectedMessage;
    private string _status = "Swipe a row.";

    public SwipeActionsViewModel()
    {
        string[] senders = { "Ada", "Grace", "Linus", "Margaret", "Ken", "Barbara", "Dennis", "Frances" };
        string[] subjects = { "Quarterly report", "Lunch on Friday?", "Build is green", "Design review notes",
            "Invoice #4821", "Release checklist", "Welcome aboard", "Server maintenance" };
        for (int i = 0; i < 40; i++)
            Messages.Add(new SwipeMessage { Sender = senders[i % senders.Length], Subject = $"{subjects[i % subjects.Length]} ({i + 1})" });

        FlagCommand = new RelayCommand<SwipeMessage>(m =>
        {
            if (m is null) return;
            m.IsFlagged = !m.IsFlagged;
            Status = $"{(m.IsFlagged ? "Flagged" : "Unflagged")} \"{m.Subject}\".";
        });
        ArchiveCommand = new RelayCommand<SwipeMessage>(m => Remove(m, "Archived"));
        DeleteCommand = new RelayCommand<SwipeMessage>(m => Remove(m, "Deleted"));
    }

    public ObservableCollection<SwipeMessage> Messages { get; } = new();

    public ICommand FlagCommand { get; }

    public ICommand ArchiveCommand { get; }

    public ICommand DeleteCommand { get; }

    public SwipeMessage? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            if (ReferenceEquals(_selectedMessage, value)) return;
            _selectedMessage = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Remove(SwipeMessage? message, string verb)
    {
        if (message is null || !Messages.Remove(message)) return;
        Status = $"{verb} \"{message.Subject}\".";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
