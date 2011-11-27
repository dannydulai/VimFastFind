//#define USE_REGEX

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace VimFastFind {
    abstract class Matcher : IDisposable {
        protected string _dir;
        protected List<string> _paths = new List<string>();
        Dictionary<string, bool> _plusextensions = new Dictionary<string, bool>();
        Dictionary<string, bool> _minusextensions = new Dictionary<string, bool>();
        FileSystemWatcher _fswatcher;

        bool IsFileOk(string name) {
            string ext = Path.GetExtension(name).ToLower();
            if (_plusextensions.Count != 0 && !_plusextensions.ContainsKey(ext))
                return false;
            else if (_minusextensions.Count != 0 && _minusextensions.ContainsKey(ext))
                return false;
            return true;
        }

        public string TrimPath(string fullpath) {
            return fullpath.Substring(_dir.Length+1);
        }

        public void IncludeExtension(string e) {
            _plusextensions["." + e.ToLower()] = true;
//            Console.WriteLine("+ {0}", e);
        }
        public void ExcludeExtension(string e) {
            _minusextensions["." + e.ToLower()] = true;
//            Console.WriteLine("- {0}", e);
        }

        public Matcher() {
        }

        public void Go(string dir) {
#if PLATFORM_WINDOWS
            Utils.RunProcess("c:\\cygwin\\bin\\cygpath.exe", "-w " + dir, out _dir);
#else
            _dir = dir;
#endif
            _dir = _dir.Trim();
//            Console.WriteLine("watching {0}", _dir);

            _fswatcher = new FileSystemWatcher();
            _fswatcher.Path = _dir;
            _fswatcher.IncludeSubdirectories = true;
            _fswatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            _fswatcher.Changed += ev_FileChanged;
            _fswatcher.Created += ev_FileChanged;
            _fswatcher.Deleted += ev_FileChanged;
            _fswatcher.Renamed += ev_FileRenamed;
            _fswatcher.EnableRaisingEvents = true;

            foreach (DirectoryEntry entry in
                     FastDirectoryScanner.RecursiveScan(_dir,
                                                        delegate(string skipdir) {
                                                        string name = skipdir.ToLower();
                                                        return
                                                        name == "tmp" ||
                                                        name == "bin" ||
                                                        name == "obj" ||
                                                        name == ".git" ||
                                                        name == ".svn" ||
                                                        name == "CVS" ||
                                                        name == "temp";
                                                        }))
            {
                if (entry.IsFile) {
                    if (IsFileOk(entry.Name)) {
                        _paths.Add(TrimPath(entry.FullPath));
                    }
                }
            }
            OnPathsInited();
//            Console.WriteLine("{0} paths found on initial scan of {1}", _paths.Count, dir);
        }

        void ev_FileChanged(object source, FileSystemEventArgs e)
        {
//                    Console.WriteLine("fc: {0}", e.ChangeType);
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    if (!File.Exists(e.FullPath)) return;
                    if (!IsFileOk(e.FullPath)) return;
                    lock (_paths) {
                        string f = TrimPath(e.FullPath);
                        _paths.Add(f);
                        OnPathAdded(f);
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    if (!IsFileOk(e.FullPath)) return;
                    lock (_paths) {
                        string f = TrimPath(e.FullPath);
                        _paths.Remove(f);
                        OnPathRemoved(f);
                    }
                    break;
                case WatcherChangeTypes.Changed:
//                    Console.WriteLine("CHANGED1");
                    if (!IsFileOk(e.FullPath)) return;
                    lock (_paths) {
                        string f = TrimPath(e.FullPath);
//                    Console.WriteLine("CHANGED2");
                        OnPathChanged(f);
                    }
                    break;
            }
        }

        void ev_FileRenamed(object source, RenamedEventArgs e)
        {
            lock (_paths) {
                string f1 = null, f2 = null;
                if (IsFileOk(e.OldFullPath)) {
                    f1 = TrimPath(e.OldFullPath);
                    _paths.Remove(f1);
                }
                if (IsFileOk(e.FullPath)) {
                    f2 = TrimPath(e.FullPath);
                    _paths.Add(f2);
                }
                if (f1 != null && f2 == null)
                    OnPathRemoved(f1);
                else if (f1 == null && f2 != null)
                    OnPathAdded(f2);
                else if (f1 != null && f2 != null)
                    OnPathRenamed(f1, f2);
            }
        }

        protected virtual void OnPathsInited() { }
        protected virtual void OnPathAdded(string path) { }
        protected virtual void OnPathRemoved(string path) { }
        protected virtual void OnPathChanged(string path) { }
        protected virtual void OnPathRenamed(string p1, string p2) { }

        protected abstract bool DoMatch(string path, string needle, out int score, ref object obj, List<string> outs);

        public TopN<string> Match(string s, int count) {
            lock (_paths) {
                TopN<string> matches = new TopN<string>(count);

                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();

                LockFreeQueue<string> queue = new LockFreeQueue<string>();
                int i = 0;
                while (i < _paths.Count)
                    queue.Enqueue(_paths[i++]);

                int queuecount = _paths.Count;

                ManualResetEvent mre = new ManualResetEvent(false);
                WaitCallback work = delegate {
                    string path;
                    List<string> outs = new List<string>();
                    object obj = null;
                    int score;
                    while (queue.Dequeue(out path)) {
                        if (DoMatch(path, s, out score, ref obj, outs)) {
                            lock (matches) {
                                foreach (string o in outs)
                                    matches.Add(score, o);
                            }
                        }
                        if (Interlocked.Decrement(ref queuecount) == 0)
                            mre.Set();
                        else
                            outs.Clear();
                    }
                };

                i = 0;
                while (i++ < Environment.ProcessorCount)
                    ThreadPool.QueueUserWorkItem(work);

                mre.WaitOne();

//                Console.WriteLine("{0}ms elapsed", sw.ElapsedMilliseconds);
                return matches;
            }
        }

        public virtual void Dispose() {
            if (_fswatcher != null) {
                try { _fswatcher.Dispose(); } catch { }
                _fswatcher = null;
            }
        }
    }

    class PathMatcher : Matcher {

        protected override void OnPathsInited() {
            _paths.Sort();
        }
        protected override void OnPathAdded(string path) {
            _paths.Sort();
        }

        protected override bool DoMatch(string path, string needle, out int score, ref object obj, List<string> outs) {
            int i = needle.Length-1;
            int j = path.Length-1;
            score = 0;
            bool match = false;
            while (i >= 0 && j >= 0) {
                if (Char.ToLowerInvariant(path[j]) == needle[i]) {
                    i--;
                    score++;
                    if (match)
                        score++;
                    match = true;
                } else
                    match = false;
                j--;
            }

            if (i == -1) {
                if (j >= 0)
                    if (path[j] == '/' || path[j] == '\\')
                        score++;

                outs.Add(path.Replace("\\", "/"));
                return true;
            }

            return false;
        }

    }

    class GrepMatcher : Matcher {
        static long __id = 0;
        static Dictionary<long, GrepMatcher> __all = new Dictionary<long, GrepMatcher>();
        static LockFreeQueue<KeyValuePair<GrepMatcher, string>> __incomingfiles = new LockFreeQueue<KeyValuePair<GrepMatcher, string>>();
        static AutoResetEvent __queuelock = new AutoResetEvent(false);

        int total;

        long _id;
        Dictionary<string, string> _contents = new Dictionary<string, string>();

        static GrepMatcher() {
            (new Thread(ev_read) { IsBackground = true }).Start();
        }

        public GrepMatcher() {
            _id = __id++;
            lock(__all) {
                __all[_id] = this;
            }
        }

        static void ev_read() {
            while (true) {
                KeyValuePair<GrepMatcher, string> kvp;
                while (__incomingfiles.Dequeue(out kvp)) {
                    try {
                        string file = Path.Combine(kvp.Key._dir, kvp.Value);
                        if (!File.Exists(file)) continue;
                        using (StreamReader r = new StreamReader(file)) {
                            string v = r.ReadToEnd();
                            kvp.Key.total += v.Length;
                            lock (kvp.Key._contents) {
                                kvp.Key._contents[kvp.Value] = v;
                            }
                        }
                    } catch (IOException) {
                        __incomingfiles.Enqueue(kvp);
                        Thread.Sleep(100);

                    } catch (Exception e) {
                        Console.WriteLine(e.ToString());
                    }
                }
                __queuelock.WaitOne();
            }
        }

        protected override void OnPathsInited() {
            foreach (string f in _paths) {
//                Console.WriteLine("adding to incoming file {0}", Path.Combine(this._dir, f));
                __incomingfiles.Enqueue(new KeyValuePair<GrepMatcher, string>(this, f));
            }
            __queuelock.Set();
        }
        protected override void OnPathAdded(string path) {
//                Console.WriteLine("adding to incoming file {0}", Path.Combine(this._dir, path));
            __incomingfiles.Enqueue(new KeyValuePair<GrepMatcher, string>(this, path));
            __queuelock.Set();
        }
        protected override void OnPathRemoved(string path) {
            lock (_contents) {
                _contents.Remove(path);
            }
        }
        protected override void OnPathChanged(string path) {
//                    Console.WriteLine("CHANGED3 {0}", path);
            __incomingfiles.Enqueue(new KeyValuePair<GrepMatcher, string>(this, path));
            __queuelock.Set();
        }
        protected override void OnPathRenamed(string p1, string p2) {
            lock (_contents) {
                if (_contents.ContainsKey(p1)) {
                    _contents[p2] = _contents[p1];
                    _contents.Remove(p1);
                }
            }
        }
        protected override bool DoMatch(string path, string needle, out int score, ref object obj, List<string> outs) {
#if USE_REGEX
            Regex rx = null;
            if (obj == null) {
                rx = new Regex(needle, RegexOptions.IgnoreCase);
                obj = rx;
            } else {
                rx = (Regex)obj;
            }

            string contents;
            if (!_contents.TryGetValue(path, out contents)) {
                score = 0;
                return false;
            }
            MatchCollection matches = rx.Matches(contents);
            if (matches.Count > 0) {
                score = 100;
                return true;
            } else {
                score = 0;
                return false;
            }
#else
//                        Console.WriteLine("matching {0} against {1}", path, needle);
            string contents;
            if (!_contents.TryGetValue(path, out contents)) {
//                        Console.WriteLine("{0} not found", path);
                score = 0;
                return false;
            }

            score = 0;
            bool ret = false;

            int idx = 0;
            while (true) {
                idx = contents.IndexOf(needle, idx, StringComparison.Ordinal);
                if (idx == -1) break;

                int oidx = idx;

                int eidx = idx;
                while (eidx < contents.Length && contents[eidx] != '\n') eidx++;
                if (eidx != contents.Length) eidx++;

                while (idx > 0 && contents[idx] != '\n') idx--;
                if (contents[idx] == '\n') idx++;

                outs.Add(path.Replace("\\", "/") + "(" + (oidx+1) + "):" + contents.Substring(idx, eidx-idx-1));
                score = 100;
                idx = eidx;
                ret = true;
                if (idx+1 >= contents.Length) break;
            }
            return ret;
#endif
        }

        public override void Dispose() {
            lock (__all) {
                __all.Remove(_id);
            }
            base.Dispose();
        }
    }

    public class Client {
        PathMatcher _pathmatcher = new PathMatcher();
        GrepMatcher _grepmatcher = new GrepMatcher();

        TcpClient _client;

        public Client(TcpClient client) {
            _client = client;
            (new Thread(ev_client) { IsBackground = true }).Start();
        }

        void ev_client() {
            try {
//                    Console.WriteLine("listening");
                Stream stream = _client.GetStream();
                using (StreamWriter wtr = new StreamWriter(stream, Encoding.ASCII)) {
                    using (StreamReader rdr = new StreamReader(stream)) {

                        while (true) {
                            string s = rdr.ReadLine();
                            if (s == null) return;

//                            Console.WriteLine("got cmd {0}", s);

                            if (s.StartsWith("r")) {
                                // we could cache this by path
//                                Console.WriteLine("go! {0}", s);
                                _pathmatcher.Go(s.Substring(1));
                                _grepmatcher.Go(s.Substring(1));

                            } else if (s.StartsWith("i")) {
                                _pathmatcher.IncludeExtension(s.Substring(1));

                            } else if (s.StartsWith("e")) {
                                _pathmatcher.ExcludeExtension(s.Substring(1));

                            } else if (s.StartsWith("grep_i")) {
                                _grepmatcher.IncludeExtension(s.Substring(6));

                            } else if (s.StartsWith("grep_e")) {
                                _grepmatcher.ExcludeExtension(s.Substring(6));

                            } else if (s.StartsWith("grep_f")) {
//                                Console.WriteLine("find! {0}", s);
                                StringBuilder sb = new StringBuilder();
                                int i = 0;
                                foreach (string m in _grepmatcher.Match(s.Substring(6), 200)) {
                                    sb.Append(m);
                                    sb.Append("\n");
                                    i++;
                                }
//                                Console.WriteLine(sb.ToString());
                                wtr.Write(sb.ToString());
                                wtr.Write("\n");

                            } else if (s.StartsWith("f")) {
                                StringBuilder sb = new StringBuilder();
                                int i = 0;
                                foreach (string m in _pathmatcher.Match(s.Substring(1).ToLowerInvariant(), 200)) {
                                    sb.Append(m);
                                    sb.Append("\n");
                                    i++;
                                }
                                wtr.Write(sb.ToString());
                                wtr.Write("\n");

                            } else if (s.StartsWith("n")) {
                                wtr.Write("n\n");
                            } else if (s.StartsWith("q")) {
                                return;
                            } else {
                                wtr.Write("ERROR\n");
                            }
                            wtr.Flush();
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("got exception {0}", ex.ToString());
            } finally {
                try { _client.Close(); } catch { }
                _client = null;
                if (_pathmatcher != null)
                    _pathmatcher.Dispose();
                if (_grepmatcher != null)
                    _grepmatcher.Dispose();
            }
        }
    }

    public class Server {
        static int Port = 20398;

        static void Usage() {
            Console.WriteLine();
            Console.WriteLine("usage: VFFServer [-port=PORTNUMBER]");
            Console.WriteLine();
            Console.WriteLine("       Default port is 20398");
            Console.WriteLine();
            Environment.Exit(1);
        }

        public static void Main(string[] args) {
            ThreadPool.QueueUserWorkItem(delegate { });

            if (args.Length != 0) {
                if (args[0].StartsWith("-port=")) {
                    Port = Convert.ToInt32(args[0].Substring(6));
                } else {
                    Usage();
                    return;
                }
            }

            TcpListener listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (true) {
                try {
                    TcpClient client = listener.AcceptTcpClient();
                    new Client(client);
                } catch { }
            }
        }
    }
}
