using System;
using System.IO;
using System.Threading;
using System.Media;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace SoundBarKeepAlive
{
    public partial class SoundBarApp : Form
    {
        private System.Windows.Forms.Timer _timer;
        private NotifyIcon _notifyIcon;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
        private DateTime _lastPlayed = DateTime.MinValue;
        private int _playCount = 0;
        private readonly MMDeviceEnumerator _deviceEnumerator = new MMDeviceEnumerator();

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
            this.Load += (s, e) => this.Hide();
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "SoundBar KeepAlive - Next play: calculating...";
            _notifyIcon.Visible = true;

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
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = (int)_interval.TotalMilliseconds;
            _timer.Tick += (s, e) => PlaySilentSound();
            _timer.Start();

            PlaySilentSound();
            UpdateTooltip();
        }

        private void PlaySilentSound()
        {
            try
            {
                var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                float currentVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar; // 0.0–1.0
                bool isMuted = device.AudioEndpointVolume.Mute;

                // If already audible, do nothing
                //if (!isMuted && currentVolume > 0.001f)
                //{
                //    return;
                //}

                if(!device.DeviceFriendlyName.Contains("Realtek")) 
                    return; 

                // Save current state
                float originalVolume = currentVolume;
                bool originalMute = isMuted;

                // Temporarily unmute and set volume to 50%
                //device.AudioEndpointVolume.Mute = false;
                //device.AudioEndpointVolume.MasterVolumeLevelScalar = 0.5f;

                // Generate silent audio
                int sampleRate = 44100;
                int channels = 2;
                int duration = 8;

                //byte[] audioData = GenerateSilentTone(sampleRate, channels, duration);
                byte[] audioData = GenerateTone(sampleRate, channels, duration,15500);
                string tempFile = Path.GetTempFileName() + ".wav";
                WriteWavFile(tempFile, audioData, sampleRate, channels);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        using (var player = new SoundPlayer(tempFile))
                        {
                            player.PlaySync();
                        }
                        File.Delete(tempFile);
                    }
                    catch { }

                    // Restore original volume and mute
                    device.AudioEndpointVolume.MasterVolumeLevelScalar = originalVolume;
                    device.AudioEndpointVolume.Mute = originalMute;
                });

                _lastPlayed = DateTime.Now;
                _playCount++;
                UpdateTooltip();

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
            return new byte[totalSamples * 2]; // Silence (16-bit PCM)
        }

        private byte[] GenerateTone(int sampleRate, int channels, int duration, int frequency)
        {
            int totalSamples = sampleRate * duration;
            byte[] audioData = new byte[totalSamples * channels * 2]; // 16-bit PCM

            for (int i = 0; i < totalSamples; i++)
            {
                short sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue);
                for (int channel = 0; channel < channels; channel++)
                {
                    int index = (i * channels + channel) * 2;
                    audioData[index] = (byte)(sample & 0xFF);
                    audioData[index + 1] = (byte)((sample >> 8) & 0xFF);
                }
            }

            return audioData;
        }

        private void WriteWavFile(string filename, byte[] audioData, int sampleRate, int channels)
        {
            using (var fs = new FileStream(filename, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + audioData.Length);
                writer.Write("WAVE".ToCharArray());

                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);

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
                _deviceEnumerator?.Dispose();
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
