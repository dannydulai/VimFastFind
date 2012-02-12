using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

#if HAVE_MONO_POSIX
using Mono.Unix.Native;
#endif

namespace VimFastFind
{
    public class Utils {
        static public int RunProcess(string f, string a, out string output)
        {
            using (Process p = new Process()) {
                p.StartInfo.FileName = f;
                p.StartInfo.Arguments = a;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode;
            }
        }
    }
    public struct DirectoryEntry
    {
        public string Name { get; private set; }
        public string FullPath { get; private set; }
        public long ModificationTime { get; private set; }
        public bool IsFile { get; private set; }
        public bool IsDirectory { get { return !IsFile; } }

        public DirectoryEntry(string parentdir, string name, long modtime, bool isfile) : this()
        {
            Name = name;
            FullPath = Path.Combine(parentdir, name);
            ModificationTime = modtime;
            IsFile = isfile;
        }

        public DirectoryEntry(string fullpath, long modtime, bool isfile) : this()
        {
            Name = fullpath.Substring(fullpath.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            FullPath = fullpath;
            ModificationTime = modtime;
            IsFile = isfile;
        }

        public override string ToString()
        {
            return String.Format("Entry[Path={0},ModificationTime={1},IsFile={2},IsDirectory={3}]", FullPath, ModificationTime, IsFile, IsDirectory);
        }
    }

    public static class FastDirectoryScanner
    {
        // NOTE: scan methods may throw. see _ThrowException for details.

        public static IList<DirectoryEntry> Scan(string path)
        {
            return Scan(path, false, null);
        }

        // cb_shouldskipdir will be called with each directory's name (not full path). if true is
        // returned that directory will be descended recursive .
        //
        public static IList<DirectoryEntry> RecursiveScan(string path, Predicate<string> cb_shouldskipdir)
        {
            return Scan(path, true, cb_shouldskipdir);
        }

#if PLATFORM_WINDOWS

        static IList<DirectoryEntry> Scan(string path, bool recurse, Predicate<string> cb_shouldskipdir)
        {
            if (path.Length == 2 && path[1] == ':') path = path + '\\';
            var ret = new List<DirectoryEntry>();
            WIN32_FIND_DATA ffd = new WIN32_FIND_DATA();
            int rc;

            IntPtr handle = FindFirstFile(Path.Combine(path, "*"), ref ffd);
            if (handle == INVALID_HANDLE_VALUE)
            {
                rc = Marshal.GetLastWin32Error();
                if (rc == ERROR_FILE_NOT_FOUND)
                    return ret;
                _ThrowException(rc, path);
            }

            do
            {
                DirectoryEntry? entry = _GetEntry(path, ffd);
                if (entry == null)
                    continue;

                if (recurse &&
                    entry.Value.IsDirectory &&
                    cb_shouldskipdir != null && cb_shouldskipdir(entry.Value.Name))
                    continue;

                ret.Add(entry.Value);

                if (entry.Value.IsDirectory && recurse)
                    ret.AddRange(Scan(Path.Combine(path, entry.Value.Name), recurse, cb_shouldskipdir));
            }
            while (FindNextFile(handle, ref ffd));

            rc = Marshal.GetLastWin32Error();
            FindClose(handle);
            if (rc != 0 && rc != ERROR_NO_MORE_FILES)
                _ThrowException(rc, path);

            return ret;
        }

