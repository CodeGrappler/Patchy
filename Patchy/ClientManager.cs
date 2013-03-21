﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Threading;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.ComponentModel;
using Newtonsoft.Json;
using Mono.Nat;
using MonoTorrent.Client.Connections;

namespace Patchy
{
    public class ClientManager
    {
        private SettingsManager SettingsManager { get; set; }

        public void Initialize(SettingsManager settingsManager)
        {
            SettingsManager = settingsManager;
            Torrents = new ObservableCollection<PeriodicTorrent>();
            Torrents.CollectionChanged += Torrents_CollectionChanged;

            var port = SettingsManager.IncomingPort;
            if (SettingsManager.UseRandomPort)
                port = new Random().Next(1, 65536);
            if (SettingsManager.MapWithUPnP)
                MapPort();
            var settings = new EngineSettings(SettingsManager.DefaultDownloadLocation, port);

            settings.PreferEncryption = SettingsManager.EncryptionSettings != EncryptionTypes.PlainText; // Always prefer encryption unless it's disabled
            settings.AllowedEncryption = SettingsManager.EncryptionSettings;
            Client = new ClientEngine(settings);
            Client.ChangeListenEndpoint(new IPEndPoint(IPAddress.Any, port));
            if (SettingsManager.EnableDHT)
            {
                var listener = new DhtListener(new IPEndPoint(IPAddress.Any, port));
                var dht = new DhtEngine(listener);
                Client.RegisterDht(dht);
                listener.Start();
                if (File.Exists(SettingsManager.DhtCachePath))
                    dht.Start(File.ReadAllBytes(SettingsManager.DhtCachePath));
                else
                    dht.Start();
            }
            SettingsManager.PropertyChanged += SettingsManager_PropertyChanged;
        }

