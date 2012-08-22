using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
#if PLATFORM_MACOSX
using MonoMac;
using MonoMac.Foundation;
using OSXUtils;
#endif

namespace VimFastFind {
    // uses VolumeWatcher so on mac your application's main thread must be in a
    // CoreFoundation run loop.
    //
    public class DirectoryWatcher : IDisposable
    {
        public delegate void AvailabilityChangedDelegate(DirectoryWatcher sender, string path, bool exists);
        public delegate void ContentsChangedDelegate(DirectoryWatcher sender, string fullpath);

        string _path;
        string _fullpath;
        bool _watchcontents;

        // NOTE: we use FileSystemWatcher and VolumeWatcher on mac and windows. on windows and linux
        // we periodically poll to ensure the directory still exists. we don't need to poll on mac
        // because FsEventsWatcher notifies of root changes.

#if PLATFORM_MACOSX
        FsEventsWatcher _fswatcher;
#else
        FileSystemWatcher _fswatcher;
        System.Threading.Timer _availabletimer;
#endif

#if PLATFORM_MACOSX || PLATFORM_WINDOWS
        VolumeWatcher _volwatcher;
#endif

        // fires when directory disappears (volume unmounted, dir renamed, dir
        // deleted) and when directory reappears (volume mounted, dir
        // created/restored).
        public event AvailabilityChangedDelegate AvailabilityChanged;

#if PLATFORM_MACOSX
        // on mac we don't have information about specific file changes.
        // instead of firing added/removed we fire this event indicating that
        // something happened inside the subdirectory provided.
        public event ContentsChangedDelegate SubdirectoryChanged;
#else
        public event ContentsChangedDelegate FileAdded;
        public event ContentsChangedDelegate FileRemoved;
        public event ContentsChangedDelegate FileModified;
#endif

        public DirectoryWatcher(string path)
        {
            Path = path;
        }

        public void Initialize()
        {
#if PLATFORM_MACOSX || PLATFORM_WINDOWS
            _volwatcher = new VolumeWatcher();
            _volwatcher.VolumeChanged += ev_VolumeChanged;
#endif

#if PLATFORM_WINDOWS
            _volwatcher.DriveToWatchForRemoval = System.IO.Path.GetPathRoot(_fullpath);
#endif

#if PLATFORM_MACOSX
            ThreadPool.QueueUserWorkItem(delegate { _CheckAvailable(false); });
#else
            _availabletimer = new System.Threading.Timer(delegate { _CheckAvailable(false); }, null, 0, 5000);
#endif
        }

        public string Path
        {
            get
            {
                return _path;
            }
            private set
            {
                _path = value;
                _fullpath = System.IO.Path.GetFullPath(_path);
            }
        }

        public bool Exists
        {
            get; private set;
        }

        public bool EnableWatchingContents
        {
            get { return _watchcontents; }
            set { _watchcontents = value; }
        }

        public void Dispose()
        {
            if (_fswatcher != null)
                _fswatcher.Dispose();

#if SYSTEM_WINDOWS || SYSTEM_LINUX
            if (_availabletimer != null)
                _availabletimer.Dispose();
#endif

#if SYSTEM_WINDOWS || SYSTEM_MACOSX
            if (_volwatcher != null)
                _volwatcher.Dispose();
#endif
        }

        void StartWatcher()
        {
            Trace("Starting FileSystemWatcher");

            if (_fswatcher != null) _fswatcher.Dispose();

#if SYSTEM_MACOSX
            _fswatcher = new FsEventsWatcher(_fullpath);
            _fswatcher.RootChanged += ev_RootChanged;
            _fswatcher.SubdirectoryChanged += ev_SubdirectoryChanged;
#else
            _fswatcher = new FileSystemWatcher();
            _fswatcher.Path = _fullpath;
            _fswatcher.IncludeSubdirectories = true;
            _fswatcher.NotifyFilter = NotifyFilters.FileName
                                  | NotifyFilters.DirectoryName
                                  | NotifyFilters.Attributes
                                  | NotifyFilters.Size
                                  | NotifyFilters.LastWrite
                                  | NotifyFilters.CreationTime
                                  | NotifyFilters.Security;
            _fswatcher.Changed += new FileSystemEventHandler(ev_FileChanged);
            _fswatcher.Created += new FileSystemEventHandler(ev_FileChanged);
            _fswatcher.Deleted += new FileSystemEventHandler(ev_FileChanged);
            _fswatcher.Renamed += new RenamedEventHandler(ev_FileRenamed);
            try {
                // this can throw if, e.g. dir went away between when we
                // checked for existence and now. don't bring down process.
                _fswatcher.EnableRaisingEvents = true;
            } catch {
                Trace("Failed to start FileSystemWatcher: directory is inaccessible");
                Exists = false;
            }
#endif
        }

