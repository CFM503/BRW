using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EBSBrowser
{
    public class ExtendedWebBrowser : WebBrowser
    {
        private AxHost.ConnectionPointCookie cookie;
        private WebBrowserExtendedEvents events;
        private long lastNewWindowTicks = 0;

        public event EventHandler<NewWindow2EventArgs> NewWindow2Event;
        public event EventHandler<NewWindow3EventArgs> NewWindow3Event;
        public event EventHandler<ProgressChangeEventArgs> ProgressChangeEvent;
        public event EventHandler<WindowClosingEventArgs> WindowClosingEvent;
        public event EventHandler<FileDownloadEventArgs> FileDownloadEvent;

        public ExtendedWebBrowser()
        {
            // Suppress standard IE script error dialogs completely
            // DO NOT set ScriptErrorsSuppressed = true; doing so suppresses native Java security prompts!
            // Script errors are already cleanly handled by CustomWebBrowserSite (IOleCommandTarget).
            this.ScriptErrorsSuppressed = false;
        }

        protected override WebBrowserSiteBase CreateWebBrowserSiteBase()
        {
            return new CustomWebBrowserSite(this);
        }

        protected override void CreateSink()
        {
            base.CreateSink();
            events = new WebBrowserExtendedEvents(this);
            cookie = new AxHost.ConnectionPointCookie(this.ActiveXInstance, events, typeof(DWebBrowserEvents2));
        }

        protected override void DetachSink()
        {
            if (cookie != null)
            {
                cookie.Disconnect();
                cookie = null;
            }
            base.DetachSink();
        }

        protected override void OnDocumentCompleted(WebBrowserDocumentCompletedEventArgs e)
        {
            base.OnDocumentCompleted(e);

            try
            {
                if (this.Document != null && this.Document.Window != null)
                {
                    this.Document.Window.Error += DocumentWindow_Error;
                }
            }
            catch
            {
            }
        }

        private void DocumentWindow_Error(object sender, HtmlElementErrorEventArgs e)
        {
            e.Handled = true;
        }

        public void SetZoom(int percent)
        {
            try
            {
                object ax = this.ActiveXInstance;
                if (ax != null)
                {
                    object pvaIn = percent;
                    object pvaOut = null;
                    ax.GetType().InvokeMember("ExecWB", BindingFlags.InvokeMethod, null, ax, new object[] { 63, 2, pvaIn, pvaOut });
                }
            }
            catch
            {
            }
        }

        internal void OnNewWindow2(ref object ppDisp, ref bool cancel)
        {
            EventHandler<NewWindow2EventArgs> handler = NewWindow2Event;
            if (handler != null)
            {
                NewWindow2EventArgs args = new NewWindow2EventArgs(ppDisp, cancel);
                handler(this, args);
                ppDisp = args.PPDisp;
                cancel = args.Cancel;
            }
        }

        internal void OnNewWindow3(ref object ppDisp, ref bool cancel, uint dwFlags, string bstrUrlContext, string bstrUrl)
        {
            lastNewWindowTicks = Environment.TickCount;

            EventHandler<NewWindow3EventArgs> handler = NewWindow3Event;
            if (handler != null)
            {
                NewWindow3EventArgs args = new NewWindow3EventArgs(ppDisp, cancel, dwFlags, bstrUrlContext, bstrUrl);
                handler(this, args);
                ppDisp = args.PPDisp;
                cancel = args.Cancel;
            }
        }

        internal void OnProgressChange(int progress, int progressMax)
        {
            EventHandler<ProgressChangeEventArgs> handler = ProgressChangeEvent;
            if (handler != null)
            {
                handler(this, new ProgressChangeEventArgs(progress, progressMax));
            }
        }

        internal void OnWindowClosing(bool isChildWindow, ref bool cancel)
        {
            // CRITICAL: Always force cancel = true for MSHTML!
            // When cancel is false, MSHTML executes its default window closing pipeline
            // by destroying the control or sending WM_CLOSE to the host form, closing the entire application!
            cancel = true;

            EventHandler<WindowClosingEventArgs> handler = WindowClosingEvent;
            if (handler != null)
            {
                WindowClosingEventArgs args = new WindowClosingEventArgs(isChildWindow, true);
                handler(this, args);
                // Keep cancel = true regardless of managed handler so MSHTML never destroys MainForm
            }
        }

        internal void OnFileDownload(bool activeDocument, ref bool cancel)
        {
            EventHandler<FileDownloadEventArgs> handler = FileDownloadEvent;
            if (handler != null)
            {
                FileDownloadEventArgs args = new FileDownloadEventArgs(activeDocument, cancel);
                handler(this, args);
                cancel = args.Cancel;
            }
        }

        #region Custom Site to intercept script errors and retain alerts

        protected class CustomWebBrowserSite : WebBrowserSite, IOleCommandTarget, IDocHostShowUI
        {
            private static readonly Guid CGID_DocHostCommandHandler = new Guid("f38bc242-b950-11d1-8918-00c04fc2c836");
            private const int OLECMDID_SHOWSCRIPTERROR = 40;
            private const int OLECMDERR_E_UNKNOWNGROUP = -2147221248;

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

            public CustomWebBrowserSite(WebBrowser host) : base(host) { }

            public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, IntPtr prgCmds, IntPtr pCmdText)
            {
                return OLECMDERR_E_UNKNOWNGROUP;
            }

            public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
            {
                try
                {
                    if (pguidCmdGroup == CGID_DocHostCommandHandler)
                    {
                        if (nCmdID == OLECMDID_SHOWSCRIPTERROR)
                        {
                            // Return S_OK (0) to tell MSHTML that the script error was handled and suppress the dialog!
                            return 0;
                        }
                    }
                }
                catch
                {
                }
                return OLECMDERR_E_UNKNOWNGROUP;
            }

            public int ShowMessage(IntPtr hwnd, string lpstrText, string lpstrCaption, uint dwType, string lpstrHelpFile, uint dwHelpContext, out int lpResult)
            {
                try
                {
                    // Preserve genuine webpage alert / confirm dialogs if needed
                    string caption = string.IsNullOrEmpty(lpstrCaption) ? "系统提示" : lpstrCaption;
                    lpResult = MessageBoxW(hwnd, lpstrText, caption, dwType);
                    return 0;
                }
                catch
                {
                    lpResult = 0;
                    return 0;
                }
            }

            public int ShowHelp(IntPtr hwnd, string pszHelpFile, uint uCommand, uint dwData, object ptMouse, object pDispatchObjectHit)
            {
                return 0;
            }
        }

        #endregion
    }

    #region Event Arguments Classes

    public class NewWindow2EventArgs : EventArgs
    {
        public object PPDisp { get; set; }
        public bool Cancel { get; set; }

        public NewWindow2EventArgs(object ppDisp, bool cancel)
        {
            this.PPDisp = ppDisp;
            this.Cancel = cancel;
        }
    }

    public class NewWindow3EventArgs : EventArgs
    {
        public object PPDisp { get; set; }
        public bool Cancel { get; set; }
        public uint Flags { get; private set; }
        public string UrlContext { get; private set; }
        public string Url { get; private set; }

        public NewWindow3EventArgs(object ppDisp, bool cancel, uint flags, string urlContext, string url)
        {
            this.PPDisp = ppDisp;
            this.Cancel = cancel;
            this.Flags = flags;
            this.UrlContext = urlContext;
            this.Url = url;
        }
    }

    public class ProgressChangeEventArgs : EventArgs
    {
        public int Progress { get; private set; }
        public int ProgressMax { get; private set; }

        public ProgressChangeEventArgs(int progress, int progressMax)
        {
            this.Progress = progress;
            this.ProgressMax = progressMax;
        }
    }

    public class WindowClosingEventArgs : EventArgs
    {
        public bool IsChildWindow { get; private set; }
        public bool Cancel { get; set; }

        public WindowClosingEventArgs(bool isChildWindow, bool cancel)
        {
            this.IsChildWindow = isChildWindow;
            this.Cancel = cancel;
        }
    }

    public class FileDownloadEventArgs : EventArgs
    {
        public bool ActiveDocument { get; private set; }
        public bool Cancel { get; set; }

        public FileDownloadEventArgs(bool activeDocument, bool cancel)
        {
            this.ActiveDocument = activeDocument;
            this.Cancel = cancel;
        }
    }

    #endregion

    #region COM Interfaces and Event Sink

    [ComImport, Guid("b722be7f-4eb0-11b0-b59b-0000f805f530"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleCommandTarget
    {
        [PreserveSig]
        int QueryStatus([In] ref Guid pguidCmdGroup, [In] uint cCmds, [In, Out] IntPtr prgCmds, [In, Out] IntPtr pCmdText);

        [PreserveSig]
        int Exec([In] ref Guid pguidCmdGroup, [In] uint nCmdID, [In] uint nCmdexecopt, [In] IntPtr pvaIn, [In, Out] IntPtr pvaOut);
    }

    [ComImport, Guid("c4d244b0-d43e-11cf-893b-00aa00bdce1a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDocHostShowUI
    {
        [PreserveSig]
        int ShowMessage(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string lpstrText, [MarshalAs(UnmanagedType.LPWStr)] string lpstrCaption, uint dwType, [MarshalAs(UnmanagedType.LPWStr)] string lpstrHelpFile, uint dwHelpContext, out int lpResult);

        [PreserveSig]
        int ShowHelp(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszHelpFile, uint uCommand, uint dwData, [MarshalAs(UnmanagedType.Struct)] object ptMouse, [MarshalAs(UnmanagedType.IDispatch)] object pDispatchObjectHit);
    }

    [ComImport]
    [Guid("34A715A0-6587-11D0-924A-0020AFC7AC4D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [TypeLibType(TypeLibTypeFlags.FHidden)]
    public interface DWebBrowserEvents2
    {
        [DispId(102)]
        void StatusTextChange([In, MarshalAs(UnmanagedType.BStr)] string text);

        [DispId(105)]
        void CommandStateChange([In] int command, [In] bool enable);

        [DispId(108)]
        void ProgressChange([In] int progress, [In] int progressMax);

        [DispId(113)]
        void TitleChange([In, MarshalAs(UnmanagedType.BStr)] string text);

        [DispId(250)]
        void BeforeNavigate2(
            [In, MarshalAs(UnmanagedType.IDispatch)] object pDisp,
            [In, MarshalAs(UnmanagedType.Struct)] ref object URL,
            [In, MarshalAs(UnmanagedType.Struct)] ref object Flags,
            [In, MarshalAs(UnmanagedType.Struct)] ref object TargetFrameName,
            [In, MarshalAs(UnmanagedType.Struct)] ref object PostData,
            [In, MarshalAs(UnmanagedType.Struct)] ref object Headers,
            [In, Out] ref bool Cancel);

        [DispId(251)]
        void NewWindow2(
            [In, Out, MarshalAs(UnmanagedType.IDispatch)] ref object ppDisp,
            [In, Out] ref bool Cancel);

        [DispId(252)]
        void NavigateComplete2(
            [In, MarshalAs(UnmanagedType.IDispatch)] object pDisp,
            [In, MarshalAs(UnmanagedType.Struct)] ref object URL);

        [DispId(259)]
        void DocumentComplete(
            [In, MarshalAs(UnmanagedType.IDispatch)] object pDisp,
            [In, MarshalAs(UnmanagedType.Struct)] ref object URL);

        [DispId(263)]
        void WindowClosing(
            [In] bool isChildWindow,
            [In, Out] ref bool Cancel);

        [DispId(270)]
        void FileDownload(
            [In] bool activeDocument,
            [In, Out] ref bool cancel);

        [DispId(273)]
        void NewWindow3(
            [In, Out, MarshalAs(UnmanagedType.IDispatch)] ref object ppDisp,
            [In, Out] ref bool Cancel,
            [In] uint dwFlags,
            [In, MarshalAs(UnmanagedType.BStr)] string bstrUrlContext,
            [In, MarshalAs(UnmanagedType.BStr)] string bstrUrl);
    }

    internal class WebBrowserExtendedEvents : StandardOleMarshalObject, DWebBrowserEvents2
    {
        private ExtendedWebBrowser browser;

        public WebBrowserExtendedEvents(ExtendedWebBrowser browser)
        {
            this.browser = browser;
        }

        public void StatusTextChange(string text) { }
        public void CommandStateChange(int command, bool enable) { }

        public void ProgressChange(int progress, int progressMax)
        {
            browser.OnProgressChange(progress, progressMax);
        }

        public void TitleChange(string text) { }

        public void BeforeNavigate2(object pDisp, ref object URL, ref object Flags, ref object TargetFrameName, ref object PostData, ref object Headers, ref bool Cancel)
        {
            try
            {
                string u = URL != null ? URL.ToString() : "";
                string f = TargetFrameName != null ? TargetFrameName.ToString() : "";
                NativeMethods.Log("Nav", string.Format("BeforeNavigate2: URL={0}, Frame={1}", u, f));
            }
            catch {}
        }

        public void NewWindow2(ref object ppDisp, ref bool Cancel)
        {
            browser.OnNewWindow2(ref ppDisp, ref Cancel);
        }

        public void NavigateComplete2(object pDisp, ref object URL)
        {
            try
            {
                string u = URL != null ? URL.ToString() : "";
                NativeMethods.Log("Nav", "NavigateComplete2: URL=" + u);
            }
            catch {}
        }

        public void DocumentComplete(object pDisp, ref object URL)
        {
            try
            {
                string u = URL != null ? URL.ToString() : "";
                NativeMethods.Log("Nav", "DocumentComplete: URL=" + u);
            }
            catch {}
        }

        public void WindowClosing(bool isChildWindow, ref bool Cancel)
        {
            browser.OnWindowClosing(isChildWindow, ref Cancel);
        }

        public void FileDownload(bool activeDocument, ref bool cancel)
        {
            try
            {
                NativeMethods.Log("Nav", "FileDownload triggered. activeDocument=" + activeDocument);
            }
            catch {}
            browser.OnFileDownload(activeDocument, ref cancel);
        }

        public void NewWindow3(ref object ppDisp, ref bool Cancel, uint dwFlags, string bstrUrlContext, string bstrUrl)
        {
            browser.OnNewWindow3(ref ppDisp, ref Cancel, dwFlags, bstrUrlContext, bstrUrl);
        }
    }

    #endregion
}
