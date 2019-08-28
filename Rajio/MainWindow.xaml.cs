using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiscordRPC;
using MaterialDesignThemes.Wpf;
using Rajio.Audio;

namespace Rajio
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool _catchingUp = true;
        private ImageSource _albumArtSource;

        private DiscordRPC _rpc;
        private Thread _currentTimerThread;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool CatchingUp
        {
            get => _catchingUp;
            set
            {
                _catchingUp = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CatchingUp"));
            }
        }

        public ImageSource AlbumArtSource
        {
            get => _albumArtSource;
            set
            {
                _albumArtSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AlbumArtSource"));
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // editing aid using the designer because it takes up the entire designer :(
            CatchingUpDialog.Visibility = Visibility.Visible;

            StreamController.PlayingStateChanged += () =>
            {
                Debug.WriteLine("fired, " + StreamController.IsPlaying);
                Dispatcher.Invoke(() =>
                {
                    PlayPauseStream.IsEnabled = true;
                    VolumeSlider.IsEnabled = true;
                    if (StreamController.IsPlaying)
                    {
                        CatchingUp = false;
                        CreateRPFromSongData();
                    }
                    else
                    {
                        _rpc.SetPresence(new RichPresence
                        {
                            Details = "Paused"
                        });

                        _currentTimerThread.Abort();
                    }

                    ControllerButtonIcon.Kind = StreamController.IsPlaying ? PackIconKind.Pause : PackIconKind.Play;
                    PlayPauseStream.ToolTip = StreamController.IsPlaying ? "Pause" : "Play";
                });
            };

            StreamController.SongDataChanged += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    _currentTimerThread?.Abort();

                    SongName.Text = StreamController.SongInfo.Name;

                    SongAuthor.Text = "by " + StreamController.SongInfo.Author;

                    AlbumArtSource = new BitmapImage(new Uri(StreamController.SongInfo.AlbumArtUrl));
                    if (StreamController.SongInfo.Album != null)
                    {
                        SongAlbum.Text = "on " + StreamController.SongInfo.Album;
                    }
                    else
                    {
                        SongAlbum.Text = null;
                    }

                    SongLength.Maximum = StreamController.SongInfo.Seconds;
                    SongLength.Value = 0;

                    if (!StreamController.IsPlaying) return;
                    _currentTimerThread = new Thread(() =>
                    {
                        try
                        {
                            var alreadyThrough = (int)(DateTime.UtcNow - StreamController.SongInfo.StartTime).TotalSeconds;
                            var externalTimer = 0;
                            Dispatcher.Invoke(() =>
                            {
                                SongLength.Value = alreadyThrough;
                                externalTimer = (int)SongLength.Value;
                            });

                            while (externalTimer < StreamController.SongInfo.Seconds) // add a little padding
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    SongLength.Value += 1;
                                    Debug.WriteLine($"{SongLength.Value}/{SongLength.Maximum}");
                                });
                                externalTimer += 1;
                                Thread.Sleep(1000);
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    });
                    _currentTimerThread.Start();
                    CreateRPFromSongData();
                });
            };

            VolumeSlider.Value = Properties.Settings.Default.volume;
            VolumeSlider.ValueChanged += (s, e) =>
            {
                Properties.Settings.Default.volume = (float)e.NewValue;
                Properties.Settings.Default.Save();

                StreamController.VolumeChanged(s, e);
            };

            _rpc = new DiscordRPC();
        }

        private void CreateRPFromSongData()
        {
            _rpc.SetPresence(new RichPresence
            {
                Details = StreamController.SongInfo.Name,
                State = "by " + StreamController.SongInfo.Author,

                Timestamps = new Timestamps
                {
                    Start = StreamController.SongInfo.StartTime,
                    End = StreamController.SongInfo.StartTime + TimeSpan.FromSeconds(StreamController.SongInfo.Seconds)
                },

                Assets = new Assets
                {
                    LargeImageText = StreamController.SongInfo.Album
                }
        });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            new Thread(StreamController.StartWs).Start();
            new Thread(StreamController.StartStream).Start();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                StreamController.StopStream(true);
                StreamController.StopWs();
            }
            catch
            {
                // ignored, we're closing
            }
        }

        private void PlayPauseStream_Click(object sender, RoutedEventArgs e)
        {
            if (StreamController.IsPlaying)
            {
                StreamController.StopStream();
            }
            else
            {
                CatchingUp = true;
                PlayPauseStream.IsEnabled = false;
                VolumeSlider.IsEnabled = false;
                new Thread(StreamController.StartStream).Start();
            }
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
