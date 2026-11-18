using Microsoft.Web.WebView2.WinForms;

namespace PickUp.Windows;

public sealed class MainForm : Form
{
    private readonly TextBox _serverUrl = new() { Dock = DockStyle.Fill, Text = Environment.GetEnvironmentVariable("PICKUP_SERVER_URL") ?? "https://localhost:7001" };
    private readonly Button _openButton = new() { Text = "Verbinden", AutoSize = true };
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { AutoSize = true, Text = "Serveradresse eingeben und verbinden." };

    public MainForm()
    {
        Text = "Pick Up Online";
        Width = 1320;
        Height = 900;
        MinimumSize = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 3,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(36, 22, 50)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.Controls.Add(new Label { Text = "Server-URL", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        toolbar.Controls.Add(_serverUrl, 1, 0);
        toolbar.Controls.Add(_openButton, 2, 0);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 26, Padding = new Padding(8, 3, 8, 2), BackColor = Color.FromArgb(24, 15, 34) };
        _status.ForeColor = Color.Gainsboro;
        bottom.Controls.Add(_status);

        Controls.Add(_browser);
        Controls.Add(bottom);
        Controls.Add(toolbar);
        _openButton.Click += async (_, _) => await NavigateAsync();
        _serverUrl.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await NavigateAsync(); }
        };
        Shown += async (_, _) => await NavigateAsync();
    }

    private async Task NavigateAsync()
    {
        if (!Uri.TryCreate(_serverUrl.Text.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            _status.Text = "Bitte eine gültige HTTP- oder HTTPS-Serveradresse eingeben.";
            return;
        }

        try
        {
            _openButton.Enabled = false;
            _status.Text = "WebView2 wird gestartet …";
            await _browser.EnsureCoreWebView2Async();
            _browser.Source = uri;
            _status.Text = "Verbunden mit " + uri;
        }
        catch (Exception ex)
        {
            _status.Text = "WebView2/Navigation fehlgeschlagen: " + ex.Message;
        }
        finally { _openButton.Enabled = true; }
    }
}
