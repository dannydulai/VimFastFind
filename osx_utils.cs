using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OSXUtils
{
    public static class RunLoopHelper
    {
        public static bool ApplicationWillSpinMainRunLoop;

        public static IntPtr GetRunLoop()
        {
            if (ApplicationWillSpinMainRunLoop)
            {
                //Console.WriteLine("RunloopHelper: main thread is already running a loop");
                return CoreFoundation.CFRunLoopGetMain();
            }
            else
            {
                //Console.WriteLine("RunloopHelper: creating run loop in bg thread");

                IntPtr runloop;
                var mre = new ManualResetEvent(false);

                new Thread((ThreadStart)delegate
                {
                    runloop = CoreFoundation.CFRunLoopGetCurrent();
                    mre.Set();
                    while (true) {
                        CoreFoundation.CFRunLoopRun();
                        Thread.Sleep(500);
                    }
                }) { IsBackground = true }.Start();

                mre.WaitOne();
                return runloop;
            }
        }
    }

    public static class ApplicationHelper
    {
        static object _lock = new object();
        static bool _islaunched = false;
        static bool _willLaunch = false;
        static bool _windowDisplayed = false;

        // the NSApplicationDelegate (in our case this is the OpenGlWindow) should set this to true
        // when the applicationDidFinishLaunching delegate method is called.
        public static bool IsLaunched
        {
            get {
                return _islaunched;
            }
            set {
                if (value != true)
                    throw new InvalidOperationException("property can only bet set to true");
                lock (_lock)
                    _islaunched = value;
                if (OnLaunched != null)
                    OnLaunched();
            }
        }

        // Like above, but happens on applicationWillFinishLaunching
        // This gives you an opertunity to run Cocoa code while the app
        // is still initiliazing but after you have a proper event loop, shared libs loaded etc
        public static bool WillLaunch
        {
            get {
                return _willLaunch;
            }
            set {
                if (value != true)
                    throw new InvalidOperationException("property can only bet set to true");
                lock (_lock)
                    _willLaunch = value;
                if (OnWillLaunch != null)
                    OnWillLaunch();
            }
        }

        public static bool WindowDisplayed
        {
            get {
                return _windowDisplayed;
            }
            set {
                if (value != true)
                    throw new InvalidOperationException("property can only bet set to true");
                lock (_lock)
                    _windowDisplayed = value;
                if (OnWindowDisplayed != null)
                    OnWindowDisplayed();
            }
        }

        public delegate void LaunchDelegate();

        // fired when IsLaunched is set true
        public static event LaunchDelegate OnLaunched;
        public static event LaunchDelegate OnWillLaunch;
        public static event LaunchDelegate OnWindowDisplayed;

        public static void ExecuteWhenWindowDisplayed(Action action)
        {
            lock (_lock)
            {
                if (_windowDisplayed)
                {
                    action();
                }
                else
                {
                    LaunchDelegate d = null;
                    d = delegate {
                        action();
                        OnWindowDisplayed -= d;
                    };
                    OnWindowDisplayed += d;
                }
            }
        }

        public static void ExecuteWhenLaunched(Action action)
        {
            lock (_lock)
            {
                if (_islaunched)
                {
                    action();
                }
                else
                {
                    LaunchDelegate d = null;
                    d = delegate {
                        action();
                        OnLaunched -= d;
                    };
                    OnLaunched += d;
                }
            }
        }

        public static void ExecuteBeforeLaunch(Action action)
        {
               if (_willLaunch)
                {
                    action();
                }
                else
                {
                    LaunchDelegate d = null;
                    d = delegate {
                        action();
                        OnWillLaunch -= d;
                    };
                    OnWillLaunch += d;
                }
        }
    }

    public static class Globals
    {
        [DllImport("libdl")] static extern IntPtr dlopen(string what, int mode);
        [DllImport("libdl")] static extern IntPtr dlsym(IntPtr what, string name);

        static Dictionary<string, IntPtr> _libs;

        private static IntPtr _LoadLib(string libname)
        {
            if (_libs == null)
                _libs = new Dictionary<string, IntPtr>();

            if (_libs.ContainsKey(libname))
                return _libs[libname];

            IntPtr p = dlopen(libname, 5);
            if (p != IntPtr.Zero)
                _libs[libname] = p;
            return p;
        }

        public static IntPtr Get(string libname, string varname)
        {
            IntPtr lib = _LoadLib(libname);

            if (lib == IntPtr.Zero)
            {
                Console.WriteLine("Globals: dlopen '" + libname + "' failed.");
                return IntPtr.Zero;
            }

            IntPtr ptr = dlsym(lib, varname);
            if (ptr == IntPtr.Zero)
                Console.WriteLine("Globals: dlsym '" + varname + "' in library '" + libname + "' failed.");
            return ptr;
        }

        public static IntPtr GetPtr(string libname, string varname)
        {
            unsafe
            {
                IntPtr ptr = Get(libname, varname);
                if (ptr == IntPtr.Zero) return ptr;
                return new IntPtr(*(void**)ptr.ToPointer());
            }
        }

        public static Int32 GetInt32(string libname, string varname)
        {
            unsafe
            {
                IntPtr ptr = Get(libname, varname);
                if (ptr == IntPtr.Zero) return -1;
                return *(Int32*)ptr.ToPointer();
            }
        }

        public static double GetDouble(string libname, string varname)
        {
            unsafe
            {
                IntPtr ptr = Get(libname, varname);
                if (ptr == IntPtr.Zero) return -1;
                return *(double*)ptr.ToPointer();
            }
        }
    }

    public sealed class CFString : IDisposable
    {
        IntPtr _nativeptr;

        public CFString(string s)
        {
            if (s != null)
                _nativeptr = CoreFoundation.CFStringCreateWithCString(IntPtr.Zero, s, CoreFoundation.kCFStringEncodingUTF8);
            else
                _nativeptr = IntPtr.Zero;
        }

        public CFString(IntPtr nativeptr) : this(nativeptr, true)
        {
        }

        public CFString(IntPtr nativeptr, bool retain)
        {
            _nativeptr = nativeptr;
            if (retain && _nativeptr != IntPtr.Zero)
                CoreFoundation.CFRetain(_nativeptr);
        }

        public IntPtr NativePointer
        {
            get
            {
                return _nativeptr;
            }
        }

        public override string ToString()
        {
            if (_nativeptr == IntPtr.Zero)
                return null;

            long len = CoreFoundation.CFStringGetLength(_nativeptr) + 1;
            StringBuilder sb = new StringBuilder((int)len);
            CoreFoundation.CFStringGetCString(_nativeptr, sb, (uint)len, CoreFoundation.kCFStringEncodingUTF8);

            return sb.ToString();
        }

        public void Dispose()
        {
            if (_nativeptr != IntPtr.Zero)
                CoreFoundation.CFRelease(_nativeptr);
            _nativeptr = IntPtr.Zero;
        }

        // convert plist type to a cfstring
        public static CFString FromPropertyListObject(IntPtr cfpropertylistref)
        {
            if (cfpropertylistref == IntPtr.Zero)
                return new CFString(IntPtr.Zero);

            CFString fmt = new CFString("%@");
            CFString cfstr = new CFString(CoreFoundation.CFStringCreateWithFormat(IntPtr.Zero, IntPtr.Zero,
                                                                                  fmt.NativePointer, cfpropertylistref));
            fmt.Dispose();
            return cfstr;
        }
    }

    public sealed class CFArray : IEnumerable<IntPtr>, IDisposable
    {
        IntPtr _nativeptr;

        public CFArray(IntPtr nativeptr) : this(nativeptr, true)
        {
        }

        public CFArray(IntPtr nativeptr, bool retain)
        {
            _nativeptr = nativeptr;
            if (retain && _nativeptr != IntPtr.Zero)
                CoreFoundation.CFRetain(_nativeptr);
        }

        public IntPtr NativePointer
        {
            get
            {
                return _nativeptr;
            }
        }

        public int Count
        {
            get
            {
                return (int)CoreFoundation.CFArrayGetCount(_nativeptr);
            }
        }

        public IntPtr this[int idx]
        {
            get
            {
                return CoreFoundation.CFArrayGetValueAtIndex(_nativeptr, (long)idx);
            }
        }

        IEnumerator<IntPtr> IEnumerable<IntPtr>.GetEnumerator()
        {
            return new CFArrayEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CFArrayEnumerator(this);
        }

        public void Dispose()
        {
            if (_nativeptr != IntPtr.Zero)
                CoreFoundation.CFRelease(_nativeptr);
            _nativeptr = IntPtr.Zero;
        }

        class CFArrayEnumerator : IEnumerator<IntPtr>
        {
            CFArray _array;
            int cur = -1;

            public CFArrayEnumerator(CFArray array)
            {
                _array = array;
            }

            public bool MoveNext()
            {
                cur++;
                return cur < _array.Count;
            }

            public void Reset()
            {
                cur = -1;
            }

            IntPtr IEnumerator<IntPtr>.Current
            {
                get
                {
                    return _array[cur];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return _array[cur];
                }
            }

            public void Dispose()
            {
            }
        }
    }

    public sealed class CFDictionary : IDisposable
    {
        IntPtr _nativeptr;

        public CFDictionary(IntPtr nativeptr) : this(nativeptr, true)
        {
        }

        public CFDictionary(IntPtr nativeptr, bool retain)
        {
            _nativeptr = nativeptr;
            if (retain && _nativeptr != IntPtr.Zero)
                CoreFoundation.CFRetain(_nativeptr);
        }

        public IntPtr NativePointer
        {
            get
            {
                return _nativeptr;
            }
        }

        public IntPtr ValueForKey(string key)
        {
            using (var cfstr = new CFString(key))
                return CoreFoundation.CFDictionaryGetValue(_nativeptr, cfstr.NativePointer);
        }

        public string StringForKey(string key)
        {
            IntPtr ptr = ValueForKey(key);
            using (var cfstr = new CFString(ptr))
                return cfstr.ToString();
        }

        public bool TryGetValue(string key, out IntPtr val)
        {
            val = IntPtr.Zero;
            using (var cfstr = new CFString(key))
            {
                if (!CoreFoundation.CFDictionaryContainsKey(_nativeptr, cfstr.NativePointer))
                    return false;
                val = CoreFoundation.CFDictionaryGetValue(_nativeptr, cfstr.NativePointer);
                return true;
            }
        }

        public bool TryGetString(string key, out string str)
        {
            str = null;
            IntPtr ptr;
            if (TryGetValue(key, out ptr))
            {
                using (var cfstr = new CFString(ptr))
                    str = cfstr.ToString();
                return true;
            }
            return false;
        }

        public bool ContainsKey(string key)
        {
            using (var cfstr = new CFString(key))
                return CoreFoundation.CFDictionaryContainsKey(_nativeptr, cfstr.NativePointer);
        }

        public void Dispose()
        {
            if (_nativeptr != IntPtr.Zero)
                CoreFoundation.CFRelease(_nativeptr);
            _nativeptr = IntPtr.Zero;
        }
    }

    public sealed class CFBundle : IDisposable
    {
       IntPtr _nativeptr;

       public static string MainBundlePath
       {
           get
           {
               using (CFBundle bundle = new CFBundle())
                   return bundle.BundlePath;
           }
       }

       public static string MainBundleResourcePath
       {
           get
           {
               using (CFBundle bundle = new CFBundle())
                   return bundle.ResourcePath;
           }
       }

       private CFBundle()
       {
           _nativeptr = CoreFoundation.CFBundleGetMainBundle();
           CoreFoundation.CFRetain(_nativeptr);
       }

       public string BundlePath
       {
           get
           {
               return Path.GetFullPath(Path.Combine(Path.Combine(ResourcePath, ".."), ".."));
           }
       }

       public string ResourcePath
       {
           get
           {
               IntPtr url = CoreFoundation.CFBundleCopyResourcesDirectoryURL(_nativeptr);
               if (url == IntPtr.Zero)
                   return null;

               IntPtr absoluteurl = CoreFoundation.CFURLCopyAbsoluteURL(url);
               if (absoluteurl == IntPtr.Zero)
               {
                   CoreFoundation.CFRelease(url);
                   return null;
               }

               IntPtr path = CoreFoundation.CFURLCopyFileSystemPath(absoluteurl, CoreFoundation.CFURLPathStyle.kCFURLPOSIXPathStyle);
               if (path == IntPtr.Zero)
               {
                   CoreFoundation.CFRelease(absoluteurl);
                   CoreFoundation.CFRelease(url);
                   return null;
               }

               string str = null;
               using (CFString cfstr = new CFString(path))
                   str = cfstr.ToString();

               CoreFoundation.CFRelease(path);
               CoreFoundation.CFRelease(absoluteurl);
               CoreFoundation.CFRelease(url);

               return str;
           }
       }

       public void Dispose()
       {
           if (_nativeptr != IntPtr.Zero)
               CoreFoundation.CFRelease(_nativeptr);
           _nativeptr = IntPtr.Zero;
       }
    }

    public sealed class CFPreferences
    {
       // reverse-dns name, i.e. com.company.ProductName
       public string AppIdentifier { get; private set; }

       public CFPreferences(string appid)
       {
           if (appid == null || appid == "")
               throw new ArgumentException();
           AppIdentifier = appid;

           Synchronize();
       }

       // pass null to remove key from app's preferences. remove all keys to
       // cause the app's preferences plist file to be deleted.
       public void SetString(string key, string val)
       {
           CFString appid = new CFString(AppIdentifier);
           CFString k = new CFString(key);
           CFString v = new CFString(val);
           CoreFoundation.CFPreferencesSetAppValue(k.NativePointer, v.NativePointer, appid.NativePointer);
           v.Dispose();
           k.Dispose();
           appid.Dispose();
       }

       public string GetString(string key)
       {
           string ret = null;

           CFString appid = new CFString(AppIdentifier);
           CFString k = new CFString(key);
           IntPtr o = CoreFoundation.CFPreferencesCopyAppValue(k.NativePointer, appid.NativePointer);

           if (o != IntPtr.Zero)
           {
               CFString str = CFString.FromPropertyListObject(o); // turn value into a string
               ret = str.ToString();
               str.Dispose();
               CoreFoundation.CFRelease(o);
           }

           k.Dispose();
           appid.Dispose();

           return ret;
       }

       public IEnumerable<string> GetAllKeys()
       {
           List<string> ret = new List<string>();

           CFString appid = new CFString(AppIdentifier);
           IntPtr user = Globals.GetPtr(CoreFoundation.LibraryPath, "kCFPreferencesCurrentUser");
           IntPtr host = Globals.GetPtr(CoreFoundation.LibraryPath, "kCFPreferencesAnyHost");
           IntPtr pkeys = CoreFoundation.CFPreferencesCopyKeyList(appid.NativePointer, user, host);

           if (pkeys != IntPtr.Zero)
           {
               CFArray keys = new CFArray(pkeys);
               foreach (IntPtr val in keys)
               {
                   if (val == IntPtr.Zero)
                       continue;

                   CFString str = CFString.FromPropertyListObject(val); // turn value into a string
                   ret.Add(str.ToString());
                   str.Dispose();
               }

               keys.Dispose();
               CoreFoundation.CFRelease(pkeys);
           }

           appid.Dispose();

           return ret;
       }

       // commit changes and re-read plist for external updates
       public void Synchronize()
       {
           CFString appid = new CFString(AppIdentifier);
           CoreFoundation.CFPreferencesAppSynchronize(appid.NativePointer);
           appid.Dispose();
       }

       public static IEnumerable<string> GetAllApps()
       {
           List<string> ret = new List<string>();

           IntPtr user = Globals.GetPtr(CoreFoundation.LibraryPath, "kCFPreferencesCurrentUser");
           IntPtr host = Globals.GetPtr(CoreFoundation.LibraryPath, "kCFPreferencesAnyHost");
           IntPtr papps = CoreFoundation.CFPreferencesCopyApplicationList(user, host);

           if (papps != IntPtr.Zero)
           {
               CFArray apps = new CFArray(papps);
               foreach (IntPtr app in apps)
               {
                   if (app == IntPtr.Zero)
                       continue;

                   CFString str = CFString.FromPropertyListObject(app);
                   ret.Add(str.ToString());
                   str.Dispose();
               }

               apps.Dispose();
               CoreFoundation.CFRelease(papps);
           }

           return ret;
       }
    }

    public sealed class CFData : IDisposable
    {
        IntPtr _nativeptr;

        public CFData(IntPtr nativeptr) : this(nativeptr, true)
        {
        }

        public CFData(IntPtr nativeptr, bool retain)
        {
            _nativeptr = nativeptr;
            if (retain && _nativeptr != IntPtr.Zero)
                CoreFoundation.CFRetain(_nativeptr);
        }

        public IntPtr NativePointer
        {
            get { return _nativeptr; }
        }

        public byte[] GetAllBytes()
        {
            int len = (int)CoreFoundation.CFDataGetLength(_nativeptr);
            byte[] ret = new byte[len];
            Marshal.Copy(CoreFoundation.CFDataGetBytePtr(_nativeptr),
                         ret, 0, len);
            return ret;
        }

        public void Dispose()
        {
            if (_nativeptr != IntPtr.Zero)
                CoreFoundation.CFRelease(_nativeptr);
            _nativeptr = IntPtr.Zero;
        }

    }

    public static class OSInfo
    {
        static string _version;
        static int? _major, _minor, _bugfix;

        public static string Version
        {
            get
            {
                if (_version == null)
                    _version = String.Format("{0}.{1}.{2}", MajorVersion, MinorVersion, BugFixVersion);
                return _version;
            }
        }

        public static int MajorVersion
        {
            get
            {
                if (_major == null)
                {
                    IntPtr response = IntPtr.Zero;
                    int rc = CoreServices.Gestalt(CoreServices.gestaltSystemVersionMajor, ref response);
                    if (rc != 0) return 0;
                    _major = response.ToInt32();
                }
                return _major.Value;
            }
        }

        public static int MinorVersion
        {
            get
            {
                if (_minor == null)
                {
                    IntPtr response = IntPtr.Zero;
                    int rc = CoreServices.Gestalt(CoreServices.gestaltSystemVersionMinor, ref response);
                    if (rc != 0) return 0;
                    _minor = response.ToInt32();
                }
                return _minor.Value;
            }
        }

        public static int BugFixVersion
        {
            get
            {
                if (_bugfix == null)
                {
                    IntPtr response = IntPtr.Zero;
                    int rc = CoreServices.Gestalt(CoreServices.gestaltSystemVersionBugFix, ref response);
                    if (rc != 0) return 0;
                    _bugfix = response.ToInt32();
                }
                return _bugfix.Value;
            }
        }

        public static bool VersionAtLeast(int maj, int min)
        {
            return MajorVersion > maj || (MajorVersion == maj && MinorVersion >= min);
        }
    }

    public static class HardwareInfo
    {
        public static string MachineName
        {
            get
            {
                IntPtr response = IntPtr.Zero;
                int rc = CoreServices.Gestalt(CoreServices.gestaltUserVisibleMachineName, ref response);
                if (rc != 0) return null;
                string str = "";
                unsafe
                {
                    byte *ptr = (byte*)response.ToPointer();
                    int len = (int)ptr[0];
                    for (int i = 1; i <= len; i++)
                        str += (char)ptr[i];
                }
                return str;
            }
        }

        public static string GetSysctlString(string name)
        {
            int len = 1024;
            StringBuilder sb = new StringBuilder(len);
            int rc = OSXUtils.Libc.sysctlbyname(name, sb, ref len, null, 0);
            if (rc != 0) return null;
            return sb.ToString();
        }

        public static int GetSysctlInt(string name)
        {
            int len = 4;
            IntPtr ptr = IntPtr.Zero;
            int rc = OSXUtils.Libc.sysctlbyname(name, ref ptr, ref len, null, 0);
            if (rc != 0) return -1;
            return ptr.ToInt32();
        }

        public static bool GetSysctlBool(string name)
        {
            return GetSysctlInt(name) != 0;
        }
    }

    public static class AliasManager
    {
        // resolves any aliases and return true path
        public static string ResolvePath(string path)
        {
            IntPtr url;
            IntPtr resolved_url;
            IntPtr resolved_path;
            string ret = path;

            using (CFString pathstr = new CFString(path))
                url = CoreFoundation.CFURLCreateWithFileSystemPath(IntPtr.Zero,
                                                                   pathstr.NativePointer,
                                                                   CoreFoundation.CFURLPathStyle.kCFURLPOSIXPathStyle,
                                                                   false);

            try
            {
                if (url != IntPtr.Zero)
                {
                    var fsref = new CoreServices.FSRef();
                    if (CoreFoundation.CFURLGetFSRef(url, ref fsref))
                    {
                        bool isfolder, isalias;
                        if (CoreServices.FSResolveAliasFile(ref fsref, true, out isfolder, out isalias) == 0 && isalias)
                        {
                            resolved_url = CoreFoundation.CFURLCreateFromFSRef(IntPtr.Zero, ref fsref);
                            if (resolved_url != IntPtr.Zero)
                            {
                                resolved_path = CoreFoundation.CFURLCopyFileSystemPath(resolved_url, CoreFoundation.CFURLPathStyle.kCFURLPOSIXPathStyle);
                                if (resolved_path != IntPtr.Zero)
                                {
                                    using (CFString cfstr = new CFString(resolved_path))
                                        ret = cfstr.ToString();
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (url != IntPtr.Zero) CoreFoundation.CFRelease(url);
                if (resolved_url != IntPtr.Zero) CoreFoundation.CFRelease(resolved_url);
                if (resolved_path != IntPtr.Zero) CoreFoundation.CFRelease(resolved_path);
            }

            return ret;
        }
    }

    public static class FourCharCode
    {
        public static UInt32 Get(string fourchars)
        {
            if (fourchars == null) throw new ArgumentNullException("fourchars");
            if (fourchars.Length != 4) throw new ArgumentException("fourchars");
            UInt32 a = fourchars[0];
            UInt32 b = fourchars[1];
            UInt32 c = fourchars[2];
            UInt32 d = fourchars[3];
            return (a << 24) | (b << 16) | (c << 8) | d;
        }
    }

    //
    // Interop
    //

    public static class CoreFoundation
    {
        public const string LibraryPath = "/System/Library/Frameworks/CoreFoundation.framework/Versions/Current/CoreFoundation";

        [DllImport(LibraryPath)]
        public static extern void CFRetain(IntPtr cf);

        [DllImport(LibraryPath)]
        public static extern void CFRelease(IntPtr cf);

        // CFString

        public const UInt32 kCFStringEncodingUTF8 = 0x08000100;

        [DllImport(LibraryPath)]
        public static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, UInt32 encoding);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFStringCreateWithFormat(IntPtr alloc,         // CFAllocatorRef
                                                             IntPtr formatOptions, // CFDictionaryRef
                                                             IntPtr format,        // CFStringRef
                                                             IntPtr arg);

        [DllImport(LibraryPath)]
        public static extern long CFStringGetLength(IntPtr theString);

        [DllImport(LibraryPath)]
        public static extern bool CFStringGetCString(IntPtr theString,
                                                     StringBuilder buffer, UInt32 bufferSize,
                                                     UInt32 encoding);

        [DllImport(LibraryPath, EntryPoint = "__CFStringMakeConstantString")]
        public static extern IntPtr CFSTR(StringBuilder cStr);

        // CFArray

        public static IntPtr CFArrayCreate(IntPtr[] values)
        {
            return _CFArrayCreate(IntPtr.Zero, values, values.Length, IntPtr.Zero);
        }

        [DllImport(LibraryPath, EntryPoint = "CFArrayCreate")]
        static extern IntPtr _CFArrayCreate(IntPtr allocator,
                                            IntPtr[] values,
                                            long numValues,
                                            IntPtr callbacks);

        [DllImport(LibraryPath)]
        public static extern long CFArrayGetCount(IntPtr theArray);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, long idx);

        // CFDictionary

        [DllImport(LibraryPath)]
        public static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

        [DllImport(LibraryPath)]
        public static extern bool CFDictionaryContainsKey(IntPtr theDict, IntPtr key);

        // CFBundle

        public enum CFURLPathStyle
        {
            kCFURLPOSIXPathStyle = 0,
            kCFURLHFSPathStyle = 1,
            kCFURLWindowsPathStyle = 2
        };

        [DllImport(LibraryPath)]
        public static extern IntPtr CFBundleGetMainBundle();

        [DllImport(LibraryPath)]
        public static extern IntPtr CFBundleCopyBundleURL(IntPtr bundle);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFBundleCopyResourcesDirectoryURL(IntPtr bundle);

        // CFURL

        [DllImport(LibraryPath)]
        public static extern IntPtr CFURLCopyAbsoluteURL(IntPtr url);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFURLCopyFileSystemPath(IntPtr url, CFURLPathStyle style);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFURLCreateWithFileSystemPath(IntPtr alloc,
                                                                  IntPtr filepath,
                                                                  CFURLPathStyle style,
                                                                  bool isdir);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFURLCreateFromFSRef(IntPtr alloc, ref CoreServices.FSRef fsref);

        [DllImport(LibraryPath)]
        public static extern bool CFURLGetFSRef(IntPtr url, ref CoreServices.FSRef fsref);

        // CFPreferences

        [DllImport(LibraryPath)]
        public static extern void CFPreferencesSetAppValue(IntPtr key, IntPtr val, IntPtr appid);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFPreferencesCopyAppValue(IntPtr key, IntPtr appid);

        [DllImport(LibraryPath)]
        public static extern bool CFPreferencesAppSynchronize(IntPtr appid);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFPreferencesCopyKeyList(IntPtr appid, IntPtr user, IntPtr host);

        [DllImport(LibraryPath)]
        public static extern IntPtr CFPreferencesCopyApplicationList(IntPtr user, IntPtr host);

        // CFRunLoop

        public static readonly IntPtr kCFRunLoopDefaultMode = CFSTR(new StringBuilder("kCFRunLoopDefaultMode"));

        [DllImport(LibraryPath)]
        public static extern IntPtr CFRunLoopGetCurrent();

        [DllImport(LibraryPath)]
        public static extern IntPtr CFRunLoopGetMain();

        [DllImport(LibraryPath)]
        public static extern void CFRunLoopRun();

        [DllImport(LibraryPath)]
        public static extern int CFRunLoopRunInMode(IntPtr mode, double seconds, bool returnAfterSourceHandled);

        [DllImport(LibraryPath)]
        public static extern void CFRunLoopAddSource(IntPtr runloop, IntPtr source, IntPtr mode);

        // CFData

        [DllImport(LibraryPath)]
        public static extern IntPtr CFDataGetBytePtr(IntPtr /*CFDataRef*/ data);

        [DllImport(LibraryPath)]
        public static extern long CFDataGetLength(IntPtr /*CFDataRef*/ data);
    }

    public class CoreServices
    {
        public const string LibraryPath = "/System/Library/Frameworks/CoreServices.framework/Versions/Current/CoreServices";

        // FSEventStream

        public static readonly ulong kFSEventStreamEventIdSinceNow = 0xFFFFFFFFFFFFFFFFULL;

        public static readonly uint kFSEventStreamCreateFlagNone  = 0x00000000;
        public static readonly uint kFSEventStreamCreateFlagUseCFTypes = 0x00000001;
        public static readonly uint kFSEventStreamCreateFlagNoDefer = 0x00000002;
        public static readonly uint kFSEventStreamCreateFlagWatchRoot = 0x00000004;
        public static readonly uint kFSEventStreamCreateFlagIgnoreSelf = 0x00000008;

        public static readonly uint kFSEventStreamEventFlagNone   = 0x00000000;
        public static readonly uint kFSEventStreamEventFlagMustScanSubDirs = 0x00000001;
        public static readonly uint kFSEventStreamEventFlagUserDropped = 0x00000002;
        public static readonly uint kFSEventStreamEventFlagKernelDropped = 0x00000004;
        public static readonly uint kFSEventStreamEventFlagEventIdsWrapped = 0x00000008;
        public static readonly uint kFSEventStreamEventFlagHistoryDone = 0x00000010;
        public static readonly uint kFSEventStreamEventFlagRootChanged = 0x00000020;
        public static readonly uint kFSEventStreamEventFlagMount  = 0x00000040;
        public static readonly uint kFSEventStreamEventFlagUnmount = 0x00000080;

        public delegate void FSEventStreamCallback(IntPtr stream,
                                                   IntPtr userData,
                                                   UIntPtr numEvents, // size_t
                                                   [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
                                                   string[] eventPaths,
                                                   [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
                                                   uint[] eventFlags,
                                                   [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
                                                   ulong[] eventIds);

        [DllImport(LibraryPath)]
        public static extern IntPtr FSEventStreamCreate(IntPtr allocator,
                                                        FSEventStreamCallback callback,
                                                        IntPtr userData,
                                                        IntPtr pathsToWatch, // CFArrayRef
                                                        ulong sinceWhen,
                                                        double latency,
                                                        uint flags);

        [DllImport(LibraryPath)]
        public static extern void FSEventStreamScheduleWithRunLoop(IntPtr stream,
                                                                   IntPtr runloop,
                                                                   IntPtr mode);

        [DllImport(LibraryPath)]
        public static extern void FSEventStreamUnscheduleWithRunLoop(IntPtr stream,
                                                                     IntPtr runloop,
                                                                     IntPtr mode);

        [DllImport(LibraryPath)]
        public static extern void FSEventStreamStart(IntPtr stream);

        [DllImport(LibraryPath)]
        public static extern void FSEventStreamStop(IntPtr stream);

        [DllImport(LibraryPath)]
        public static extern void FSEventStreamInvalidate(IntPtr stream);

        [DllImport(LibraryPath)]
        public static extern void FSEventStreamRelease(IntPtr stream);

        [DllImport(LibraryPath)]
        public static extern void FSEventStreamShow(IntPtr stream);

        // Gestalt

        public static readonly uint gestaltUserVisibleMachineName = FourCharCode.Get("mnam");
        public static readonly uint gestaltSystemVersionMajor = FourCharCode.Get("sys1");
        public static readonly uint gestaltSystemVersionMinor = FourCharCode.Get("sys2");
        public static readonly uint gestaltSystemVersionBugFix = FourCharCode.Get("sys3");

        [DllImport(LibraryPath)]
        public static extern int Gestalt(uint selector, ref IntPtr response);

        // Alias Manager

        [StructLayout(LayoutKind.Sequential)]
        public struct FSRef
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 80)]
            public byte[] hidden;
        }

        [DllImport(LibraryPath)]
        public static extern int FSResolveAliasFile(ref FSRef fsref,
                                                    bool recursive,
                                                    out bool isfolder,
                                                    out bool isalias);
    }

    public class IOKit
    {
        public const string LibraryPath = "/System/Library/Frameworks/IOKit.framework/IOKit";

        public delegate void IOPowerSourceCallbackType(IntPtr context);

        public const string kIOPSPowerSourceStateKey = "Power Source State";
        public const string kIOPSOffLineValue = "Off Line";
        public const string kIOPSACPowerValue = "AC Power";
        public const string kIOPSBatteryPowerValue = "Battery Power";

//        [DllImport(LibraryPath)]
//        public static extern IntPtr /*CFDictionaryRef*/ IOPSCopyExternalPowerAdapterDetails(); //NOTE: 10.6 only

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFTypeRef*/ IOPSCopyPowerSourcesInfo();

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFArrayRef*/ IOPSCopyPowerSourcesList(IntPtr /*CFTypeRef*/ blob);

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFDictionaryRef*/ IOPSGetPowerSourceDescription(IntPtr /*CFTypeRef*/ blob, IntPtr /*CFTypeRef*/ source);

        [DllImport(LibraryPath)]
        public static extern IntPtr IOPSNotificationCreateRunLoopSource(IOPowerSourceCallbackType cb, IntPtr context);
    }

    public static class DiskArbitration
    {
        public const string LibraryPath = "/System/Library/Frameworks/DiskArbitration.framework/Versions/Current/DiskArbitration";

        public static readonly IntPtr kDADiskDescriptionVolumeNameKey = Globals.GetPtr(LibraryPath, "kDADiskDescriptionVolumeNameKey");
        public static readonly IntPtr kDADiskDescriptionMediaNameKey = Globals.GetPtr(LibraryPath, "kDADiskDescriptionMediaNameKey");

        [DllImport(LibraryPath)]
        public static extern IntPtr DASessionCreate(IntPtr alloc);

        [DllImport(LibraryPath)]
        public static extern IntPtr DADiskCreateFromBSDName(IntPtr alloc, IntPtr session, string name);

        [DllImport(LibraryPath)]
        public static extern IntPtr DADiskCopyDescription(IntPtr disk);
    }

    public static class DiscRecording
    {
        public const string LibraryPath = "/System/Library/Frameworks/DiscRecording.framework/Versions/Current/DiscRecording";

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFArrayRef*/ DRCopyDeviceArray();

        [DllImport(LibraryPath)]
        public static extern IntPtr /*DRDeviceRef*/ DRDeviceCopyDeviceForBSDName(IntPtr /*CFStringRef*/ name);

        [DllImport(LibraryPath)]
        public static extern IntPtr /*DRDeviceRef*/ DRDeviceCopyDeviceForIORegistryEntryPath(IntPtr /*CFStringRef*/ path);

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFDictionaryRef*/ DRDeviceCopyInfo(IntPtr /*DRDeviceRef*/ device);

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFDictionaryRef*/ DRDeviceCopyStatus(IntPtr /*DRDeviceRef*/ device);

        [DllImport(LibraryPath)]
        public static extern int DRDeviceEjectMedia(IntPtr /*DRDeviceRef*/ device);

        [DllImport(LibraryPath)]
        public static extern int DRDeviceCloseTray(IntPtr /*DRDeviceRef*/ device);
    }

    public class AE
    {
        public const string LibraryPath = "/System/Library/Frameworks/CoreServices.framework/Versions/Current/" +
                                          "Frameworks/AE.framework/Versions/Current/AE";

        public const Int32 kAutoGenerateReturnID = -1; /* AECreateAppleEvent will generate a session-unique ID */
        public const Int32 kAnyTransactionID = 0; /* no transaction is in use */

        public const Int32 kAEDefaultTimeout = -1; /* timeout value determined by AEM */
        public const Int32 kNoTimeOut = -2; /* wait until reply comes back, however long it takes */


        public enum AESendMode
        {
            kAENoReply                    = 0x00000001, /* sender doesn't want a reply to event */
            kAEQueueReply                 = 0x00000002, /* sender wants a reply but won't wait */
            kAEWaitReply                  = 0x00000003, /* sender wants a reply and will wait */
            kAEDontReconnect              = 0x00000080, /* don't reconnect if there is a sessClosedErr from PPCToolbox */
            kAEWantReceipt                = 0x00000200, /* (nReturnReceipt) sender wants a receipt of message */
            kAENeverInteract              = 0x00000010, /* server should not interact with user */
            kAECanInteract                = 0x00000020, /* server may try to interact with user */
            kAEAlwaysInteract             = 0x00000030, /* server should always interact with user where appropriate */
            kAECanSwitchLayer             = 0x00000040, /* interaction may switch layer */
            kAEDontRecord                 = 0x00001000, /* don't record this event - available only in vers 1.0.1 and greater */
            kAEDontExecute                = 0x00002000, /* don't send the event for recording - available only in vers 1.0.1 and greater */
            kAEProcessNonReplyEvents      = 0x00008000, /* allow processing of non-reply events while awaiting synchronous AppleEvent reply */
            kAEDoNotAutomaticallyAddAnnotationsToEvent = 0x00010000 /* if set, don't automatically add any sandbox or other annotations to the event */
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct AEDesc
        {
            // this is meant to be opaque
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] hidden;
        }

        public delegate void AppleEventCallback(ref AE.AEDesc ae_event, ref AE.AEDesc ae_reply, IntPtr userdata);

        [DllImport(LibraryPath)]
        public static extern int AEInstallEventHandler(UInt32 eventclass, // FourCharCode
                                                       UInt32 eventid, // FourCharCode
                                                       AppleEventCallback cb,
                                                       IntPtr userdata,
                                                       bool syshandler);

        [DllImport(LibraryPath, CharSet=CharSet.Ansi)]
        public static extern int AECreateDesc(UInt32 typecode, // FourCharCode
                                              string data,
                                              Int32 datalen,
                                              ref AEDesc result);

        [DllImport(LibraryPath)]
        public static extern int AECreateAppleEvent(UInt32 eventclass, // FourCharCode
                                                    UInt32 eventid, // FourCharCode
                                                    ref AEDesc target,
                                                    Int16 return_id,
                                                    Int32 transaction_id,
                                                    ref AEDesc result);

        [DllImport(LibraryPath)]
        public static extern int AEPutParamPtr(ref AEDesc apple_event,
                                               UInt32 keyword, // FourCharCode
                                               UInt32 type, // FourCharCode
                                               IntPtr data,
                                               Int32 datalen);

        [DllImport(LibraryPath)]
        public static extern int AEGetParamPtr(ref AEDesc apple_event,
                                               UInt32 keyword, // FourCharCode
                                               UInt32 desired_type, // FourCharCode
                                               out UInt32 actual_type, // FourCharCode
                                               IntPtr result,
                                               Int32 result_len,
                                               out Int32 result_actual_size);

        [DllImport(LibraryPath)]
        public static extern int AESendMessage(ref AEDesc event_to_send,
                                               ref AEDesc event_reply,
                                               AESendMode sendmode,
                                               Int32 timeout_ticks);

        [DllImport(LibraryPath)]
        public static extern int AEDisposeDesc(ref AEDesc descriptor);

        //--------------------

        public static string SendMessage(string target_bundle_id, string msg)
        {
            AE.AEDesc target = new AE.AEDesc();
            AE.AEDesc event_to_send = new AE.AEDesc();
            AE.AEDesc event_reply = new AE.AEDesc();

            int rc = AE.AECreateDesc(FourCharCode.Get("bund"),
                                     target_bundle_id,
                                     target_bundle_id.Length,
                                     ref target);
            if (rc != 0)
                throw new AppleEventsException("AECreateDesc failed with " + rc);

            rc = AE.AECreateAppleEvent(FourCharCode.Get("sooe"),
                                       FourCharCode.Get("sooa"),
                                       ref target,
                                       AE.kAutoGenerateReturnID,
                                       AE.kAnyTransactionID,
                                       ref event_to_send);
            if (rc != 0)
            {
                AE.AEDisposeDesc(ref target);
                throw new AppleEventsException("AECreateAppleEvent failed with " + rc);
            }

            AE.AEDisposeDesc(ref target);

            _PutMessage(ref event_to_send, msg);
            rc = AE.AESendMessage(ref event_to_send, ref event_reply,
                                  AE.AESendMode.kAEWaitReply | AE.AESendMode.kAENeverInteract,
                                  AE.kAEDefaultTimeout);

            AE.AEDisposeDesc(ref event_to_send);

            if (rc != 0)
                throw new AppleEventsException("AESendMessage failed with " + rc);

            string reply = _GetMessage(ref event_reply);
            AE.AEDisposeDesc(ref event_reply);
            return reply;
        }

        static string _GetMessage(ref AE.AEDesc ae_event)
        {
            UInt32 type;
            IntPtr buf;
            int size;

            try
            {
                const int buflen = 8192;
                buf = Marshal.AllocHGlobal(buflen);

                int rc = AE.AEGetParamPtr(ref ae_event,
                                          FourCharCode.Get("----"),
                                          FourCharCode.Get("utf8"),
                                          out type,
                                          buf, buflen,
                                          out size);
                if (rc != 0)
                    throw new AppleEventsException("AEGetParamPtr failed with " + rc);
                if (type != FourCharCode.Get("utf8"))
                    throw new InvalidOperationException("apple event param has invalid type");

                byte[] data = new byte[size];
                Marshal.Copy(buf, data, 0, data.Length);
                return Encoding.UTF8.GetString(data);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        static void _PutMessage(ref AE.AEDesc ae_event, string msg)
        {
            IntPtr buf;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                buf = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, buf, data.Length);
                int rc = AE.AEPutParamPtr(ref ae_event,
                                          FourCharCode.Get("----"),
                                          FourCharCode.Get("utf8"),
                                          buf,
                                          data.Length);
                if (rc != 0)
                    throw new AppleEventsException("AEPutParamPtr failed with " + rc);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        public class AppleEventsException : ApplicationException
        {
            public AppleEventsException() : base() { }
            public AppleEventsException(string s) : base(s) { }
        }
    }

    public static class Libc
    {
        public const string LibraryPath = "/usr/lib/libc.dylib";

        [DllImport(LibraryPath)]
        public static extern int sysctlbyname(string name, StringBuilder oldp, ref int oldplen, string newp, int newlen);

        [DllImport(LibraryPath)]
        public static extern int sysctlbyname(string name, ref IntPtr oldp, ref int oldplen, string newp, int newlen);
    }

    public static class OSServices
    {
        public const string LibraryPath = "/System/Library/Frameworks/CoreServices.framework/Versions/Current/" +
                                          "Frameworks/OSServices.framework/Versions/Current/OSServices";

        public static readonly uint kCSIdentityQueryGenerateUpdateEvents = 0x0001;
        public static readonly uint kCSIdentityQueryIncludeHiddenIdentities = 0x0002;

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CSIdentityQueryRef*/ CSIdentityQueryCreateForCurrentUser(IntPtr alloc);

        [DllImport(LibraryPath)]
        public static extern bool CSIdentityQueryExecute(IntPtr /*CSIdentityQueryRef*/ query, uint flags, IntPtr /*CFErrorRef*/ error);

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFArrayRef*/ CSIdentityQueryCopyResults(IntPtr /*CSIdentityQueryRef*/ query);

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CSIdentityRef*/ CSIdentityGetImageData(IntPtr /*CSIdentityRef*/ identity);

        [DllImport(LibraryPath)]
        public static extern IntPtr /*CFStringRef*/ CSIdentityGetImageDataType(IntPtr /*CSIdentityRef*/ identity);
    }
}
