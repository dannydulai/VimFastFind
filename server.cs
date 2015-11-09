//#define USE_REGEX

using System;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace VimFastFind {
    abstract class Matcher : IDisposable {
        protected string _dir;
        protected List<string> _paths = new List<string>();

        public List<string> Paths { get { return _paths; } }

        List<MatchRule> _rules = new List<MatchRule>();
        DirectoryWatcher _fswatcher;

        class MatchRule {
            public bool Include;
            public bool Starts;
            public bool Ends;
            public string Value;

            public MatchRule(bool include, string v) {
                this.Include = include;

                if (v[0]          == '*') this.Ends   = true;
                if (v[v.Length-1] == '*') this.Starts = true;

                if (this.Starts && this.Ends)
                    this.Value = v.Substring(1, v.Length-2);
                else if (this.Starts)
                    this.Value = v.Substring(0, v.Length-1);
                else if (this.Ends)
                    this.Value = v.Substring(1);
                else
                    this.Value = v;
            }

            public bool Match(string e) {
//                Console.WriteLine("{0} vs {1}", e, Value);
                if (Starts && !Ends) return e.StartsWith(Value);
                if (Ends && !Starts) return e.EndsWith(Value);
                if (Ends && Starts) return e.IndexOf(Value) != -1;
                return e == Value;
            }
        }

        public bool IsFileOk(string name, bool onlyexclude=false) {
            foreach (var mr in _rules) {
                if (!onlyexclude && mr.Include) {
                    if (mr.Match(name))
                        return true;
                } else if (!mr.Include) {
                    if (mr.Match(name))
                        return false;
                }
            }
            return onlyexclude;
        }

        public string TrimPath(string fullpath) {
            if (_dir == fullpath) return "";
            return fullpath.Substring(_dir.Length+1);
        }

        public void Include(string e) {
            var mr = new MatchRule(true, e);
            _rules.Add(mr);
//            Console.WriteLine("+ {0}", e);
        }
        public void Exclude(string e) {
            var mr = new MatchRule(false, e);
            mr.Include = false;
            _rules.Add(mr);
//            Console.WriteLine("- {0}", e);
        }

        public string InitDir { get; private set; }
        public Matcher(string initdir) {
            this.InitDir = initdir;
        }

        public void Go(List<string> paths) {
#if PLATFORM_WINDOWS
            if (Directory.Exists("c:\\cygwin64")) {
                Utils.RunProcess("c:\\cygwin64\\bin\\cygpath.exe", "-w " + this.InitDir, out _dir);
            } else {
                Utils.RunProcess("c:\\cygwin\\bin\\cygpath.exe", "-w " + this.InitDir, out _dir);
            }
#else
            _dir = this.InitDir;
#endif
            _dir = _dir.Trim();
            while (_dir.Length > 0 && _dir[_dir.Length-1] == Path.DirectorySeparatorChar)
                _dir = _dir.Substring(0, _dir.Length-1);

//            Console.WriteLine("watching {0}", _dir);

            _fswatcher = new DirectoryWatcher(_dir);
            _fswatcher.EnableWatchingContents = true;
            _fswatcher.Initialize();

#if PLATFORM_WINDOWS
            _fswatcher.FileAdded += ev_FileChanged;
            _fswatcher.FileRemoved += ev_FileChanged;
            _fswatcher.FileModified += ev_FileChanged;
#elif PLATFORM_MACOSX
            _fswatcher.SubdirectoryChanged += ev_SubdirChanged;
#endif

            if (paths != null) {
                _paths = paths;
            } else {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                foreach (DirectoryEntry entry in FastDirectoryScanner.RecursiveScan(_dir, skipdir => !IsFileOk(TrimPath(skipdir), true))) {
                    if (entry.IsFile) {
                        string tp = TrimPath(entry.FullPath);
                        if (IsFileOk(tp)) _paths.Add(tp);
                    }
                }
                sw.Stop();
                Console.WriteLine("[{0}ms] {1} paths found on initial scan of {2}", sw.ElapsedMilliseconds, _paths.Count, this.InitDir);
            }
            OnPathsInited();
        }

        void ev_SubdirChanged(DirectoryWatcher source, string fulldirpath)
        {
            string dirpath = TrimPath(fulldirpath);
            if (dirpath.Length > 0 && dirpath[dirpath.Length-1] != Path.DirectorySeparatorChar)
                dirpath += Path.DirectorySeparatorChar;

            if (!Directory.Exists(fulldirpath)) {
//                Console.WriteLine("subdir removed: {0}", dirpath);
                lock (_paths) {
                    int i = 0;
                    while (i < _paths.Count) {
                        string f = _paths[i++];
                        if (f.StartsWith(dirpath)) {
                            _paths.RemoveAt(--i);
                            OnPathRemoved(f);
                        }
                    }
                }
            } else {
//                Console.WriteLine("subdir changed: {0}", dirpath);

                HashSet<string> filesindir = new HashSet<string>(Directory.GetFiles(fulldirpath).Where(x => IsFileOk(x)).Select(x => TrimPath(x)));

                lock (_paths) {
                    int i = 0;
                    while (i < _paths.Count) {
                        string path = _paths[i++];
                        string dir = Path.GetDirectoryName(path);
                        if (dir.Length > 0 && dir[dir.Length-1] != Path.DirectorySeparatorChar)
                            dir += Path.DirectorySeparatorChar;
                        if (dir == dirpath) {
                            if (filesindir.Contains(path)) {
                                OnPathChanged(path);
                            } else {
                                _paths.RemoveAt(--i);
                                OnPathRemoved(path);
                            }
                            filesindir.Remove(path);
                        }
                    }
                    foreach (string f in filesindir) {
                        _paths.Add(f);
                        OnPathAdded(f);
                    }
                }
            }
        }

        void ev_FileChanged(DirectoryWatcher source, string fullpath)
        {
            if (!IsFileOk(fullpath)) return;

//            Console.WriteLine("filechnaged: {0}", fullpath);

            if (File.Exists(fullpath)) {
                lock (_paths) {
                    string f = TrimPath(fullpath);
                    if (_paths.Contains(f)) {
                        OnPathChanged(f);
                    } else {
                        _paths.Add(f);
                        OnPathAdded(f);
                    }
                }
            } else {
                lock (_paths) {
                    string f = TrimPath(fullpath);
                    _paths.Remove(f);
                    OnPathRemoved(f);
                }
            }
        }

        protected virtual void OnPathsInited() { }
        protected virtual void OnPathAdded(string path) { }
        protected virtual void OnPathRemoved(string path) { }
        protected virtual void OnPathChanged(string path) { }
        protected virtual void OnPathRenamed(string p1, string p2) { }

        protected abstract bool DoMatch(string path, string needle, out int score, ref object obj, List<string> outs);

        public TopN<string> Match(string s, int count) {
            TopN<string> matches = new TopN<string>(count);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            LockFreeQueue<string> queue = new LockFreeQueue<string>();
            int queuecount;
            lock (_paths) {
                int i = 0;
                while (i < _paths.Count)
                    queue.Enqueue(_paths[i++]);
                queuecount = _paths.Count;
            }

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

            if (queuecount != 0) {
                int cpu = 0;
                while (cpu++ < Environment.ProcessorCount)
                    ThreadPool.QueueUserWorkItem(work);
                mre.WaitOne();
            }

            //                Console.WriteLine("{0}ms elapsed", sw.ElapsedMilliseconds);
            return matches;
        }

        public virtual void Dispose() {
            if (_fswatcher != null) {
                try { _fswatcher.Dispose(); } catch { }
                _fswatcher = null;
            }
        }

        int _refcnt = 1;
        public void Ref() {
            Interlocked.Increment(ref _refcnt);
        }
        public bool Free() {
            if (Interlocked.Decrement(ref _refcnt) == 0) {
                Dispose();
                return true;
            }
            return false;
        }
    }

    class PathMatcher : Matcher {
        public PathMatcher(string dir) : base(dir) { }
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

        bool dead;

        long _id;
        Dictionary<string, string> _contents = new Dictionary<string, string>();

        static GrepMatcher() {
            (new Thread(ev_read) { IsBackground = true }).Start();
        }

        public GrepMatcher(string dir) : base(dir) {
            _id = __id++;
            lock(__all) {
                __all[_id] = this;
            }
        }

        static void ev_read() {
            while (true) {
                KeyValuePair<GrepMatcher, string> kvp;
                while (__incomingfiles.Dequeue(out kvp)) {
                    if (kvp.Key.dead)
                        continue;
                    string file = Path.Combine(kvp.Key._dir, kvp.Value);
                    try {
                        if (!File.Exists(file)) continue;
                        using (StreamReader r = new StreamReader(file)) {
                            string v = r.ReadToEnd();
                            lock (kvp.Key._contents) {
                                kvp.Key._contents[kvp.Value] = v;
                            }
                        }

                    } catch (ArgumentException) {
                        // skipping because this is just a blank file

                    } catch (IOException e) {
                        try {
                            var fi = new FileInfo(file);
                            if (fi.Length != 0) {
                                Console.WriteLine("IO exception opening {0} for grepping: {1} ", kvp.Value, e);
                                __incomingfiles.Enqueue(kvp);
                            }
                        } catch { }

                    } catch (Exception e) {
                        Console.WriteLine("exception opening {0} for grepping: {1} ", kvp.Value, e);
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
//            Console.WriteLine("removing file {0}", Path.Combine(this._dir, path));
            lock (_contents) {
                _contents.Remove(path);
            }
        }
        protected override void OnPathChanged(string path) {
//            Console.WriteLine("changed file {0}", Path.Combine(this._dir, path));
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
//            Console.WriteLine("matching {0} against {1}", path, needle);
            string contents;
            lock (_contents) {
                if (!_contents.TryGetValue(path, out contents)) {
                    //                Console.WriteLine("{0} not found", path);
                    score = 0;
                    return false;
                }
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
        }

        public override void Dispose() {
//            Console.WriteLine("disposing grep {0}", _id);
            lock (__all) {
                __all.Remove(_id);
            }
            base.Dispose();
            dead = true;
        }
    }

    public class Client {
        static Dictionary<string, PathMatcher> __pathmatchercache = new Dictionary<string, PathMatcher>();
        static Dictionary<string, GrepMatcher> __grepmatchercache = new Dictionary<string, GrepMatcher>();

        PathMatcher _pathmatcher;
        GrepMatcher _grepmatcher;

        TcpClient _client;

        bool _ownspath;
        bool _ownsgrep;

        public Client(TcpClient client) {
            _client = client;
            (new Thread(ev_client) { IsBackground = true }).Start();
        }

        void ev_client() {
            try {
//                    Console.WriteLine("listening");
                using (Stream stream = _client.GetStream()) {
                    using (StreamWriter wtr = new StreamWriter(stream, Encoding.ASCII)) {
                        using (StreamReader rdr = new StreamReader(stream)) {

                            while (true) {
                                string line = rdr.ReadLine();
                                if (line == null) return;
//                            Console.WriteLine("got cmd {0}", line);

                                line = Regex.Replace(line, @"^\s*#.*", "");
                                if (String.IsNullOrWhiteSpace(line)) continue;
                                string[] s = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);


                                if (s[0] == "init") {
                                    s = line.Split(new char[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                                    lock (__pathmatchercache) {
                                        if (!__pathmatchercache.TryGetValue(s[1], out _pathmatcher)) {
                                            _pathmatcher = new PathMatcher(s[1]); 
                                            __pathmatchercache[s[1]] = _pathmatcher;
                                            _ownspath = true;
                                        } else {
                                            _pathmatcher.Ref();
                                        }
                                    }

                                    lock (__grepmatchercache) {
                                        if (!__grepmatchercache.TryGetValue(s[1], out _grepmatcher)) {
                                            _grepmatcher = new GrepMatcher(s[1]); 
                                            __grepmatchercache[s[1]] = _grepmatcher;
                                            _ownsgrep = true;
                                        } else {
                                            _grepmatcher.Ref();
                                        }
                                    }

                                } else if (s[0] == "go") {
//                                Console.WriteLine("go! {0}", s);
                                    if (_ownspath) _pathmatcher.Go(null);
                                    if (_ownsgrep) _grepmatcher.Go(_pathmatcher.Paths);


                                } else if (s[0] == "config") {
                                    if (s[1] == "include") {
                                        if (_ownspath) _pathmatcher.Include(s[2]);
                                        if (_ownsgrep) _grepmatcher.Include(s[2]);

                                    } else if (s[1] == "exclude") {
                                        if (_ownspath) _pathmatcher.Exclude(s[2]);
                                        if (_ownsgrep) _grepmatcher.Exclude(s[2]);
                                    }

                                } else if (s[0] == "grep" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
//                                Console.WriteLine("find! {0}", line);
                                    StringBuilder sb = new StringBuilder();
                                    int i = 0;
                                    foreach (string m in _grepmatcher.Match(s[2], 200)) {
                                        sb.Append(m);
                                        sb.Append("\n");
                                        i++;
                                    }
                                    //                                Console.WriteLine(sb.ToString());
                                    wtr.Write(sb.ToString());
                                    wtr.Write("\n");

                                } else if (s[0] == "find" && s[1] == "match") {
                                    s = line.Split(new char[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
                                    StringBuilder sb = new StringBuilder();
                                    int i = 0;
                                    foreach (string m in _pathmatcher.Match(s[2].ToLowerInvariant(), 200)) {
                                        sb.Append(m);
                                        sb.Append("\n");
                                        i++;
                                    }
                                    wtr.Write(sb.ToString());
                                    wtr.Write("\n");

                                } else if (s[0] == "nop") {
                                    wtr.Write("nop\n");
                                } else if (s[0] == "quit") {
                                    return;
                                } else {
                                }
                                wtr.Flush();
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("got exception {0}", ex.ToString());
            } finally {
                if (_client != null) {
                    try { _client.Close(); } catch { }
                    _client = null;
                }
                lock (__pathmatchercache) {
                    if (_pathmatcher != null) {
                        if (_pathmatcher.Free()) __pathmatchercache.Remove(_pathmatcher.InitDir);
                        _pathmatcher = null;
                    }
                }
                lock (__grepmatchercache) {
                    if (_grepmatcher != null) {
                        if (_grepmatcher.Free()) __grepmatchercache.Remove(_grepmatcher.InitDir);
                        _grepmatcher = null;
                    }
                }
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
