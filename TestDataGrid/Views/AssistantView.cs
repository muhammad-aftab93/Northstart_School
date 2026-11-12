using EnterpriseUI.Editors.Interactivity;
using EnterpriseUI.WinForms;
using TestDataGrid.Services;

namespace TestDataGrid.Views;

/// <summary>Ask questions about the school in a chat and draft letters with the prompt control; both read the live records.</summary>
public sealed class AssistantView : UserControl
{
    private readonly SchoolStore _store;
    private readonly ChatAuthor _assistant = new("assistant", "Northstar Assistant");

    public EnterpriseChatControl Chat { get; }
    public EnterpriseAIPromptControl Prompt { get; }

    public AssistantView(SchoolStore store)
    {
        _store = store; Dock = DockStyle.Fill; Font = SchoolUi.BodyFont;
        var me = new ChatAuthor("me", "You", true);
        Chat = new EnterpriseChatControl { Conversation = new ChatConversation(me), PlaceholderText = "Ask about fees, lessons, tasks, attendance or a student", AllowAttachments = false, MessageMarkup = LabelMarkupMode.Markdown, Dock = DockStyle.Fill, AccessibleName = "School questions" };
        Chat.Conversation.Receive(_assistant, "Hello! I look things up in the school records. Ask a question, or pick a shortcut below.", SchoolAnswers.StartingReplies);
        Chat.MessageSubmitted += async (_, e) => await AnswerAsync(e.Message);
        Chat.LinkClicked += (_, e) => { if (e.LinkText?.StartsWith("page:", StringComparison.Ordinal) == true) SchoolUi.Window(this)?.Navigate(e.LinkText[5..]); };

        Prompt = new EnterpriseAIPromptControl { Provider = new SchoolAssistant(store), Dock = DockStyle.Fill, AccessibleName = "Letter drafting", PromptHint = "Name a student, a course or paste notes, then pick a command" };
        foreach (var command in SchoolAssistant.Commands) Prompt.RegisterCommand(command);
        Prompt.InsertRequested += (_, text) => CopyDraft(text);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(16, 0, 16, 14) };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        split.Controls.Add(Frame("Ask the school", Chat), 0, 0);
        split.Controls.Add(Frame("Draft a letter or report", Prompt), 1, 0);
        Controls.Add(split); Controls.Add(SchoolUi.Heading("Assistant")); SchoolUi.Apply(this);
    }

    private static Panel Frame(string caption, Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 0) };
        content.Dock = DockStyle.Fill;
        panel.Controls.Add(content);
        panel.Controls.Add(new EnterpriseLabelControl { Text = caption, Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        return panel;
    }

    private async Task AnswerAsync(ChatMessage question)
    {
        var conversation = Chat.Conversation;
        conversation.SetStatus(question, ChatMessageStatus.Delivered);
        conversation.SetTyping(_assistant, true);
        await Task.Delay(650);
        if (IsDisposed) return;
        var reply = SchoolAnswers.Reply(_store.Data, question.Text);
        conversation.Receive(_assistant, reply.Text, reply.QuickReplies);
        conversation.SetStatus(question, ChatMessageStatus.Read);
    }

    private void CopyDraft(string text)
    {
        try
        {
            Clipboard.SetDataObject(text, true, 5, 100);
            SchoolUi.Window(this)?.Notify("Draft copied", "The draft is on the clipboard, ready to paste into an email or document.", NotificationSeverity.Success, null);
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            MessageBox.Show(FindForm(), exception.Message, "Copy draft", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