        static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        const int ERROR_FILE_NOT_FOUND = 0x2;
        const int ERROR_NO_MORE_FILES = 0x12;
        const long TIME_ADJUSTMENT = 504911232000000000; // adjust from FILETIME ticks to DateTime ticks

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto), BestFitMapping(false)]
        struct WIN32_FIND_DATA {
            public int FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public int FileSizeHigh;
            public int FileSizeLow;
            public int Reserved0;
            public int Reserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string FileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string AlternateFileName;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FILETIME {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr FindFirstFile(string pFileName, ref WIN32_FIND_DATA pFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool FindNextFile(IntPtr hndFindFile, ref WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FindClose(IntPtr hndFindFile);

        static DirectoryEntry? _GetEntry(string parentdir, WIN32_FIND_DATA ffd)
        {
            bool isdir = (ffd.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
            if (isdir && (ffd.FileName == "." || ffd.FileName == ".."))
                return null;
            long modtime = (((long)ffd.LastWriteTime.HighDateTime << 32) | ffd.LastWriteTime.LowDateTime) + TIME_ADJUSTMENT;
            return new DirectoryEntry(parentdir, ffd.FileName, modtime, !isdir);
        }

        static void _ThrowException(int errorCode, string str)
        {
            switch (errorCode)
            {
                case   80: throw new IOException("FileExists: " + str);
                case 0x57: throw new IOException("IO Error: " + errorCode);
                case 0xCE: throw new PathTooLongException("PathTooLong: " + str );
                case    2: throw new FileNotFoundException("FileNotFound: " + str);
                case    3: throw new DirectoryNotFoundException("PathNotFound: " + str);
                case    5: throw new UnauthorizedAccessException("UnauthorizedAccess: " + str);
                case 0x20: throw new IOException("SharingViolation: " + str);
                default  : throw new IOException("IO Error: " + errorCode);
            }
        }

#else
#if !HAVE_MONO_POSIX
        static readonly long EPOCH_TICKS = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).Ticks;
#endif

        unsafe static IList<DirectoryEntry> Scan(string path, bool recurse, Predicate<string> cb_shouldskipdir)
        {
            if (recurse)
            {
                bool first = true;
                IntPtr str = Marshal.StringToHGlobalAnsi(path);
                try
                {
                    IntPtr[] arr = new IntPtr[] { str, IntPtr.Zero };
                    IntPtr handle;
                    fixed (IntPtr *arg = arr)
                        handle = fts_open(new IntPtr(arg), FTS_NOCHDIR | FTS_PHYSICAL, IntPtr.Zero);
                    if (handle == IntPtr.Zero) throw new Exception("failed to fts_open");

                    try
                    {
                        FTSENT *ent = fts_read(handle);
                        if (ent == null)
                            return __empty;

                        List<DirectoryEntry> ret = new List<DirectoryEntry>();
                        while (ent != null)
                        {
                            //Console.WriteLine("ent: " + ent->ToString());
                            bool isfile;
                            if (ent->fts_info == FTS_F) {
                                isfile = true;
                            } else if (ent->fts_info == FTS_D) {
                                isfile = false;
                            } else {
                                ent = fts_read(handle);
                                continue;
                            }
                            bool skip = false;
                            if (!isfile && cb_shouldskipdir != null)
                            {
                                string name = Marshal.PtrToStringAnsi(new IntPtr((byte*)ent->fts_name));
                                if (cb_shouldskipdir != null && cb_shouldskipdir(name)) {
                                    fts_set(handle, ent, FTS_SKIP);
                                    skip = true;
                                }
                            }
                            if (!skip)
                            {
                                string fullpath = Marshal.PtrToStringAnsi(ent->fts_path);
                                if (first) {
                                    first = false;
                                } else {
                                    long mtime = (long)ent->fts_statp->st_mtimespec.tv_sec;
                                    ret.Add(new DirectoryEntry(fullpath, mtime, isfile));
                                }
                            }
                            ent = fts_read(handle);
                        }
                        return ret;
                    }
                    finally
                    {
                        fts_close(handle);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(str);
                }
            }
            else
            {
#if !HAVE_MONO_POSIX
                // I've verified that this returns the exact same timestamps as the other branch of code
                // on linux + mac. In theory, this should perform just as well, but unsure. We can optimize
                // further later with p/invoke if necessary
                var ret = new List<DirectoryEntry>();
                DirectoryInfo info = new DirectoryInfo(path);
                foreach (var entry in info.EnumerateFileSystemInfos()) {
                    var attrs = entry.Attributes & ~(FileAttributes.Hidden | FileAttributes.ReadOnly);
                    if ((attrs & FileAttributes.Directory) != 0) {
                        ret.Add(new DirectoryEntry(path, entry.FullName, 0, false));
                    } else {
                        ret.Add(new DirectoryEntry(path, entry.FullName, (entry.LastWriteTimeUtc.Ticks - EPOCH_TICKS) / 10000000, true));
                    }
                }
                return ret;
#else
                var ret = new List<DirectoryEntry>();

                IntPtr dirp = Syscall.opendir(path);
                if (dirp == IntPtr.Zero)
                    throw new Exception("opendir: error " + Stdlib.GetLastError());

                try
                {
                    do
                    {
                        Dirent d = Syscall.readdir(dirp);
                        if (d == null)
                        {
                            Errno rc = Stdlib.GetLastError();
                            if (rc == Errno.ENOENT || rc == 0) break; // done
                            throw new Exception("readdir: error " + rc);
                        }

                        if (d.d_type == READDIR_ISFILE)
                        {
                            Stat st;
                            string fn = Path.Combine(path, d.d_name);
                            if (Syscall.stat(fn, out st) != 0)
                                throw new Exception("stat \"" + fn + "\": error " + Stdlib.GetLastError());

                            ret.Add(new DirectoryEntry(path, d.d_name, st.st_mtime, true));
                        }
                        else if (d.d_type == READDIR_ISDIRECTORY)
                        {
                            if (d.d_name == "." || d.d_name == "..") continue;
                            ret.Add(new DirectoryEntry(path, d.d_name, 0, false));
                        }
                    }
                    while (dirp != IntPtr.Zero);
                }
                finally
                {
                    Syscall.closedir(dirp);
                }

                return ret;
#endif
            }
        }

        static DirectoryEntry[] __empty = new DirectoryEntry[0];

        const int FTS_NOCHDIR = 0x4;
        const int FTS_PHYSICAL = 0x10;

        const int FTS_SKIP = 4;
        const ushort FTS_F = 8;
        const ushort FTS_DC = 2;
        const ushort FTS_D = 1;
        const ushort FTS_DP = 6;

        const byte READDIR_ISFILE = 0x8;
        const byte READDIR_ISDIRECTORY = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        struct timespec {
            public int tv_sec;
            public int tv_nsec;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct stat { /* abridged */
            public /*dev_t*/ uint st_dev;
            public /*ino_t*/ uint st_ino;
            public /*mode_t*/ ushort st_mode;
            public /*nlink_t*/ ushort st_nlink;
            public ushort st_uid;
            public ushort st_gid;
            public ushort st_rdev;
            public uint st_size;
            public timespec st_atimespec;
            public timespec st_mtimespec;
            public timespec st_ctimespec;
        }

        [StructLayout(LayoutKind.Sequential)]
        unsafe struct FTSENT {
            public FTSENT *fts_cycle;
            public FTSENT *fts_parent;
            public FTSENT *fts_link;
            public int fts_number;
            public /*void*/IntPtr fts_pointer;
            public /*char*/IntPtr fts_accpath;
            public /*char*/IntPtr fts_path;
            public int fts_errno;
            int fts_symfd;
            public ushort fts_pathlen;
            public ushort fts_namelen;

            /*ino_t*/ uint fts_ino;
            /*dev_t*/ uint fts_dev;
            /*nlink_t*/ ushort fts_nlink;

            public short fts_level;
            public ushort fts_info;
            public ushort fts_flags;
            public ushort fts_instr;

            public stat *fts_statp;
            public fixed byte fts_name[4096];

            public override string ToString() {
                return String.Format(@"FTSENT[
                    fts_cycle       = 0x{0}
                    fts_parent      = 0x{1}
                    fts_link        = 0x{2}
                    fts_number      = 0x{3}
                    fts_pointer     = 0x{4}
                    fts_accpath     = 0x{5} ({21})
                    fts_path        = 0x{6} ({20})
                    fts_errno       = 0x{7}
                    fts_symfd       = {8}
                    fts_pathlen     = {9}
                    fts_namelen     = {10}
                    fts_ino         = {11}
                    fts_dev         = 0x{12}
                    fts_nlink       = {13}
                    fts_level       = {14}
                    fts_info        = 0x{15}
                    fts_flags       = 0x{16}
                    fts_instr       = 0x{17}
                    fts_statp       = 0x{18}
                    fts_name        = {19}
                ]",
                new IntPtr(fts_cycle).ToString("x"),
                new IntPtr(fts_parent).ToString("x"),
                new IntPtr(fts_link).ToString("x"),
                fts_number.ToString("x"),
                fts_pointer.ToString("x"),
                fts_accpath.ToString("x"),
                fts_path.ToString("x"),
                fts_errno.ToString("x"),
                fts_symfd,
                fts_pathlen,
                fts_namelen,
                fts_ino,
                fts_dev.ToString("x"),
                fts_nlink,
                fts_level,
                fts_info.ToString("x"),
                fts_flags.ToString("x"),
                fts_instr.ToString("x"),
                new IntPtr(fts_statp).ToString("x"),
                "<noname>",
                fts_path == IntPtr.Zero ? "(null)" : Marshal.PtrToStringAnsi(fts_path),
                fts_accpath == IntPtr.Zero ? "(null)" : Marshal.PtrToStringAnsi(fts_accpath));
            }
        }

        [DllImport("libc")]
        static extern IntPtr fts_open(/* char** */IntPtr path_argv, int options, IntPtr comparer);

        [DllImport("libc")]
        unsafe static extern FTSENT *fts_set(IntPtr fts_handle, FTSENT *ent, int options);

        [DllImport("libc")]
        unsafe static extern FTSENT *fts_read(IntPtr fts_handle);

        [DllImport("libc")]
        static extern int fts_close(IntPtr fts_handle);

#endif
    }

    internal class SingleLinkNode<T> {
      // Note; the Next member cannot be a property since it participates in
      // many CAS operations
      public SingleLinkNode<T> Next; 
      public T Item;
    }
    internal static class SyncMethods {
        public static bool CAS<T>(ref T location, T comparand, T newValue) where T : class {
            return
                (object) comparand ==
                (object) Interlocked.CompareExchange<T>(ref location, newValue, comparand);
        }
    }
    public class LockFreeQueue<T> {
        SingleLinkNode<T> head;
        SingleLinkNode<T> tail;

        public LockFreeQueue() {
            head = new SingleLinkNode<T>();
            tail = head;
        }

        public void Enqueue(T item) {
            SingleLinkNode<T> oldTail = null;
            SingleLinkNode<T> oldTailNext;

            SingleLinkNode<T> newNode = new SingleLinkNode<T>();
            newNode.Item = item;

            bool newNodeWasAdded = false;
            while (!newNodeWasAdded) {
                oldTail = tail;
                oldTailNext = oldTail.Next;

                if (tail == oldTail) {
                    if (oldTailNext == null)
                        newNodeWasAdded = SyncMethods.CAS<SingleLinkNode<T>>(ref tail.Next, null, newNode);
                    else
                        SyncMethods.CAS<SingleLinkNode<T>>(ref tail, oldTail, oldTailNext);
                }
            }

            SyncMethods.CAS<SingleLinkNode<T>>(ref tail, oldTail, newNode);
        }

        public bool Dequeue(out T item) {
            item = default(T);
            SingleLinkNode<T> oldHead = null;

            bool haveAdvancedHead = false;
            while (!haveAdvancedHead) {
                oldHead = head;
                SingleLinkNode<T> oldTail = tail;
                SingleLinkNode<T> oldHeadNext = oldHead.Next;

                if (oldHead == head) {
                    if (oldHead == oldTail) {
                        if (oldHeadNext == null) {
                            return false;
                        }
                        SyncMethods.CAS<SingleLinkNode<T>>(ref tail, oldTail, oldHeadNext);
                    } else {
                        item = oldHeadNext.Item;
                        haveAdvancedHead = 
                            SyncMethods.CAS<SingleLinkNode<T>>(ref head, oldHead, oldHeadNext);
                    }
                }
            }
            return true;
        }

        public T Dequeue() {
            T result;
            Dequeue(out result);
            return result;
        }
    }

    public class TopN<T> : IEnumerable<T> where T : class {
        class Container : IComparable<Container> {
            public Container(double rel, T item) { Relevance = rel; Item = item; }
            public double Relevance;
            public T Item;
            public int CompareTo(Container other) {
                int xx = other.Relevance.CompareTo(Relevance);
                if (xx == 0) {
                    if (Item is IComparable<T> && other.Item is IComparable<T>)
                        return ((IComparable<T>)other.Item).CompareTo(Item);
                    else
                        return other.Item.GetHashCode().CompareTo(Item.GetHashCode());
                }
                return xx;
            }
            public override bool Equals(object obj) {
                return Item == ((Container)obj).Item;
            }
            public override int GetHashCode() {
                return Item.GetHashCode();
            }
        }

        double _minrel = 0.0;
        SortedSet<Container> _list = new SortedSet<Container>();
        int _n;

        public TopN(int n) {
            _n = n;
        }

        public void Add(double rel, T item) {
            if (_n == 0) return;
            if (_list.Count < _n) {
                _list.Add(new Container(rel, item));
                _minrel = _list.Max.Relevance;
            } else if (rel > _minrel) {
                _list.Remove(_list.Max);
                _list.Add(new Container(rel, item));
                _minrel = _list.Max.Relevance;
            }
        }

        public IEnumerator<T> GetEnumerator() {
            foreach (Container t in _list) yield return t.Item;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            foreach (Container t in _list) yield return t.Item;
        }
    }

}
