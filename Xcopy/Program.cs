using System.Drawing;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    private const string SourceDirectory = @"C:\Users\Administrator\Desktop\合同台账\";
    private const string TargetRoot = "https://dav.gfwzb.com/";
    private const string TargetUsername = "gfwzb";
    private const string TargetPassword = "popmart";
    private const int MaxRetries = 3;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromMinutes(5);
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "xcopy-log.txt");

    [STAThread]
    private static async Task Main()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex}");
        }
    }

    private static async Task RunAsync()
    {
        if (!Directory.Exists(SourceDirectory))
        {
            Log($"Source directory not found: {SourceDirectory}");
            return;
        }

        using var handler = new HttpClientHandler
        {
            PreAuthenticate = true,
            Credentials = new NetworkCredential(TargetUsername, TargetPassword),
            AllowAutoRedirect = true
        };

        using var client = new HttpClient(handler)
        {
            Timeout = HttpTimeout
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XcopyWebDav", "1.0"));
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{TargetUsername}:{TargetPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        var files = Directory.EnumerateFiles(SourceDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Log($"Starting copy. Files found: {files.Count}");

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var targetUri = BuildTargetUri(TargetRoot, fileName);

            await UploadWithRetryAsync(client, file, targetUri);
        }

        Log("Copy completed.");
        ShowCompletionDialog();
    }

    private static async Task UploadWithRetryAsync(HttpClient client, string filePath, Uri targetUri)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using var stream = File.OpenRead(filePath);
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                using var request = new HttpRequestMessage(HttpMethod.Put, targetUri)
                {
                    Content = content
                };

                using var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    Log($"OK: {Path.GetFileName(filePath)} -> {targetUri}");
                    return;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                Log($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} for {filePath}. Response: {responseBody}");
            }
            catch (Exception ex)
            {
                Log($"Attempt {attempt} failed for {filePath}: {ex.Message}");
            }

            if (attempt < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        Log($"FAILED: {filePath} after {MaxRetries} attempts.");
    }

    private static Uri BuildTargetUri(string baseUri, string fileName)
    {
        var trimmedBase = baseUri.TrimEnd('/') + "/";
        var encodedFileName = Uri.EscapeDataString(fileName);
        return new Uri(trimmedBase + encodedFileName);
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Swallow logging errors to avoid interruptions.
        }
    }

    private static void ShowCompletionDialog()
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var dialog = new AutoCloseDialog("任务已完成", 3);
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Log($"Dialog error: {ex.Message}");
        }
    }

    private sealed class AutoCloseDialog : Form
    {
        private readonly System.Windows.Forms.Timer _timer;
        private int _remainingSeconds;
        private readonly Label _countdownLabel;

        public AutoCloseDialog(string title, int seconds)
        {
            Text = title;
            _remainingSeconds = Math.Max(1, seconds);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(320, 140);

            var messageLabel = new Label
            {
                AutoSize = false,
                Text = "任务已完成。",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 70
            };

            _countdownLabel = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 30
            };

            var okButton = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Dock = DockStyle.Bottom,
                Height = 30
            };

            Controls.Add(okButton);
            Controls.Add(_countdownLabel);
            Controls.Add(messageLabel);

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += (_, _) => TickCountdown();
            UpdateCountdownText();
            _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        private void TickCountdown()
        {
            _remainingSeconds--;
            if (_remainingSeconds <= 0)
            {
                Close();
                return;
            }

            UpdateCountdownText();
        }

        private void UpdateCountdownText()
        {
            _countdownLabel.Text = $"窗口将在 {_remainingSeconds} 秒后自动关闭。";
        }
    }
}
