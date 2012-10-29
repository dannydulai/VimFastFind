using System;
using System.IO;
using System.Collections.Generic;
#if PLATFORM_WINDOWS
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
#if JEREMIAHFIXME
using Dolinay; // DriveDetector
#endif
#elif PLATFORM_MACOSX
using MonoMac;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
#endif

namespace VimFastFind
{
    public enum VolumeWatcherEvent
    {
        DidMount,
        WillUnmount,
        DidUnmount
    }

    // volume will resemble "C:" on windows and "/" or "/Volumes/xxx" on mac
    public delegate void VolumeWatcherEventDelegate(VolumeWatcher sender, VolumeWatcherEvent evt, string volume);


    // on mac, VolumeWatcher uses NSWorkspace notifications: to get notifications,
    // your application's main thread must be in a CoreFoundation run loop.
    //
    public class VolumeWatcher : IDisposable
    {
#if PLATFORM_MACOSX
#pragma warning disable 0414
        VolumeWatcherHelper _helper;
#pragma warning restore 0414
#elif PLATFORM_WINDOWS
#if JEREMIAHFIXME
        DriveDetector _detector;
#endif
        string _hookeddrive;
#endif

        public event VolumeWatcherEventDelegate VolumeChanged;

        public VolumeWatcher()
        {
#if PLATFORM_MACOSX
            _helper = new VolumeWatcherHelper(this);
#elif PLATFORM_WINDOWS
#if JEREMIAHFIXME
            _detector = new DriveDetector();
            _detector.DeviceArrived += delegate(object sender, DriveDetectorEventArgs e) {
                if (e.Drive == _hookeddrive) e.HookQueryRemove = true;
                ev_VolumeChanged(VolumeWatcherEvent.DidMount, e.Drive);
            };
            _detector.DeviceRemoved += delegate(object sender, DriveDetectorEventArgs e) {
                ev_VolumeChanged(VolumeWatcherEvent.DidUnmount, e.Drive);
            };
            _detector.QueryRemove += delegate(object sender, DriveDetectorEventArgs e) {
                // QueryRemove only gets fired if the device is "hooked" (see
                // DriveDetector for explanation) and only one drive can be hooked
                // at a time. may need to fix if this event is needed.
                ev_VolumeChanged(VolumeWatcherEvent.WillUnmount, e.Drive);
            };
#endif
#else
            throw new NotSupportedException();
#endif
        }

        // returns the list of connected volumes.
        public static IList<string> ScanVolumes()
        {
#if PLATFORM_MACOSX
            var list = new List<string>();
            using (NSAutoreleasePool pool = new NSAutoreleasePool())
            {
                foreach (string path in NSWorkspace.SharedWorkspace.MountedLocalVolumePaths)
                    list.Add(path);
            }
            return list;
#elif PLATFORM_WINDOWS
            var list = new List<string>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
                list.Add(drive.RootDirectory.FullName);
            return list;
#else
            throw new NotSupportedException();
#endif
        }

        void ev_VolumeChanged(VolumeWatcherEvent evt, string volume)
        {
            Trace("ev_VolumeChanged " + evt + ": " + volume);
            if (VolumeChanged != null)
                VolumeChanged(this, evt, volume);
        }

        void Trace(string s)
        {
//            Console.WriteLine(s);
        }

        public void Dispose()
        {
#if PLATFORM_MACOSX
            _helper.Dispose();
#elif PLATFORM_WINDOWS
#if JEREMIAHFIXME
            _detector.Dispose();
#endif
#else
            throw new NotSupportedException();
#endif
        }

#if PLATFORM_MACOSX
        [Register]
        public class VolumeWatcherHelper : NSObject
        {
            VolumeWatcher outer;
            object _disposelock = new object();
            bool _disposed;

            public VolumeWatcherHelper(VolumeWatcher vw)
            {
                outer = vw;
                OSXUtils.ApplicationHelper.ExecuteWhenLaunched(delegate
                {
                    using (NSAutoreleasePool pool = new NSAutoreleasePool ())
                    {
                        NSNotificationCenter nc = NSWorkspace.SharedWorkspace.NotificationCenter;
                        nc.AddObserver(this, new Selector("ev_VolumeDidMount:"), new NSString("NSWorkspaceDidMountNotification"), null);
                        nc.AddObserver(this, new Selector("ev_VolumeDidUnmount:"), new NSString("NSWorkspaceDidUnmountNotification"), null);
                        nc.AddObserver(this, new Selector("ev_VolumeWillUnmount:"), new NSString("NSWorkspaceWillUnmountNotification"), null);
                    }
                });
            }

            protected override void Dispose(bool disposing)
            {
                if (_disposed) return;
                lock (_disposelock)
                {
                    if (_disposed) return;
                    OSXUtils.ApplicationHelper.ExecuteWhenLaunched(delegate
                    {
                        using (NSAutoreleasePool pool = new NSAutoreleasePool ())
                            NSWorkspace.SharedWorkspace.NotificationCenter.RemoveObserver (this);
                    });
                    _disposed = true;
                }
                base.Dispose(disposing);
            }

            [Export("ev_VolumeDidMount:")]
            public void ev_VolumeDidMount(NSNotification n)
            {
                using (NSAutoreleasePool pool = new NSAutoreleasePool())
                {
                    string mntpt = (NSString)n.UserInfo.ObjectForKey(new NSString("NSDevicePath"));
                    outer.ev_VolumeChanged(VolumeWatcherEvent.DidMount, mntpt);
                }
            }

            [Export("ev_VolumeWillUnmount:")]
            public void ev_VolumeWillUnmount(NSNotification n)
            {
                using (NSAutoreleasePool pool = new NSAutoreleasePool())
                {
                    string mntpt = (NSString)n.UserInfo.ObjectForKey(new NSString("NSDevicePath"));
                    outer.ev_VolumeChanged(VolumeWatcherEvent.WillUnmount, mntpt);
                }
            }

            [Export("ev_VolumeDidUnmount:")]
            public void ev_VolumeDidUnmount(NSNotification n)
            {
                using (NSAutoreleasePool pool = new NSAutoreleasePool())
                {
                    string mntpt = (NSString)n.UserInfo.ObjectForKey(new NSString("NSDevicePath"));
                    outer.ev_VolumeChanged(VolumeWatcherEvent.DidUnmount, mntpt);
                }
            }
        }

#elif PLATFORM_WINDOWS
        public string DriveToWatchForRemoval
        {
            get
            {
                return _hookeddrive;
            }
            set
            {
                _hookeddrive = value;
#if JEREMIAHFIXME
                _detector.EnableQueryRemove(value);
#endif
            }
        }
#endif
    }
}
