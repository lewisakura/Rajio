using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace Rajio.Audio
{
    internal static class StreamController
    {
        private const string Url = "https://listen.moe/fallback";
        private const string Gateway = "wss://listen.moe/gateway_v2";

        private static WaveOut _wo;
        private static bool _initialized;

        public static bool IsPlaying;
        public static SongData SongInfo;

        internal class SongData
        {
            public string Name;
            public string Author;
            public string AuthorRomaji;

            public string Album;
            public string AlbumRomaji;
            public string AlbumArtUrl = "https://www.seekpng.com/png/detail/85-850474_shrug-emote-anime-shrug-emote-transparent.png";

            public int Seconds;
            public DateTime StartTime;
        }

        public delegate void StreamControllerEvent();

        public static event StreamControllerEvent PlayingStateChanged;
        public static event StreamControllerEvent SongDataChanged;

        private static Thread _listenMoeThread;
        private static Thread _wsHeartbeatThread;

        private static WebSocket _ws;

        public static void StartWs()
        {
            _ws = new WebSocket(Gateway);

            _ws.OnMessage += (s, e) =>
            {
                Debug.WriteLine(e.Data);
                dynamic data = JObject.Parse(e.Data);

                var opcode = (int) data.op;

                switch (opcode)
                {
                    case 0: // welcome
                        _wsHeartbeatThread = new Thread(() =>
                        {
                            while (true)
                            {
                                Thread.Sleep((int)data.d.heartbeat);
                                _ws.Send(@"{""op"":9}"); // heartbeat opcode
                            }
                        });

                        _wsHeartbeatThread.Start();
                        break;

                    case 1: // playback information
                        if (data.t == "TRACK_UPDATE" || data.t == "TRACK_UPDATE_REQUEST")
                        {
                            SongInfo = new SongData
                            {
                                Seconds = (int)data.d.song.duration,
                                StartTime = (DateTime)data.d.startTime,

                                Name = (string)data.d.song.title,
                                Author = string.Join(", ", ((JArray)data.d.song.artists).Select(a => a["name"])),
                                AuthorRomaji = string.Join(", ", ((JArray)data.d.song.artists).Select(a => a["nameRomaji"]))
                            };

                            if (((JArray)data.d.song.albums).Count > 0)
                            {
                                SongInfo.Album = (string)((JArray)data.d.song.albums)[0]["name"];
                                SongInfo.AlbumRomaji = (string)((JArray)data.d.song.albums)[0]["nameRomaji"];
                                if ((string)((JArray)data.d.song.albums)[0]["image"] != null)
                                    SongInfo.AlbumArtUrl = "https://cdn.listen.moe/covers/" + (string)((JArray)data.d.song.albums)[0]["image"];
                            }

                            SongDataChanged?.Invoke();
                        }
                        break;

                    // not required but nice
                    case 10: // heartbeat acknowledged serverside
                        Debug.WriteLine("heartbeat acknowledged");
                        break;

                    default:
                        Debug.WriteLine("unknown opcode " + opcode);
                        break;
                }
            };

            _ws.Connect();
        }

        public static void StopWs()
        {
            _wsHeartbeatThread.Abort();
            _ws.Close();
        }

        public static void StartStream()
        {
            _initialized = false;
            _wo = new WaveOut();
            _listenMoeThread = new Thread(() =>
            {
                using (var mf = new MediaFoundationReader(Url))
                {
                    _wo.Init(mf);
                    _initialized = true;

                    while (true)
                    {
                        Thread.Sleep(1000 * 60 * 60 * 24); // 1 day because this loop literally does nothing
                    }
                }
            });

            _listenMoeThread.Start();

            while (!_initialized) Thread.Sleep(100);

            _wo.Volume = Properties.Settings.Default.volume;
            _wo.Play();
            IsPlaying = true;
            PlayingStateChanged?.Invoke();
            SongDataChanged?.Invoke();
        }

        public static void StopStream(bool shuttingDown = false)
        {
            _wo.Stop();

            if (!shuttingDown)
            {
                IsPlaying = false;
                PlayingStateChanged?.Invoke();
            }

            _listenMoeThread.Abort();
        }

        public static void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _wo.Volume = (float)e.NewValue;
        }
    }
}
