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

        public event EventHandler<NewWindow2EventArgs> NewWindow2Event;
        public event EventHandler<NewWindow3EventArgs> NewWindow3Event;
        public event EventHandler<ProgressChangeEventArgs> ProgressChangeEvent;
        public event EventHandler<WindowClosingEventArgs> WindowClosingEvent;
        public event EventHandler<FileDownloadEventArgs> FileDownloadEvent;

        public ExtendedWebBrowser()
        {
            // Do NOT set ScriptErrorsSuppressed = true because that suppresses alert() and confirm() dialogs!
            // We suppress only script errors via HtmlWindow.Error event.
            this.ScriptErrorsSuppressed = false;
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

            // Hook Error event on the loaded document window to silence script error popups
            // while preserving window.alert and window.confirm
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
            // Mark as handled to prevent the annoying "A script error has occurred" IE dialog
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
            EventHandler<WindowClosingEventArgs> handler = WindowClosingEvent;
            if (handler != null)
            {
                WindowClosingEventArgs args = new WindowClosingEventArgs(isChildWindow, cancel);
                handler(this, args);
                cancel = args.Cancel;
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

        public void BeforeNavigate2(object pDisp, ref object URL, ref object Flags, ref object TargetFrameName, ref object PostData, ref object Headers, ref bool Cancel) { }

        public void NewWindow2(ref object ppDisp, ref bool Cancel)
        {
            browser.OnNewWindow2(ref ppDisp, ref Cancel);
        }

        public void NavigateComplete2(object pDisp, ref object URL) { }
        public void DocumentComplete(object pDisp, ref object URL) { }

        public void WindowClosing(bool isChildWindow, ref bool Cancel)
        {
            browser.OnWindowClosing(isChildWindow, ref Cancel);
        }

        public void FileDownload(bool activeDocument, ref bool cancel)
        {
            browser.OnFileDownload(activeDocument, ref cancel);
        }

        public void NewWindow3(ref object ppDisp, ref bool Cancel, uint dwFlags, string bstrUrlContext, string bstrUrl)
        {
            browser.OnNewWindow3(ref ppDisp, ref Cancel, dwFlags, bstrUrlContext, bstrUrl);
        }
    }

    #endregion
}