        void StopWatcher()
        {
            Trace("Stopping FileSystemWatcher");
            if (_fswatcher != null) _fswatcher.Dispose();
            _fswatcher = null;
        }

#if SYSTEM_MACOSX || SYSTEM_WINDOWS
        void ev_VolumeChanged(VolumeWatcher sender, VolumeWatcherEvent evt, string volume)
        {
            if (!_fullpath.StartsWith(volume)) return;

            Trace("ev_VolumeChanged " + evt + ": " + volume);

            if (evt == VolumeWatcherEvent.DidMount)
            {
                _CheckAvailable(false);
            }
            else if (evt == VolumeWatcherEvent.WillUnmount || evt == VolumeWatcherEvent.DidUnmount)
            {
                _CheckAvailable(true); // must stop watcher on WillUnmount so we don't block unmount
            }
        }
#endif

        void FireAvailabilityChanged()
        {
            if (AvailabilityChanged != null)
                AvailabilityChanged(this, Path, Exists);
        }

        void _CheckAvailable(bool forceunavailable)
        {
            bool oldvalue = Exists;

            Exists = false;
            if (!forceunavailable)
            {
                try {
                    var di = new DirectoryInfo(_fullpath);
                    if (di.Exists) {
#if SYSTEM_WINDOWS
                        di.GetAccessControl(); // on windows, may throw if we have no access.
                                               // always seems to throw on mac...
#endif
                        Exists = true;
                    }
                } catch { }
            }

            if (oldvalue != Exists)
            {
                // do this before firing availability changed because if
                // starting watcher fails, it will mark dir as inaccessible.
                if (Exists) StartWatcher();
                else StopWatcher();

                FireAvailabilityChanged();
            }
        }

        static void Trace(string s) {
//            Console.WriteLine("[watcher] " + s);
        }

#if SYSTEM_MACOSX

        // FsEventsWatcher uses the CoreServices FSEvents API: your application's
        // main thread must be in a CoreFoundation run loop so the event stream
        // listener can be scheduled.
        public class FsEventsWatcher : IDisposable
        {
            public delegate void RootChangedDelegate(FsEventsWatcher sender, string rootpath, bool exists);
            public delegate void SubdirectoryChangedDelegate(FsEventsWatcher sender, string dirpath);

            List<string> _paths;
            IntPtr _stream;
            CoreServices.FSEventStreamCallback _callback;

            // watched path or one of its parents was created or deleted
            public event RootChangedDelegate RootChanged;

            // contained subdirectory changed
            public event SubdirectoryChangedDelegate SubdirectoryChanged;

            public FsEventsWatcher(string path) : this(new List<string>(){ path })
            {
            }

            public FsEventsWatcher(IEnumerable<string> paths)
            {
                _paths = new List<string>();
                foreach (string path in paths)
                    _paths.Add(System.IO.Path.GetFullPath(path));
                Init();
            }

            public void Dispose()
            {
                Trace(DateTime.Now + " FsEventsWatcher: stop stream");
                CoreServices.FSEventStreamStop(_stream);
                Trace(DateTime.Now + " FsEventsWatcher: invalidate stream");
                CoreServices.FSEventStreamInvalidate(_stream);
                Trace(DateTime.Now + " FsEventsWatcher: release stream");
                CoreServices.FSEventStreamRelease(_stream);
                _callback = null;
                Trace(DateTime.Now + " FsEventsWatcher: disposed");
            }

