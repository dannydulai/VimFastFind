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

        List<MatchRule> _rules = new List<MatchRule>();
        DirectoryWatcher _fswatcher;

        class MatchRule {
            public bool Include;
            public string[] Values;
            public bool Match(string e) {
                int i = 0;
                int j = 0;
                while (j < Values.Length) {
                    var v = Values[j++];
                    if (v == "")
                        continue;
                    i = e.IndexOf(v, i);
                    if (i == -1)
                        return false;
                    i += v.Length;
                }
                if (Values[Values.Length-1] == "")
                    return true;
                return i == e.Length;
            }
        }

        bool IsFileOk(string name) {
            foreach (var mr in _rules) {
                if (mr.Include) {
                    if (mr.Match(name))
                        return true;
                } else {
                    if (mr.Match(name))
                        return false;
                }
            }
            return false;
        }

        public string TrimPath(string fullpath) {
            if (_dir == fullpath) return "";
            return fullpath.Substring(_dir.Length+1);
        }

        public void Include(string e) {
            _rules.Add(new MatchRule() { Include = true, Values = e.Split(new char[] { '*' }) });
//            Console.WriteLine("+ {0}", e);
        }
        public void Exclude(string e) {
            _rules.Add(new MatchRule() { Include = false, Values = e.Split(new char[] { '*' }) });
//            Console.WriteLine("- {0}", e);
        }

        public Matcher() {
        }

        public void Go(string dir) {
#if PLATFORM_WINDOWS
            if (Directory.Exists("c:\\cygwin64")) {
                Utils.RunProcess("c:\\cygwin64\\bin\\cygpath.exe", "-w " + dir, out _dir);
            } else {
                Utils.RunProcess("c:\\cygwin\\bin\\cygpath.exe", "-w " + dir, out _dir);
            }
#else
            _dir = dir;
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
                if (entry.IsFile && IsFileOk(TrimPath(entry.FullPath)))
                    _paths.Add(TrimPath(entry.FullPath));
            }
            OnPathsInited();
