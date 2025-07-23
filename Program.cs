using System;
using System.IO;
using System.Threading;
using System.Media;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;

namespace SoundBarKeepAlive
{
    public partial class SoundBarApp : Form
    {
        private System.Windows.Forms.Timer _timer;
        private NotifyIcon _notifyIcon;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
        private DateTime _lastPlayed = DateTime.MinValue;
        private int _playCount = 0;

        public SoundBarApp()
        {
            InitializeComponent();
            SetupSystemTray();
            StartTimer();
        }

        private void InitializeComponent()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;
            this.Text = "SoundBar KeepAlive";
            this.Size = new Size(300, 200);

            // Hide the form immediately
            this.Load += (s, e) => this.Hide();
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "SoundBar KeepAlive - Next play: calculating...";
            _notifyIcon.Visible = true;

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            var statusItem = new ToolStripMenuItem("Status");
            statusItem.Click += ShowStatus;

            var playNowItem = new ToolStripMenuItem("Play Silent Sound Now");
            playNowItem.Click += (s, e) => PlaySilentSound();

            var startupItem = new ToolStripMenuItem("Add to Startup");
            startupItem.Click += AddToStartup;

            var removeStartupItem = new ToolStripMenuItem("Remove from Startup");
            removeStartupItem.Click += RemoveFromStartup;

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => {
                _notifyIcon.Visible = false;
                Application.Exit();
            };

            contextMenu.Items.AddRange(new ToolStripItem[] {
                statusItem,
                new ToolStripSeparator(),
                playNowItem,
                new ToolStripSeparator(),
                startupItem,
                removeStartupItem,
                new ToolStripSeparator(),
                exitItem
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += ShowStatus;
        }

        private void StartTimer()
        {
            // Start immediately, then repeat every 10 minutes
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = (int)_interval.TotalMilliseconds;
            _timer.Tick += (s, e) => PlaySilentSound();
            _timer.Start();

            // Play immediately on startup
            PlaySilentSound();

            UpdateTooltip();
        }

        private void PlaySilentSound()
        {
            try
            {
                // Generate a very quiet sine wave for 20 seconds
                int sampleRate = 44100;
                int channels = 2; // Stereo
                int duration = 8; // seconds

                // Create a very quiet 1000 Hz tone
                byte[] audioData = GenerateSilentTone(sampleRate, channels, duration);

                // Create a temporary WAV file
                string tempFile = Path.GetTempFileName() + ".wav";
                WriteWavFile(tempFile, audioData, sampleRate, channels);

                // Play the sound in a separate thread to avoid blocking
                ThreadPool.QueueUserWorkItem(_ => {
                    try
                    {
                        using (var player = new SoundPlayer(tempFile))
                        {
                            player.PlaySync();
                        }
                        File.Delete(tempFile);
                    }
                    catch { }
                });

                _lastPlayed = DateTime.Now;
                _playCount++;
                UpdateTooltip();

                // Show a brief notification (optional)
                _notifyIcon.ShowBalloonTip(2000, "SoundBar KeepAlive",
                    $"Silent sound played ({_playCount} times total)", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _notifyIcon.ShowBalloonTip(5000, "SoundBar KeepAlive Error",
                    $"Error playing sound: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void UpdateTooltip()
        {
            var nextPlay = _lastPlayed.Add(_interval);
            var timeUntilNext = nextPlay - DateTime.Now;

            if (timeUntilNext.TotalSeconds > 0)
            {
                _notifyIcon.Text = $"SoundBar KeepAlive - Next play in {timeUntilNext.Minutes:D2}:{timeUntilNext.Seconds:D2}";
            }
            else
            {
                _notifyIcon.Text = "SoundBar KeepAlive - Playing soon...";
            }
        }

        private byte[] GenerateSilentTone(int sampleRate, int channels, int duration)
        {
            int totalSamples = sampleRate * channels * duration;
            byte[] audioData = new byte[totalSamples * 2]; // 16-bit samples

            return audioData;
        }

        private void WriteWavFile(string filename, byte[] audioData, int sampleRate, int channels)
        {
            using (var fs = new FileStream(filename, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                // WAV header
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + audioData.Length);
                writer.Write("WAVE".ToCharArray());

                // fmt chunk
                writer.Write("fmt ".ToCharArray());
                writer.Write(16); // chunk size
                writer.Write((short)1); // PCM format
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2); // byte rate
                writer.Write((short)(channels * 2)); // block align
                writer.Write((short)16); // bits per sample

                // data chunk
                writer.Write("data".ToCharArray());
                writer.Write(audioData.Length);
                writer.Write(audioData);
            }
        }

        private void ShowStatus(object sender, EventArgs e)
        {
            var nextPlay = _lastPlayed.Add(_interval);
            var timeUntilNext = nextPlay - DateTime.Now;

            string message;
            if (_lastPlayed == DateTime.MinValue)
            {
                message = "Starting up... First play will be soon.";
            }
            else if (timeUntilNext.TotalSeconds > 0)
            {
                message = $"Status: Running\n" +
                         $"Last played: {_lastPlayed:HH:mm:ss}\n" +
                         $"Next play: {nextPlay:HH:mm:ss}\n" +
                         $"Time until next: {timeUntilNext.Minutes:D2}:{timeUntilNext.Seconds:D2}\n" +
                         $"Total plays: {_playCount}";
            }
            else
            {
                message = $"Status: Running\n" +
                         $"Last played: {_lastPlayed:HH:mm:ss}\n" +
                         $"Next play: Due now\n" +
                         $"Total plays: {_playCount}";
            }

            MessageBox.Show(message, "SoundBar KeepAlive Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AddToStartup(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.SetValue("SoundBarKeepAlive", Application.ExecutablePath);
                }
                MessageBox.Show("Successfully added to Windows startup!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add to startup: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveFromStartup(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.DeleteValue("SoundBarKeepAlive", false);
                }
                MessageBox.Show("Successfully removed from Windows startup!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove from startup: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                _notifyIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SoundBarApp());
        }
    }
}