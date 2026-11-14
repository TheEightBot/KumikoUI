using System;
using System.Collections.Generic;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SampleApp.Shared;

namespace SampleApp.Uno;

/// <summary>
/// Events and selection (← SampleApp.Maui EventsPage): <c>CellTapped</c>, <c>HeaderTapped</c>,
/// <c>ContextMenuRequested</c>, <c>RowDoubleTapped</c>, <c>ValidationFailed</c> and <c>SelectionChanged</c> written
/// to a log, the selection modes, a <c>CellValidator</c>, and saving and restoring the layout with
/// <c>KumikoLayoutState</c>.
/// </summary>
public sealed partial class EventsPage : Page
{
    private const string LayoutKey = "EventsPage.GridLayout";
    private const int MaxLogLines = 40;

    private static readonly KumikoSelectionMode[] Modes =
        { KumikoSelectionMode.Extended, KumikoSelectionMode.Single, KumikoSelectionMode.Multiple, KumikoSelectionMode.None };

    private readonly List<string> _log = new();
    private int _modeIndex;
    private bool _german;
    private bool _heavyScroll;

    public EventsPage()
    {
        this.InitializeComponent();
        kumiko.ItemsSource = ShowcaseData.CreateRows(200, n => $"Assets/Avatars/avatar-{n}.png");
        kumiko.CellValidator = ShowcaseData.Validate;

        kumiko.CellTapped += (_, e) => Log(ShowcaseData.Describe(e));
        kumiko.HeaderTapped += (_, e) => Log(ShowcaseData.Describe(e));
        kumiko.ContextMenuRequested += (_, e) => Log(ShowcaseData.Describe(e));
        kumiko.RowDoubleTapped += (_, e) => Log($"RowDoubleTapped: row {e.RowIndex}");
        kumiko.ValidationFailed += (_, e) => Log(ShowcaseData.Describe(e));
        kumiko.CellValueChanged += (_, e) => Log($"CellValueChanged: {e.Column.Header} = {e.NewValue}");
        kumiko.SelectionChanged += (_, e) =>
        {
            Log(ShowcaseData.Describe(e));
            selectionLabel.Text = $"Selected: {(kumiko.SelectedItem as ShowcaseRow)?.Name ?? "none"} ({kumiko.SelectedItems?.Count ?? 0} selected)";
        };
    }

    private void Log(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > MaxLogLines) _log.RemoveAt(_log.Count - 1);
        logBox.Text = string.Join(Environment.NewLine, _log);
    }

    private void OnToggleModeClicked(object sender, RoutedEventArgs e)
    {
        _modeIndex = (_modeIndex + 1) % Modes.Length;
        kumiko.GridSelectionMode = Modes[_modeIndex];
        modeToggle.Content = $"Selection: {Modes[_modeIndex]}";
    }

    private void OnSaveLayoutClicked(object sender, RoutedEventArgs e)
    {
        Windows.Storage.ApplicationData.Current.LocalSettings.Values[LayoutKey] = kumiko.GetLayoutState().ToJson();
        Log("Layout saved: column order, widths, sort, filters and grouping");
    }

    private void OnRestoreLayoutClicked(object sender, RoutedEventArgs e)
    {
        if (Windows.Storage.ApplicationData.Current.LocalSettings.Values[LayoutKey] is not string json)
        {
            Log("Nothing saved yet");
            return;
        }

        kumiko.ApplyLayoutState(KumikoLayoutState.FromJson(json));
        Log("Layout restored");
    }

    private void OnToggleLanguageClicked(object sender, RoutedEventArgs e)
    {
        _german = !_german;
        ShowcaseData.SetLanguage(kumiko.DataSource, _german);
        languageToggle.Content = _german ? "Language: Deutsch" : "Language: English";
    }

    private void OnToggleScrollClicked(object sender, RoutedEventArgs e)
    {
        _heavyScroll = !_heavyScroll;
        kumiko.ScrollSettings = _heavyScroll ? ShowcaseData.HeavyScrolling : new ScrollSettings();
        scrollToggle.Content = _heavyScroll ? "Scrolling: Heavy" : "Scrolling: Default";
    }

    private void OnClearLogClicked(object sender, RoutedEventArgs e)
    {
        _log.Clear();
        logBox.Text = string.Empty;
    }
}