//            Console.WriteLine("{0} paths found on initial scan of {1}", _paths.Count, dir);
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

    unsafe class Map {
        // uses memmem on windows, which is why needle_len is needed. uses
        // strnstr on mac, though, so needle must be null-terminated.
        static unsafe sbyte* strnstr(sbyte* haystack, int haystack_len, sbyte* needle, int needle_len)
        {
#if PLATFORM_WINDOWS
            return memmem(haystack, (uint)haystack_len, needle, (uint)needle_len);
#elif PLATFORM_LINUX
            return memmem(haystack, new UIntPtr((uint)haystack_len), needle, new UIntPtr((uint)needle_len));
#elif PLATFORM_MACOSX
            return strnstr(haystack, needle, new UIntPtr((uint)haystack_len));
#else
#       error Unsupported Platform
#endif
        }

#if PLATFORM_WINDOWS
        // use uint for size_t here since this lib is always built 32-bit
        [DllImport("storagestringutils", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)]
        static extern sbyte * memmem(sbyte* haystack, uint haystack_len, sbyte* needle, uint needle_len);
#elif PLATFORM_LINUX
        // use UIntPtr for size_t here since we don't know how wide size_t is on this platform's libc
        [DllImport("libc", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)]
        static extern sbyte * memmem(sbyte* haystack, UIntPtr haystack_len, sbyte* needle, UIntPtr needle_len);
#elif PLATFORM_MACOSX
        // use UIntPtr for size_t here since we don't know how wide size_t is on this platform's libc
        [DllImport("libc", CharSet=CharSet.Ansi)]
        static unsafe extern sbyte* strnstr(sbyte* s1, sbyte* s2, UIntPtr n);
#else
#       error Unsupported Platform
#endif

        MemoryMappedFile mmf;
        MemoryMappedViewAccessor va;
        Microsoft.Win32.SafeHandles.SafeMemoryMappedViewHandle buf;
        int filelen;
        sbyte* ptr;

        public void Open(string file) {
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                mmf = MemoryMappedFile.CreateFromFile(fs, file, 0, MemoryMappedFileAccess.Read, null, HandleInheritability.None, true);
                va = mmf.CreateViewAccessor(0L, 0L, MemoryMappedFileAccess.Read);
                buf = va.SafeMemoryMappedViewHandle;
                byte *p = null;
                buf.AcquirePointer(ref p);
                ptr = (sbyte*)p;
                filelen = (int)va.Capacity;
            }
        }

        public void Close() {
            try { buf.ReleasePointer(); } catch { }
            try { buf.Dispose(); } catch { }
            try { va.Dispose(); } catch { }
            try { mmf.Dispose(); } catch { }
        }

        public static void FreeString(sbyte *s) {
            Marshal.FreeHGlobal(new IntPtr(s));
        }

        public int IndexOf(int idx, string s, ref sbyte* str) {
            if (idx >= filelen)
                return -1;

            if (str == null)
                str = (sbyte*)(Marshal.StringToHGlobalAnsi(s).ToPointer());

            sbyte* ret = strnstr(ptr + idx, (int)(filelen-idx), str, s.Length);
            if (ret == null)
                return -1;
            return (int)(ret - ptr);
        }

        public string GetLineAt(int idx, out int endidx) {
            if (idx >= filelen) {
                endidx = idx;
                return null;
            }

            unsafe {
                sbyte *startoffile = ptr;
                sbyte *endoffile = startoffile + filelen;

                sbyte *e = startoffile + idx;
                while (e < endoffile && *e != (sbyte)'\n') e++;

                sbyte *s = startoffile + idx;
                while (s >= startoffile && *s != (sbyte)'\n') s--;
                s++;
//                if (s != startoffile) s++;

                endidx = (int)(e - startoffile);
                return new string(s, 0, (int)(e-s), Encoding.UTF8);
            }
        }
    }

    class GrepMatcher : Matcher {
        static long __id = 0;
        static Dictionary<long, GrepMatcher> __all = new Dictionary<long, GrepMatcher>();
        static LockFreeQueue<KeyValuePair<GrepMatcher, string>> __incomingfiles = new LockFreeQueue<KeyValuePair<GrepMatcher, string>>();
        static AutoResetEvent __queuelock = new AutoResetEvent(false);

        bool dead;

        long _id;
        Dictionary<string, Map> _contents = new Dictionary<string, Map>();

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
                    if (kvp.Key.dead)
                        continue;
                    string file = Path.Combine(kvp.Key._dir, kvp.Value);
                    try {
                        if (!File.Exists(file)) continue;
                        Map m = new Map();
                        m.Open(file);
                        lock (kvp.Key._contents) {
                            Map o;
                            if (kvp.Key._contents.TryGetValue(kvp.Value, out o))
                                o.Close();
                            kvp.Key._contents[kvp.Value] = m;
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
                Map m = _contents[path];
                _contents.Remove(path);
                m.Close();
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
            Map contents;
            lock (_contents) {
                if (!_contents.TryGetValue(path, out contents)) {
                    //                Console.WriteLine("{0} not found", path);
                    score = 0;
                    return false;
                }
            }

            score = 0;
            bool ret = false;

            unsafe {
                sbyte* buf = null;
                int idx = 0;
                while (true) {
                    idx = contents.IndexOf(idx, needle, ref buf);
                    if (idx == -1) break;

                    int eidx;
                    string line = contents.GetLineAt(idx, out eidx);

                    outs.Add(path.Replace("\\", "/") + "(" + (idx+1).ToString() + "):" + line);
                    score = 100;
                    idx = eidx+1;
                    ret = true;
                }
                if (buf != null)
                    Map.FreeString(buf);
            }
            return ret;
        }

        public override void Dispose() {
//            Console.WriteLine("disposing grep {0}", _id);
            lock (__all) {
                __all.Remove(_id);
            }
            lock (_contents) {
                foreach (var kvp in _contents)
                    kvp.Value.Close();
            }
            base.Dispose();
            dead = true;
        }

        ~GrepMatcher() {
//            Console.WriteLine("FINALIZED {0}", _id);
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
                using (Stream stream = _client.GetStream()) {
                    using (StreamWriter wtr = new StreamWriter(stream, Encoding.ASCII)) {
                        using (StreamReader rdr = new StreamReader(stream)) {

                            while (true) {
                                string line = rdr.ReadLine();
                                if (line == null) return;
//                            Console.WriteLine("got cmd {0}", line);

                                line = Regex.Replace(line, @"^\s*#.*", "");
                                line = Regex.Replace(line, @"^\s*$", "");
                                if (line == "") continue;
                                string[] s = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);


                                if (s[0] == "go") {
                                    // we could cache this by path
//                                Console.WriteLine("go! {0}", s);
                                    s = line.Split(new char[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                                    _pathmatcher.Go(s[1]);
                                    _grepmatcher.Go(s[1]);

                                } else if (s[0] == "config") {
                                    if (s[1] == "include") {
                                        _pathmatcher.Include(s[2]);
                                        _grepmatcher.Include(s[2]);

                                    } else if (s[1] == "exclude") {
                                        _pathmatcher.Exclude(s[2]);
                                        _grepmatcher.Exclude(s[2]);

                                    } else if (s[1] == "find" && s[2] == "include") {
                                        s = line.Split(new char[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                                        _pathmatcher.Include(s[3]);

                                    } else if (s[1] == "find" && s[2] == "exclude") {
                                        s = line.Split(new char[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                                        _pathmatcher.Exclude(s[3]);

                                    } else if (s[1] == "grep" && s[2] == "include") {
                                        s = line.Split(new char[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                                        _grepmatcher.Include(s[3]);

                                    } else if (s[1] == "grep" && s[2] == "exclude") {
                                        s = line.Split(new char[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                                        _grepmatcher.Exclude(s[3]);
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
                if (_pathmatcher != null) {
                    _pathmatcher.Dispose();
                    _pathmatcher = null;
                }
                if (_grepmatcher != null) {
                    _grepmatcher.Dispose();
                    _grepmatcher = null;
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