            void Init()
            {
                NSAutoreleasePool pool = new NSAutoreleasePool();

                _callback = new CoreServices.FSEventStreamCallback(OnFsEvent);

                var pathptrs = new List<IntPtr>();
                foreach (string path in _paths)
                    pathptrs.Add(CoreFoundation.CFSTR(new StringBuilder(path)));

                _stream = CoreServices.FSEventStreamCreate(IntPtr.Zero,
                                                           _callback,
                                                           IntPtr.Zero,
                                                           CoreFoundation.CFArrayCreate(pathptrs.ToArray()),
                                                           CoreServices.kFSEventStreamEventIdSinceNow,
                                                           1.0,
                                                           CoreServices.kFSEventStreamCreateFlagWatchRoot);

                CoreServices.FSEventStreamScheduleWithRunLoop(_stream,
                                                              RunLoopHelper.GetRunLoop(),
                                                              CoreFoundation.kCFRunLoopDefaultMode);

                CoreServices.FSEventStreamStart(_stream);

                //CoreServices.FSEventStreamShow(_stream);

                pool.Dispose();
            }

            void OnFsEvent(IntPtr stream, IntPtr userData,
                           UIntPtr numEvents,
                           string[] paths,
                           uint[] eventFlags,
                           ulong[] eventIds)
            {
                string rootchanged = null;

                for (int i = 0; i < (uint)numEvents; i++)
                {
                    Trace(String.Format("FsEventsWatcher: Changed {0} in {1}, flags {2}", eventIds[i], paths[i], eventFlags[i]));

                    string path = paths[i].TrimEnd(new char[]{ '/' });

                    if (eventFlags[i] == CoreServices.kFSEventStreamEventFlagRootChanged)
                    {
                        rootchanged = path;
                    }
                    else
                    {
                        if (SubdirectoryChanged != null)
                            SubdirectoryChanged(this, path);
                    }
                }

                if (rootchanged != null && RootChanged != null)
                    RootChanged(this, rootchanged, Directory.Exists(rootchanged) || File.Exists(rootchanged));
            }
        }

        void ev_RootChanged(FsEventsWatcher sender, string rootpath, bool exists)
        {
            if (!Exists && Directory.Exists(_fullpath))
            {
                Exists = true;
                FireAvailabilityChanged();
            }
            else if (Exists && !Directory.Exists(_fullpath))
            {
                Exists = false;
                FireAvailabilityChanged();
            }
        }

        void ev_SubdirectoryChanged(FsEventsWatcher sender, string dirpath)
        {
            if (_watchcontents)
                FireSubdirectoryChanged(dirpath);
        }

        void FireSubdirectoryChanged(string dirpath)
        {
            if (SubdirectoryChanged != null)
                SubdirectoryChanged(this, dirpath);
        }

#else

        void ev_FileChanged(object sender, FileSystemEventArgs e)
        {
            if (_watchcontents)
            {
                Trace(e.ChangeType + ": " + e.FullPath);
                FireWatcherEvent(e.ChangeType, e.FullPath);
            }
        }

        void ev_FileRenamed(object sender, RenamedEventArgs e)
        {
            if (_watchcontents)
            {
                if (e.FullPath.StartsWith(_fullpath) || e.OldFullPath.StartsWith(_fullpath))
                    Trace("Renamed: " + e.OldFullPath + " to " + e.FullPath);

                if (e.FullPath.StartsWith(_fullpath))
                    FireWatcherEvent(WatcherChangeTypes.Created, e.FullPath);

                if (e.OldFullPath.StartsWith(_fullpath))
                    FireWatcherEvent(WatcherChangeTypes.Deleted, e.OldFullPath);
            }
        }

        void FireWatcherEvent(WatcherChangeTypes type, string path)
        {
            switch (type)
            {
                case WatcherChangeTypes.Created:
                    if (FileAdded != null) FileAdded(this, path);
                    break;
                case WatcherChangeTypes.Deleted:
                    if (FileRemoved != null) FileRemoved(this, path);
                    break;
                case WatcherChangeTypes.Changed:
                    if (FileModified != null) FileModified(this, path);
                    break;
                default: throw new ArgumentException("watcher change type");
            }
        }
#endif
    }
}