        void SettingsManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IncomingPort":
                    Client.Listener.ChangeEndpoint(new IPEndPoint(IPAddress.Any, SettingsManager.IncomingPort));
                    if (SettingsManager.MapWithUPnP)
                        MapPort();
                    break;
                case "MapWithUPnP":
                    if (SettingsManager.MapWithUPnP)
                        MapPort();
                    break;
                case "MaxUploadSpeed":
                    Client.Settings.GlobalMaxUploadSpeed = SettingsManager.MaxUploadSpeed * 1024;
                    break;
                case "MaxDownloadSpeed":
                    Client.Settings.GlobalMaxDownloadSpeed = SettingsManager.MaxDownloadSpeed * 1024;
                    break;
                case "MaxConnections":
                    Client.Settings.GlobalMaxConnections = SettingsManager.MaxConnections;
                    break;
                case "EnableDHT":
                    if (SettingsManager.EnableDHT)
                    {
                        var port = SettingsManager.IncomingPort;
                        if (SettingsManager.UseRandomPort)
                            port = new Random().Next(1, 65536);
                        var listener = new DhtListener(new IPEndPoint(IPAddress.Any, port));
                        var dht = new DhtEngine(listener);
                        Client.RegisterDht(dht);
                        listener.Start();
                        if (File.Exists(SettingsManager.DhtCachePath))
                            dht.Start(File.ReadAllBytes(SettingsManager.DhtCachePath));
                        else
                            dht.Start();
                    }
                    else
                        Client.DhtEngine.Stop();
                    break;
                case "EncryptionSettings":
                    Client.Settings.AllowedEncryption = SettingsManager.EncryptionSettings;
                    break;
                case "ProxyAddress":
                case "EnableProxyAuthentication":
                case "ProxyUsername":
                case "ProxyPassword":
                    if (string.IsNullOrEmpty(SettingsManager.ProxyAddress))
                        ConnectionFactory.RegisterTypeForProtocol("tcp", typeof(IPV4Connection));
                    else
                    {
                        ConnectionFactory.RegisterTypeForProtocol("tcp", typeof (ProxiedConnection));
                        ushort port = 1080;
                        string address = SettingsManager.ProxyAddress;
                        if (SettingsManager.ProxyAddress.Contains(':'))
                        {
                            var parts = SettingsManager.ProxyAddress.Split(':');
                            address = parts[0];
                            port = ushort.Parse(parts[1]);
                        }
                        if (SettingsManager.EnableProxyAuthentication)
                            ProxiedConnection.SetProxyDetails(address, port);
                        else
                            ProxiedConnection.SetProxyDetails(address, port, SettingsManager.ProxyUsername, SettingsManager.ProxyPassword);
                    }
                    break;
            }
        }

        private void MapPort()
        {
            NatUtility.DeviceFound += NatUtility_DeviceFound;
            NatUtility.StartDiscovery();
        }

        void NatUtility_DeviceFound(object sender, DeviceEventArgs e)
        {
            try
            {
                e.Device.CreatePortMap(new Mapping(Protocol.Tcp, SettingsManager.IncomingPort, SettingsManager.IncomingPort));
                NatUtility.StopDiscovery();
            }
            catch { }
        }

        void Torrents_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    for (int i = 0; i < Torrents.Count; i++)
                        Torrents[i].Index = i + 1;
                }));
        }

        public PeriodicTorrent AddTorrent(TorrentWrapper torrent)
        {
            return AddTorrent(torrent, SettingsManager.StartTorrentsImmediately);
        }

        public PeriodicTorrent AddTorrent(TorrentWrapper torrent, bool startImmediately)
        {
            // Apply settings
            torrent.Settings.UseDht = SettingsManager.EnableDHT;
            torrent.Settings.MaxConnections = SettingsManager.MaxConnectionsPerTorrent;
            torrent.Settings.MaxDownloadSpeed = SettingsManager.MaxDownloadSpeed;
            torrent.Settings.MaxUploadSpeed = SettingsManager.MaxUploadSpeed;
            torrent.Settings.UploadSlots = SettingsManager.UploadSlotsPerTorrent;
            var periodicTorrent = new PeriodicTorrent(torrent);
            Task.Factory.StartNew(() =>
                {
                    Client.Register(torrent);
                    if (startImmediately)
                        torrent.Start();
                });
            Application.Current.Dispatcher.BeginInvoke(new Action(() => Torrents.Add(periodicTorrent)));
            return periodicTorrent;
        }

        public PeriodicTorrent LoadFastResume(FastResume resume, TorrentWrapper torrent, bool startImmediately)
        {
            // Apply settings
            torrent.Settings.UseDht = SettingsManager.EnableDHT;
            torrent.Settings.MaxConnections = SettingsManager.MaxConnectionsPerTorrent;
            torrent.Settings.MaxDownloadSpeed = SettingsManager.MaxDownloadSpeed;
            torrent.Settings.MaxUploadSpeed = SettingsManager.MaxUploadSpeed;
            torrent.Settings.UploadSlots = SettingsManager.UploadSlotsPerTorrent;
            var periodicTorrent = new PeriodicTorrent(torrent);
            Task.Factory.StartNew(() =>
                {
                    torrent.LoadFastResume(resume);
                    Client.Register(torrent);
                    if (startImmediately)
                        torrent.Start();
                });
            Application.Current.Dispatcher.BeginInvoke(new Action(() => Torrents.Add(periodicTorrent)));
            return periodicTorrent;
        }

        public void RemoveTorrent(PeriodicTorrent torrent)
        {
            torrent.Torrent.TorrentStateChanged += (s, e) =>
                {
                    if (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Error)
                    {
                        torrent.Torrent.Stop();
                        try
                        {
                            Client.Unregister(torrent.Torrent);
                        }
                        catch { } // TODO: See if we need to do more
                        // Delete cache
                        if (File.Exists(torrent.CacheFilePath))
                            File.Delete(torrent.CacheFilePath);
                        if (File.Exists(Path.Combine(SettingsManager.TorrentCachePath,
                            Path.GetFileNameWithoutExtension(torrent.CacheFilePath) + ".info")))
                        {
                            File.Delete(Path.Combine(SettingsManager.TorrentCachePath,
                                Path.GetFileNameWithoutExtension(torrent.CacheFilePath) + ".info"));
                        }
                        torrent.Torrent.Dispose();
                        // We need to delay this until we're out of the handler for some reason
                        Task.Factory.StartNew(() => Application.Current.Dispatcher.BeginInvoke(new Action(() => Torrents.Remove(torrent))));
                    }
                };
            Task.Factory.StartNew(() => torrent.Torrent.Stop());
        }

        public void RemoveTorrentAndFiles(PeriodicTorrent torrent)
        {
            torrent.Torrent.TorrentStateChanged += (s, e) =>
            {
                if (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Error)
                {
                    Client.Unregister(torrent.Torrent);
                    // Delete cache
                    if (File.Exists(torrent.CacheFilePath))
                        File.Delete(torrent.CacheFilePath);
                    if (File.Exists(Path.Combine(SettingsManager.TorrentCachePath,
                        Path.GetFileNameWithoutExtension(torrent.CacheFilePath) + ".info")))
                    {
                        File.Delete(Path.Combine(SettingsManager.TorrentCachePath,
                            Path.GetFileNameWithoutExtension(torrent.CacheFilePath) + ".info"));
                    }
                    // Delete files
                    try
                    {
                        Directory.Delete(torrent.Torrent.Path, true);
                    }
                    catch { }

                    torrent.Torrent.Dispose();
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => Torrents.Remove(torrent)));
                }
            };
            Task.Factory.StartNew(() => torrent.Torrent.Stop());
        }

        public void Shutdown()
        {
            if (SettingsManager.EnableDHT)
            {
                Client.DhtEngine.Stop();
                File.WriteAllBytes(SettingsManager.DhtCachePath, Client.DhtEngine.SaveNodes());
            }
            Client.Dispose();
        }

        public PeriodicTorrent GetTorrent(Torrent torrent)
        {
            return Torrents.FirstOrDefault(t => t.Torrent.Torrent == torrent);
        }

        public void MoveTorrent(PeriodicTorrent torrent, string path)
        {
            Task.Factory.StartNew(() =>
                {
                    path = Path.Combine(path, Path.GetFileName(torrent.Torrent.Path));
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    var oldPath = torrent.Torrent.Path;
                    torrent.Torrent.Stop();
                    while (torrent.State != TorrentState.Stopped) ;
                    torrent.Torrent.MoveFiles(path, true);
                    torrent.Torrent.Start();
                    Directory.Delete(oldPath, true);
                    torrent.Torrent.Path = torrent.Torrent.SavePath;

                    var cache = Path.Combine(SettingsManager.TorrentCachePath, Path.GetFileName(oldPath));
                    cache = Path.Combine(Path.GetDirectoryName(cache), Path.GetFileName(cache)) + ".info";
                    torrent.TorrentInfo.Path = torrent.Torrent.Path;
                    var serializer = new JsonSerializer();
                    using (var writer = new StreamWriter(cache))
                        serializer.Serialize(new JsonTextWriter(writer), torrent.TorrentInfo);
                });
        }

        public ObservableCollection<PeriodicTorrent> Torrents { get; set; }

        private static ClientEngine Client { get; set; }

        public static string CleanFileName(string fileName)
        {
            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), String.Empty));
        }
    }

    public class TorrentWrapper : TorrentManager
    {
        public string Name { get; private set; }
        public long Size { get; private set; }
        public bool IsMagnet { get; set; }
        public string Path { get; set; }

        public TorrentWrapper(Torrent torrent, string savePath, TorrentSettings settings)
            : base(torrent, savePath, settings)
        {
            Name = torrent.Name;
            Size = torrent.Size;
            IsMagnet = false;
            Path = savePath;
        }

        public TorrentWrapper(MagnetLink magnetLink, string savePath, TorrentSettings settings, string torrentSave)
            : base(magnetLink, savePath, settings, torrentSave)
        {
            Name = magnetLink.Name;
            Name = HttpUtility.HtmlDecode(HttpUtility.UrlDecode(Name));
            Size = -1;
            IsMagnet = true;
            Path = savePath;
        }
    }
}
