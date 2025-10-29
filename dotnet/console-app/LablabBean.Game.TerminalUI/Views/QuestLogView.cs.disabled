using Terminal.Gui;
using Arch.Core;
using LablabBean.Plugins.Quest.Services;

namespace LablabBean.Game.TerminalUI.Views;

/// <summary>
/// Quest log UI screen - displays active and completed quests
/// </summary>
public class QuestLogView : Window
{
    private readonly QuestService _questService;
    private readonly Entity _playerEntity;
    private ListView? _activeQuestsList;
    private TextView? _questDetailsView;

    public QuestLogView(QuestService questService, Entity playerEntity) : base()
    {
        _questService = questService;
        _playerEntity = playerEntity;

        InitializeUI();
        RefreshQuests();
    }

    private void InitializeUI()
    {
        Title = "Quest Log";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Active quests list (left panel)
        var activeQuestsLabel = new Label("Active Quests:")
        {
            X = 1,
            Y = 1
        };
        Add(activeQuestsLabel);

        _activeQuestsList = new ListView()
        {
            X = 1,
            Y = 2,
            Width = Dim.Percent(40),
            Height = Dim.Fill() - 3
        };
        _activeQuestsList.SelectedItemChanged += OnQuestSelected;
        Add(_activeQuestsList);

        // Quest details (right panel)
        var detailsLabel = new Label("Quest Details:")
        {
            X = Pos.Right(_activeQuestsList) + 2,
            Y = 1
        };
        Add(detailsLabel);

        _questDetailsView = new TextView()
        {
            X = Pos.Right(_activeQuestsList) + 2,
            Y = 2,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 3,
            ReadOnly = true
        };
        Add(_questDetailsView);

        // Close button
        var closeButton = new Button("Close [ESC]")
        {
            X = Pos.Center(),
            Y = Pos.Bottom(this) - 2
        };
        closeButton.Clicked += (s, e) => Application.RequestStop();
        Add(closeButton);
    }

    private void RefreshQuests()
    {
        var activeQuests = _questService.GetActiveQuests(_playerEntity);
        var questNames = activeQuests.Select(q => $"{q.Name} - {GetQuestProgress(q)}").ToList();

        if (_activeQuestsList != null)
        {
            _activeQuestsList.SetSource(questNames);
        }
    }

    private void OnQuestSelected(object? sender, ListViewItemEventArgs args)
    {
        var activeQuests = _questService.GetActiveQuests(_playerEntity);

        if (args.Item >= 0 && args.Item < activeQuests.Count)
        {
            var quest = activeQuests[args.Item];
            DisplayQuestDetails(quest);
        }
    }

    private void DisplayQuestDetails(QuestInfo quest)
    {
        if (_questDetailsView == null)
            return;

        var details = $@"Quest: {quest.Name}
State: {quest.State}

Description:
{quest.Description}

Objectives:
{string.Join("\n", quest.Objectives.Select(o => $"  {(o.IsCompleted ? "✓" : "○")} {o.Description} ({o.Current}/{o.Required})"))}

Rewards:
  Experience: {quest.Rewards.ExperiencePoints} XP
  Gold: {quest.Rewards.Gold}
{(quest.Rewards.ItemIds.Count > 0 ? $"  Items: {string.Join(", ", quest.Rewards.ItemIds)}" : "")}
";

        _questDetailsView.Text = details;
    }

    private string GetQuestProgress(QuestInfo quest)
    {
        int completed = quest.Objectives.Count(o => o.IsCompleted);
        int total = quest.Objectives.Count;
        return $"{completed}/{total}";
    }
}
