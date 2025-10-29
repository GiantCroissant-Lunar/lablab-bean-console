using Terminal.Gui;
using Arch.Core;
using LablabBean.Plugins.NPC.Services;

namespace LablabBean.Game.TerminalUI.Views;

/// <summary>
/// Dialogue UI screen - displays NPC dialogue and choices
/// </summary>
public class DialogueView : Window
{
    private readonly NPCService _npcService;
    private readonly Entity _playerEntity;
    private readonly Entity _npcEntity;
    private readonly string _npcName;

    private Label? _npcTextLabel;
    private ListView? _choicesList;
    private List<DialogueChoice>? _currentChoices;

    public DialogueView(NPCService npcService, Entity playerEntity, Entity npcEntity, string npcName) : base()
    {
        _npcService = npcService;
        _playerEntity = playerEntity;
        _npcEntity = npcEntity;
        _npcName = npcName;

        InitializeUI();
        DisplayDialogue("Greetings, adventurer!");
    }

    private void InitializeUI()
    {
        Title = $"Dialogue with {_npcName}";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(70);
        Height = Dim.Percent(60);

        // NPC name label
        var npcNameLabel = new Label($"{_npcName}:")
        {
            X = 1,
            Y = 1,
            ColorScheme = Colors.Dialog
        };
        Add(npcNameLabel);

        // NPC text display
        _npcTextLabel = new Label("")
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2,
            Height = Dim.Percent(40)
        };
        Add(_npcTextLabel);

        // Choices label
        var choicesLabel = new Label("Your response:")
        {
            X = 1,
            Y = Pos.Bottom(_npcTextLabel) + 1
        };
        Add(choicesLabel);

        // Choices list
        _choicesList = new ListView()
        {
            X = 1,
            Y = Pos.Bottom(choicesLabel) + 1,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 3
        };
        _choicesList.OpenSelectedItem += OnChoiceSelected;
        Add(_choicesList);

        // Navigation hint
        var hintLabel = new Label("[↑↓] Navigate | [Enter] Select | [ESC] Close")
        {
            X = Pos.Center(),
            Y = Pos.Bottom(this) - 2,
            ColorScheme = Colors.Dialog
        };
        Add(hintLabel);
    }

    private void DisplayDialogue(string npcText, List<DialogueChoice>? choices = null)
    {
        if (_npcTextLabel != null)
        {
            _npcTextLabel.Text = WrapText(npcText, (int)(_npcTextLabel.Frame.Width - 2));
        }

        _currentChoices = choices ?? new List<DialogueChoice>
        {
            new DialogueChoice("ask-about-quest", "Do you have any tasks for me?"),
            new DialogueChoice("ask-about-lore", "Tell me about this place."),
            new DialogueChoice("goodbye", "Farewell.")
        };

        if (_choicesList != null)
        {
            _choicesList.SetSource(_currentChoices.Select((c, i) => $"{i + 1}. {c.Text}").ToList());
        }
    }

    private void OnChoiceSelected(object? sender, ListViewItemEventArgs args)
    {
        if (_currentChoices == null || args.Item < 0 || args.Item >= _currentChoices.Count)
            return;

        var choice = _currentChoices[args.Item];

        // Send choice to NPC service
        _npcService.SelectChoice(_playerEntity, choice.Id);

        // Handle special cases
        if (choice.Id == "goodbye" || choice.Id == "continue")
        {
            Application.RequestStop();
        }
        else
        {
            // Update dialogue based on choice
            UpdateDialogueForChoice(choice.Id);
        }
    }

    private void UpdateDialogueForChoice(string choiceId)
    {
        switch (choiceId)
        {
            case "ask-about-quest":
                DisplayDialogue(
                    "Indeed! I seek a brave soul to retrieve an ancient artifact from the fifth level of the dungeon. Will you accept?",
                    new List<DialogueChoice>
                    {
                        new DialogueChoice("accept_quest", "I accept your quest!"),
                        new DialogueChoice("decline_quest", "Not right now.")
                    }
                );
                break;

            case "accept_quest":
                DisplayDialogue(
                    "Excellent! May the ancient powers guide you. Return to me when you have the artifact.",
                    new List<DialogueChoice>
                    {
                        new DialogueChoice("continue", "I'll return soon.")
                    }
                );
                break;

            case "decline_quest":
                DisplayDialogue(
                    "I understand. Perhaps another time, when you feel ready.",
                    new List<DialogueChoice>
                    {
                        new DialogueChoice("continue", "Farewell.")
                    }
                );
                break;

            case "ask-about-lore":
                DisplayDialogue(
                    "This dungeon was built centuries ago by a forgotten civilization. Many treasures and dangers lie within.",
                    new List<DialogueChoice>
                    {
                        new DialogueChoice("continue", "I see. Thank you.")
                    }
                );
                break;
        }
    }

    private string WrapText(string text, int maxWidth)
    {
        if (maxWidth <= 0 || text.Length <= maxWidth)
            return text;

        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 <= maxWidth)
            {
                currentLine += (currentLine.Length > 0 ? " " : "") + word;
            }
            else
            {
                if (currentLine.Length > 0)
                    lines.Add(currentLine);
                currentLine = word;
            }
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine);

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Represents a dialogue choice
/// </summary>
public record DialogueChoice(string Id, string Text);
