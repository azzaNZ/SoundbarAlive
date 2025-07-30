using System;
using System.IO;
using System.Threading;
using System.Media;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

// TEST COMMENT: This comment was added to verify that Program.cs updates are working correctly
// and not creating duplicate files in different folders. Date: 2024
// FIXED: Now updating the correct file at the proper path - C:\DataAnnotations\Other\c#\SoundbarAlive\

namespace SoundBarKeepAlive
{
    public partial class SoundBarApp : Form
    {
        private System.Windows.Forms.Timer _timer;
        private NotifyIcon _notifyIcon;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(8);
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

                // For testing purposes, comment out the Realtek check
                if(!device.DeviceFriendlyName.Contains("Realtek")) 
                    return; 

                // Save current state
                float originalVolume = currentVolume;
                bool originalMute = isMuted;

                // For testing, always set volume to ensure we can hear it
                device.AudioEndpointVolume.Mute = false;
                //if (currentVolume < 0.3f) // If volume is too low, temporarily increase it
                //{
                //    device.AudioEndpointVolume.MasterVolumeLevelScalar = 0.3f;
                //}

                // Generate audible tone for testing
                int sampleRate = 44100;
                int channels = 2;
                int duration = 2; // Shorter duration for testing

                byte[] audioData = GenerateTone(sampleRate, channels, duration, 15500); // 1000Hz - clearly audible
                
                // Create WAV file in memory and keep it alive during playback
                var wavStream = CreateWavMemoryStream(audioData, sampleRate, channels);
                
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        using (wavStream) // Ensure stream is disposed after use
                        using (var player = new SoundPlayer(wavStream))
                        {
                            player.PlaySync(); // This blocks until playback is complete
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error for debugging
                        _notifyIcon.ShowBalloonTip(3000, "Playback Error", 
                            $"Audio playback failed: {ex.Message}", ToolTipIcon.Warning);
                    }
                    finally
                    {
                        // Restore original volume and mute state
                        try
                        {
                            device.AudioEndpointVolume.MasterVolumeLevelScalar = originalVolume;
                            device.AudioEndpointVolume.Mute = originalMute;
                        }
                        catch { }
                    }
                });

                _lastPlayed = DateTime.Now;
                _playCount++;
                UpdateTooltip();

                _notifyIcon.ShowBalloonTip(2000, "SoundBar KeepAlive",
                    $"Test tone played ({_playCount} times total) - Device: {device.DeviceFriendlyName}", ToolTipIcon.Info);
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

            // Use a lower amplitude to avoid clipping and distortion
            double amplitude = 0.3; // 30% of max volume to avoid clipping

            for (int i = 0; i < totalSamples; i++)
            {
                short sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * amplitude);
                for (int channel = 0; channel < channels; channel++)
                {
                    int index = (i * channels + channel) * 2;
                    audioData[index] = (byte)(sample & 0xFF);
                    audioData[index + 1] = (byte)((sample >> 8) & 0xFF);
                }
            }

            return audioData;
        }

        private MemoryStream CreateWavMemoryStream(byte[] audioData, int sampleRate, int channels)
        {
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                // WAV header
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + audioData.Length);
                writer.Write("WAVE".ToCharArray());

                // fmt chunk
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);

                // data chunk
                writer.Write("data".ToCharArray());
                writer.Write(audioData.Length);
                writer.Write(audioData);
            }
            
            stream.Position = 0; // Reset position to beginning for reading
            return stream;
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
