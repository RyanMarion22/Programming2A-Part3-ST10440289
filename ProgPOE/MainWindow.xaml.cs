using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProgPOE
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, List<string>> keywordResponses;
        private Dictionary<string, string> sentimentResponses;
        private List<CyberTask> tasks = new List<CyberTask>();
        private int currentQuestionIndex = 0;
        private int score = 0;
        private bool waitingForName = true;
        private string userName = null;
        private bool isDarkMode = false;
        private readonly DispatcherTimer reminderTimer;
        private readonly List<string> activityLog = new List<string>();
        private const int MaxLogEntries = 5;
        private string currentTaskTitle = null;
        private string currentTaskDescription = null;
        private int taskAddStep = 0; // 0: waiting for title, 1: waiting for description, 2: waiting for reminder

        public MainWindow()
        {
            InitializeComponent();
            InitializeBot();

            SetPlaceholderStyles();
            AppendToChat("Bot: Hello! Before we start, what is your name?");
            reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            reminderTimer.Tick += ReminderTimer_Tick;
            reminderTimer.Start();
        }

        private void InitializeBot()
        {
            keywordResponses = new Dictionary<string, List<string>>
            {
                ["phishing"] = new List<string> { "Be cautious of fake emails.", "Watch for strange URLs and requests." },
                ["password"] = new List<string> { "Use complex passwords with numbers and symbols.", "Never reuse the same password." },
                ["privacy"] = new List<string> { "Review your privacy settings often.", "Limit what you share online." },
                ["scam"] = new List<string> { "Avoid clicking unknown links.", "Verify any unexpected messages." }
            };

            sentimentResponses = new Dictionary<string, string>
            {
                ["worried"] = "It’s okay to feel that way. Let me help you out.",
                ["curious"] = "Curiosity is great! Ask me something.",
                ["frustrated"] = "Cybersecurity can be confusing, but I’ve got your back."
            };
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string input = UserInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(input)) return;

            AppendToChat($"You: {input}");
            UserInput.Clear();

            string response;

            if (waitingForName)
            {
                userName = input;
                waitingForName = false;
                response = $"Nice to meet you, {userName}! You can ask me about phishing, passwords, privacy, scams, or say 'security tips', or start adding a task with 'add task'.";
                AddToActivityLog($"Task added: Greeted user and set name to {userName}.");
            }
            else
            {
                response = HandleUserInput(input.ToLower());
            }

            AppendToChat($"Bot: {response}");
        }

        private string HandleUserInput(string input)
        {
            if (input == "hello")
                return userName != null ? $"Hi {userName}! How can I assist you today?" : "Hi there! How can I assist you today?";
            if (input == "how are you")
                return "I'm doing great! Ready to help you stay safe online.";
            if (input == "security tips")
                return "1. Keep your software and OS updated.\n2. Avoid using public Wi-Fi for sensitive info.\n3. Use 2FA on your accounts.";
            if (input == "what can i ask")
                return "You can ask me about:\n- Phishing\n- Password Safety\n- Privacy\n- Scams\n- Security Tips\nOr start adding a task with 'add task'!";
            if (input == "show activity log" || input == "what have you done for me?")
            {
                ActivityLogDisplay.Text = GetActivityLog();
                MainTabControl.SelectedIndex = 3;
                return "Activity log displayed in the Activity Log tab.";
            }
            if (input == "show tasks")
            {
                if (tasks.Count == 0)
                    return "No tasks added yet.";
                string taskList = "Your tasks:\n" + string.Join("\n", tasks.Select(t => t.ToString()));
                return taskList;
            }

            foreach (var sentiment in sentimentResponses)
            {
                if (input.Contains(sentiment.Key))
                    return sentiment.Value;
            }

            foreach (var keyword in keywordResponses)
            {
                if (input.Contains(keyword.Key))
                {
                    var responses = keyword.Value;
                    var random = new Random();
                    return responses[random.Next(responses.Count)];
                }
            }

            if (input == "add task" && taskAddStep == 0)
            {
                taskAddStep = 1;
                return "Please enter the task title.";
            }
            else if (taskAddStep == 1)
            {
                currentTaskTitle = input;
                taskAddStep = 2;
                return "Please enter the task description.";
            }
            else if (taskAddStep == 2)
            {
                currentTaskDescription = input;
                taskAddStep = 3;
                return "Please enter the reminder (e.g., 'in 3 days') or type 'none' if no reminder is needed.";
            }
            else if (taskAddStep == 3)
            {
                string reminder = input == "none" ? "" : input;
                var task = new CyberTask { Title = currentTaskTitle, Description = currentTaskDescription, Reminder = reminder };
                if (tasks.Any(t => t.Title == currentTaskTitle))
                {
                    taskAddStep = 0;
                    return "A task with this title already exists.";
                }
                tasks.Add(task);
                ScheduleReminder(task);
                AddToActivityLog($"Task added: '{currentTaskTitle}' (Reminder set for {reminder}).");
                taskAddStep = 0; // Reset step
                return $"Task added: '{currentTaskTitle}' - {currentTaskDescription}" + (string.IsNullOrEmpty(reminder) ? "" : $" (Remind: {reminder}).");
            }

            if (input.Contains("remind me") && tasks.Any(t => t.Title == input.Split(new[] { "remind me" }, StringSplitOptions.None)[0].Trim()))
            {
                string title = input.Split(new[] { "remind me" }, StringSplitOptions.None)[0].Trim();
                var task = tasks.First(t => t.Title == title);
                if (!string.IsNullOrEmpty(task.Reminder))
                {
                    return $"Reminder already set for '{title}'. I'll remind you {task.Reminder}.";
                }
                string[] reminderParts = input.Split(new[] { "in " }, StringSplitOptions.None);
                if (reminderParts.Length > 1 && int.TryParse(reminderParts[1].Split(' ')[0], out int days))
                {
                    task.Reminder = $"in {days} days";
                    ScheduleReminder(task);
                    AddToActivityLog($"Reminder updated: '{title}' for {days} days.");
                    return $"Got it! I'll remind you about '{title}' in {days} days.";
                }
                return "Please specify a reminder timeframe (e.g., 'remind me in 3 days').";
            }

            if (input.Contains("complete task"))
            {
                string title = input.Replace("complete task", "").Trim();
                var task = tasks.FirstOrDefault(t => t.Title == title && !t.Title.EndsWith(" [Done]"));
                if (task != null)
                {
                    task.Title += " [Done]";
                    AddToActivityLog($"Task completed: '{task.Title.Replace(" [Done]", "")}'.");
                    return $"Task '{title}' marked as completed.";
                }
                return "No matching task found or task already completed.";
            }

            if (input.Contains("delete task"))
            {
                string title = input.Replace("delete task", "").Trim();
                var task = tasks.FirstOrDefault(t => t.Title == title);
                if (task != null)
                {
                    tasks.Remove(task);
                    AddToActivityLog($"Task deleted: '{title}'.");
                    return $"Task '{title}' deleted.";
                }
                return "No matching task found.";
            }

            if (input.Contains("quiz") || input.Contains("test me"))
                return "Go to the Quiz tab to test your cybersecurity skills!";

            return "Sorry, I didn’t quite get that. Try asking about passwords, scams, or phishing, or start adding a task with 'add task'.";
        }

        private void AppendToChat(string message)
        {
            ChatOutput.Document.Blocks.Add(new Paragraph(new Run(message)));
            ChatOutput.ScrollToEnd();
        }

        private void SetPlaceholderStyles()
        {
            foreach (var tb in new[] { TaskTitleInput, TaskDescriptionInput, TaskReminderInput })
            {
                tb.GotFocus += (s, e) =>
                {
                    if (s is TextBox textBox && textBox.Text == (string)textBox.Tag)
                    {
                        textBox.Text = "";
                        textBox.Foreground = (SolidColorBrush)FindResource("TextForeground");
                    }
                };
                tb.LostFocus += (s, e) =>
                {
                    if (s is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = (string)textBox.Tag;
                        textBox.Foreground = (SolidColorBrush)FindResource("PlaceholderForeground");
                    }
                };
            }
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            string title = TaskTitleInput.Text.Trim();
            string description = TaskDescriptionInput.Text.Trim();
            string reminder = TaskReminderInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(title) || title == (string)TaskTitleInput.Tag)
            {
                MessageBox.Show("Please enter a task title.");
                return;
            }

            if (description == (string)TaskDescriptionInput.Tag) description = "";
            if (reminder == (string)TaskReminderInput.Tag) reminder = "";

            var task = new CyberTask { Title = title, Description = description, Reminder = reminder };
            if (tasks.Any(t => t.Title == title))
            {
                MessageBox.Show("A task with this title already exists.");
                return;
            }
            tasks.Add(task);
            TaskList.Items.Add(task.ToString());

            TaskTitleInput.Text = (string)TaskTitleInput.Tag;
            TaskTitleInput.Foreground = (SolidColorBrush)FindResource("PlaceholderForeground");
            TaskDescriptionInput.Text = (string)TaskDescriptionInput.Tag;
            TaskDescriptionInput.Foreground = (SolidColorBrush)FindResource("PlaceholderForeground");
            TaskReminderInput.Text = (string)TaskReminderInput.Tag;
            TaskReminderInput.Foreground = (SolidColorBrush)FindResource("PlaceholderForeground");

            MessageBox.Show("Task added successfully!");
            ScheduleReminder(task);
            AddToActivityLog($"Task added: '{title}' (Reminder set for {reminder}).");
        }

        private void CompleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && TaskList.SelectedItem != null)
            {
                int index = TaskList.Items.IndexOf(TaskList.SelectedItem);
                if (index >= 0 && index < tasks.Count)
                {
                    var task = tasks[index];
                    if (!task.Title.EndsWith(" [Done]"))
                    {
                        task.Title += " [Done]";
                        TaskList.Items[index] = task.ToString();
                        MessageBox.Show("Task marked as completed.");
                        AddToActivityLog($"Task completed: '{task.Title.Replace(" [Done]", "")}'.");
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a task to complete.");
            }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && TaskList.SelectedItem != null)
            {
                int index = TaskList.Items.IndexOf(TaskList.SelectedItem);
                if (index >= 0 && index < tasks.Count)
                {
                    var task = tasks[index];
                    tasks.RemoveAt(index);
                    TaskList.Items.RemoveAt(index);
                    MessageBox.Show("Task deleted.");
                    AddToActivityLog($"Task deleted: '{task.Title}'.");
                }
            }
            else
            {
                MessageBox.Show("Please select a task to delete.");
            }
        }

        private class QuizQuestion
        {
            public string Question { get; set; }
            public List<string> Options { get; set; }
            public int CorrectIndex { get; set; }
            public string Feedback { get; set; }
        }

        private List<QuizQuestion> quizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion { Question = "What does 2FA stand for?", Options = new List<string> { "Two-Factor Authentication", "Two-Firewall Access", "Fast Authentication" }, CorrectIndex = 0, Feedback = "2FA adds an extra layer of security with a second verification step." },
            new QuizQuestion { Question = "Phishing attempts are usually made through:", Options = new List<string> { "Emails", "USB sticks", "Antivirus software" }, CorrectIndex = 0, Feedback = "Phishing often uses emails to trick users into providing sensitive info." },
            new QuizQuestion { Question = "True or False: You should use the same password everywhere.", Options = new List<string> { "True", "False" }, CorrectIndex = 1, Feedback = "Using unique passwords prevents a single breach from compromising all accounts." },
            new QuizQuestion { Question = "What’s a strong password made of?", Options = new List<string> { "Birthdays and names", "123456", "Letters, numbers, and symbols" }, CorrectIndex = 2, Feedback = "A mix of characters increases password strength." },
            new QuizQuestion { Question = "Which is a secure way to browse the web?", Options = new List<string> { "Using HTTPS", "Using HTTP", "Ignoring warnings" }, CorrectIndex = 0, Feedback = "HTTPS encrypts your connection, protecting your data." },
            new QuizQuestion { Question = "True or False: Public Wi-Fi is always safe for online banking.", Options = new List<string> { "True", "False" }, CorrectIndex = 1, Feedback = "Public Wi-Fi can be intercepted; use a VPN or secure network." },
            new QuizQuestion { Question = "What is a sign of a scam website?", Options = new List<string> { "Strange domain names", "HTTPS padlock", "Clear contact info" }, CorrectIndex = 0, Feedback = "Unusual domains often indicate fraudulent sites." },
            new QuizQuestion { Question = "What should you do with unused online accounts?", Options = new List<string> { "Leave them", "Delete or deactivate them", "Use them once a year" }, CorrectIndex = 1, Feedback = "Removing unused accounts reduces security risks." },
            new QuizQuestion { Question = "What does a firewall do?", Options = new List<string> { "Cooks your data", "Blocks unauthorized access", "Stores passwords" }, CorrectIndex = 1, Feedback = "Firewalls protect your network by filtering traffic." },
            new QuizQuestion { Question = "Which of these is social engineering?", Options = new List<string> { "Guessing passwords", "Tricking someone into giving info", "Firewall setup" }, CorrectIndex = 1, Feedback = "Social engineering manipulates people into revealing confidential data." }
        };

        private void LoadQuizQuestion()
        {
            if (currentQuestionIndex >= quizQuestions.Count)
            {
                QuizQuestionText.Text = "Quiz complete!";
                QuizOptionsPanel.Children.Clear();
                ScoreText.Text = $"Final Score: {score}/{quizQuestions.Count}";
                QuizProgressBar.Value = quizQuestions.Count;
                ShowFeedback($"Quiz complete! Your score is {score}/{quizQuestions.Count}. Keep learning!", true);
                NextButton.IsEnabled = false;
                AddToActivityLog($"Quiz started - {quizQuestions.Count} questions answered.");
                return;
            }

            var question = quizQuestions[currentQuestionIndex];
            QuizQuestionText.Text = question.Question;
            QuizOptionsPanel.Children.Clear();

            for (int i = 0; i < question.Options.Count; i++)
            {
                var btn = new Button
                {
                    Content = question.Options[i],
                    Tag = i,
                    Margin = new Thickness(0, 0, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MinWidth = 200
                };
                btn.Click += QuizOption_Click;
                QuizOptionsPanel.Children.Add(btn);
            }

            ScoreText.Text = $"Score: {score}";
            QuizProgressBar.Value = currentQuestionIndex;
            HideFeedback();
        }

        private void QuizOption_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            int selectedIndex = (int)button.Tag;
            var question = quizQuestions[currentQuestionIndex];

            if (selectedIndex == question.CorrectIndex)
            {
                score++;
            }

            string feedback = selectedIndex == question.CorrectIndex
                ? $"Correct! {question.Feedback}"
                : $"Incorrect. The correct answer is: {question.Options[question.CorrectIndex]}. {question.Feedback}";
            ShowFeedback(feedback, false);

            ScoreText.Text = $"Score: {score}";
            foreach (Button btn in QuizOptionsPanel.Children)
            {
                btn.IsEnabled = false;
            }
            NextButton.IsEnabled = true;
        }

        private void ShowFeedback(string message, bool isFinal)
        {
            FeedbackText.Text = message;
            FeedbackOverlay.Visibility = Visibility.Visible;
            if (isFinal)
            {
                NextButton.IsEnabled = false;
            }
        }

        private void HideFeedback()
        {
            FeedbackOverlay.Visibility = Visibility.Collapsed;
            NextButton.IsEnabled = true;
        }

        private void NextQuestion_Click(object sender, RoutedEventArgs e)
        {
            currentQuestionIndex++;
            LoadQuizQuestion();
        }

        private void FeedbackClose_Click(object sender, RoutedEventArgs e)
        {
            HideFeedback();
        }

        private void RestartQuiz_Click(object sender, RoutedEventArgs e)
        {
            currentQuestionIndex = 0;
            score = 0;
            QuizProgressBar.Value = 0;
            ScoreText.Text = $"Score: {score}";
            FeedbackOverlay.Visibility = Visibility.Collapsed;
            LoadQuizQuestion();
            AddToActivityLog("Reminder set: Quiz restarted.");
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem selectedTab)
            {
                if (selectedTab.Header.ToString() == "Quiz")
                {
                    currentQuestionIndex = 0;
                    score = 0;
                    LoadQuizQuestion();
                    NextButton.IsEnabled = true;
                    HideFeedback();
                }
                else if (selectedTab.Header.ToString() == "Activity Log")
                {
                    ActivityLogDisplay.Text = GetActivityLog();
                }
            }
        }

        private void DarkModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            isDarkMode = true;
            ApplyTheme();
        }

        private void DarkModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isDarkMode = false;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var storyboard = FindResource("ThemeTransition") as Storyboard;
            if (storyboard == null) return;

            if (isDarkMode)
            {
                Resources["PrimaryBackground"] = Resources["DarkPrimaryBackground"];
                Resources["HeaderBackground"] = Resources["DarkHeaderBackground"];
                Resources["AccentBackground"] = Resources["DarkAccentBackground"];
                Resources["AccentForeground"] = Resources["DarkAccentForeground"];
                Resources["CardBackground"] = Resources["DarkCardBackground"];
                Resources["TextForeground"] = Resources["DarkText"];
                Resources["BorderGrey"] = Resources["DarkBorderGrey"];
                Resources["PlaceholderForeground"] = Resources["DarkPlaceholder"];
            }
            else
            {
                Resources["PrimaryBackground"] = Resources["LightPrimaryBackground"];
                Resources["HeaderBackground"] = Resources["LightHeaderBackground"];
                Resources["AccentBackground"] = Resources["LightAccentBackground"];
                Resources["AccentForeground"] = Resources["LightAccentForeground"];
                Resources["CardBackground"] = Resources["LightCardBackground"];
                Resources["TextForeground"] = Resources["LightText"];
                Resources["BorderGrey"] = Resources["LightBorderGrey"];
                Resources["PlaceholderForeground"] = Resources["LightPlaceholder"];
            }

            storyboard.Begin(this);
        }

        private void ScheduleReminder(CyberTask task)
        {
            if (!string.IsNullOrWhiteSpace(task.Reminder))
            {
                if (task.Reminder.ToLower().StartsWith("in ") && int.TryParse(task.Reminder.Split(' ')[1], out int days))
                {
                    var reminderTime = DateTime.Now.AddDays(days);
                    ReminderNotification.Text = $"Reminder: {task.Title} is due in {days} days!";
                    ReminderNotification.Visibility = Visibility.Visible;
                    AddToActivityLog($"Reminder set: '{task.Title}' for {days} days from now.");
                }
            }
        }

        private void ReminderTimer_Tick(object sender, EventArgs e)
        {
            foreach (var task in tasks)
            {
                if (!string.IsNullOrWhiteSpace(task.Reminder) && !task.Title.EndsWith(" [Done]"))
                {
                    ReminderNotification.Text = $"Reminder: {task.Title} is pending!";
                    ReminderNotification.Visibility = Visibility.Visible;
                    break;
                }
            }
        }

        private void AddToActivityLog(string action)
        {
            activityLog.Insert(0, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {action}");
            if (activityLog.Count > MaxLogEntries)
            {
                activityLog.RemoveRange(MaxLogEntries, activityLog.Count - MaxLogEntries);
            }
            if (MainTabControl.SelectedIndex == 3)
            {
                ActivityLogDisplay.Text = GetActivityLog();
            }
        }

        private string GetActivityLog()
        {
            return "Here's a summary of recent actions:\n" + string.Join("\n", activityLog);
        }
    }

    public class CyberTask
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Reminder { get; set; }

        public override string ToString()
        {
            return $"{Title} - {Description}" + (string.IsNullOrEmpty(Reminder) ? "" : $" (Remind: {Reminder})");
        }
    }
}