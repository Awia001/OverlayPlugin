﻿using System;
using System.Collections.Generic;
using CefSharp.OffScreen;
using CefSharp.Structs;
using CefSharp.Enums;
using CefSharp;
using CefSharp.Internals;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace RainbowMage.HtmlRenderer
{
    public class Renderer : IDisposable
    {
        public event EventHandler<BrowserErrorEventArgs> BrowserError;
        public event EventHandler<BrowserLoadEventArgs> BrowserStartLoading;
        public event EventHandler<BrowserLoadEventArgs> BrowserLoad;
        public event EventHandler<BrowserConsoleLogEventArgs> BrowserConsoleLog;

        private ChromiumWebBrowser _browser;
        private List<String> scriptQueue = new List<string>();
        private string urlToLoad = null;
        
        public string OverlayVersion {
            get { return overlayVersion; }
        }

        public string OverlayName {
          get { return overlayName; }
        }

        private int clickCount;
        private MouseButtonType lastClickButton;
        private DateTime lastClickTime;
        private int lastClickPosX;
        private int lastClickPosY;
        private string overlayVersion;
        private string overlayName;

        public Renderer(string overlayVersion, string overlayName, OverlayForm form, object api)
        {
            this.overlayVersion = overlayVersion;
            this.overlayName = overlayName;

            this._browser = new BrowserWrapper("about:blank", automaticallyCreateBrowser: false, form: form);
            _browser.FrameLoadStart += Browser_FrameLoadStart;
            _browser.FrameLoadEnd += Browser_FrameLoadEnd;
            _browser.LoadError += Browser_LoadError;
            _browser.ConsoleMessage += Browser_ConsoleMessage;

            if (api != null)
            {
                _browser.JavascriptObjectRepository.Register("OverlayPluginApi", api, isAsync: true);
                _browser.JavascriptObjectRepository.ObjectBoundInJavascript += JavascriptObjectRepository_ObjectBoundInJavascript;
            }
        }

        private void JavascriptObjectRepository_ObjectBoundInJavascript(object sender, CefSharp.Event.JavascriptBindingCompleteEventArgs e)
        {
            // BrowserConsoleLog(sender, new BrowserConsoleLogEventArgs("Object " + e.ObjectName + " succesfully bound.", "internal", 1));
        }

        private void Browser_FrameLoadStart(object sender, FrameLoadStartEventArgs e)
        {
            var initScript = @"(async () => {
                await CefSharp.BindObjectAsync('OverlayPluginApi');
                OverlayPluginApi.overlayName = " + JsonConvert.SerializeObject(this.overlayName) + @";
                OverlayPluginApi.ready = true;
            })();";
            e.Frame.ExecuteJavaScriptAsync(initScript, "init");

            foreach (var item in this.scriptQueue)
            {
                e.Frame.ExecuteJavaScriptAsync(item, "injectOnLoad");
            }
            this.scriptQueue.Clear();

            BrowserStartLoading?.Invoke(this, new BrowserLoadEventArgs(0, e.Url));
        }

        private void Browser_LoadError(object sender, LoadErrorEventArgs e)
        {
            BrowserError?.Invoke(sender, new BrowserErrorEventArgs(e.ErrorCode, e.ErrorText, e.FailedUrl));
        }

        private void Browser_ConsoleMessage(object sender, ConsoleMessageEventArgs e)
        {
            BrowserConsoleLog?.Invoke(sender, new BrowserConsoleLogEventArgs(e.Message, e.Source, e.Line));
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (urlToLoad != null)
            {
                _browser.Load(urlToLoad);
                urlToLoad = null;
            }

            BrowserLoad?.Invoke(sender, new BrowserLoadEventArgs(e.HttpStatusCode, e.Url));
        }

        public void Load(string url)
        {
            if (this._browser != null && _browser.IsBrowserInitialized)
            {
                this._browser.Load(url);
            } else
            {
                urlToLoad = url;
            }
        }

        public void BeginRender(int width, int height, string url, int maxFrameRate = 30)
        {
            EndRender();

            var cefWindowInfo = new WindowInfo();
            cefWindowInfo.SetAsWindowless(IntPtr.Zero);
            cefWindowInfo.Width = width;
            cefWindowInfo.Height = height;

            var cefBrowserSettings = new BrowserSettings();
            cefBrowserSettings.WindowlessFrameRate = maxFrameRate;
            _browser.CreateBrowser(cefWindowInfo, cefBrowserSettings);

            urlToLoad = url;
        }

        public void EndRender()
        {
            
        }

        public void SetMaxFramerate(int fps)
        {
            if (_browser != null && _browser.IsBrowserInitialized)
            {
                _browser.GetBrowserHost().WindowlessFrameRate = fps;
            }
        }

        public void Reload()
        {
            if (this._browser != null && _browser.IsBrowserInitialized)
            {
                this._browser.Reload();
            }
        }

        public void Resize(int width, int height)
        {
            if (this._browser != null)  // && _browser.IsBrowserInitialized)
            {
                this._browser.Size = new System.Drawing.Size(width, height);
            }
        }

        public void NotifyMoveStarted()
        {
            if (this._browser != null && this._browser.IsBrowserInitialized)
            {
                this._browser.GetBrowserHost().NotifyMoveOrResizeStarted();
            }
        }

        public void SetVisible(bool visible)
        {
            if (this._browser != null && this._browser.IsBrowserInitialized)
            {
                // Sometimes, CEF stops emitting OnPaint events after this call.
                // Not sure why, probably a timing issue.
                // TODO: Debug this further. Will probably require debugging libcef.dll directly.
                //       Modifying _browser.Size immidiately after the WasHidden() call leads to a crash in libcef.dll,
                //       that might be a good place to start investigating.
                //Task.Run(() => this._browser.GetBrowserHost().WasHidden(!visible));
            }
        }

        public void SendMouseMove(int x, int y, MouseButtonType button)
        {
            if (this._browser != null && this._browser.IsBrowserInitialized)
            {
                var host = this._browser.GetBrowserHost();
                var modifiers = CefEventFlags.None;
                if (button == MouseButtonType.Left)
                {
                    modifiers = CefEventFlags.LeftMouseButton;
                }
                else if (button == MouseButtonType.Middle)
                {
                    modifiers = CefEventFlags.MiddleMouseButton;
                }
                else if (button == MouseButtonType.Right)
                {
                    modifiers = CefEventFlags.RightMouseButton;
                }

                var mouseEvent = new MouseEvent(x, y, modifiers);
                host.SendMouseMoveEvent(mouseEvent, false);
            }
        }

        public void SendMouseUpDown(int x, int y, MouseButtonType button, bool isMouseUp)
        {
            if (this._browser != null && this._browser.IsBrowserInitialized)
            {
                var host = this._browser.GetBrowserHost();

                if (!isMouseUp)
                {
                    if (IsContinuousClick(x, y, button))
                    {
                        clickCount++;
                    }
                    else
                    {
                        clickCount = 1;
                    }
                }

                var mouseEvent = new MouseEvent(x, y, CefEventFlags.None);
                host.SendMouseClickEvent(mouseEvent, button, isMouseUp, clickCount);

                lastClickPosX = x;
                lastClickPosY = y;
                lastClickButton = button;
                lastClickTime = DateTime.Now;
            }
        }

        public void SendMouseWheel(int x, int y, int delta, bool isVertical)
        {
            if (this._browser != null && this._browser.IsBrowserInitialized)
            {
                var host = this._browser.GetBrowserHost();
                var mouseEvent = new MouseEvent(x, y, CefEventFlags.None);
                host.SendMouseWheelEvent(mouseEvent, isVertical ? delta : 0, !isVertical ? delta : 0);
            }
        }

        private bool IsContinuousClick(int x, int y, MouseButtonType button)
        {
            // ダブルクリックとして認識するクリックの間隔よりも大きかった場合は継続的なクリックとみなさない
            var delta = (DateTime.Now - lastClickTime).TotalMilliseconds;
            if (delta > System.Windows.Forms.SystemInformation.DoubleClickTime)
            {
                return false;
            }

            // クリック位置が違う、もしくはボタンが違う場合にも継続的なクリックとはみなさない
            if (lastClickPosX != x || lastClickPosY != y || lastClickButton != button)
            {
                return false;
            }

            return true;
        }

        public void SendKeyEvent(KeyEvent keyEvent)
        {
            if (this._browser != null)
            {
                var host = this._browser.GetBrowserHost();

                host.SendKeyEvent(keyEvent);
            }
        }

        public void showDevTools(bool firstwindow = true)
        {
            if (_browser != null)
            {
                WindowInfo wi = new WindowInfo();
                wi.SetAsPopup(_browser.GetBrowserHost().GetWindowHandle(), "DevTools");
                _browser.GetBrowserHost().ShowDevTools(wi);
            }
        }

        public void Dispose()
        {
            if (this._browser != null)
            {
                this._browser.Dispose();
                this._browser = null;
            }
        }

        static bool initialized = false;

        public static void Initialize(string pluginDirectory)
        {
            if (!initialized)
            {
                Cef.EnableHighDPISupport();

                var lang = System.Globalization.CultureInfo.CurrentCulture.Name;
                var langPak = Path.Combine(pluginDirectory, Environment.Is64BitProcess ? "x64" : "x86",
                    "locales", lang + ".pak");

                // Fall back to en-US if we can't find the current locale.
                if (!File.Exists(langPak))
                {
                    lang = "en-US";
                }

                var cefSettings = new CefSettings
                {
                    WindowlessRenderingEnabled = true,
                    Locale = lang,
                    CachePath = Path.Combine(pluginDirectory, "Cache"),
                    MultiThreadedMessageLoop = true,
#if DEBUG
                    LogSeverity = LogSeverity.Info,
#else
                    LogSeverity = LogSeverity.Disable,
#endif
                    BrowserSubprocessPath = Path.Combine(pluginDirectory,
                                           Environment.Is64BitProcess ? "x64" : "x86",
                                           "CefSharp.BrowserSubprocess.exe"),
                };

                // Necessary to avoid input lag with a framerate limit below 60.
                cefSettings.CefCommandLineArgs["enable-begin-frame-scheduling"] = "1";

                // Allow websites to play sound even if the user never interacted with that site (pretty common for our overlays)
                cefSettings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";

                cefSettings.EnableAudio();

                // Enables software compositing instead of GPU compositing -> less CPU load but no WebGL
                cefSettings.SetOffScreenRenderingBestPerformanceArgs();

                Cef.Initialize(cefSettings, performDependencyCheck: true, browserProcessHandler: null);

                initialized = true;
            }
        }

        public static void Shutdown()
        {
            if (initialized)
            {
                Cef.Shutdown();
            }
        }

        public void ExecuteScript(string script)
        {
            if (_browser != null && _browser.IsBrowserInitialized)
            {
                var frame = _browser.GetMainFrame();
                frame.ExecuteJavaScriptAsync(script, "injected");
            }
            else
            {
                this.scriptQueue.Add(script);
            }
        }

        // IJavascriptCallback can't be used outside HtmlRenderer. This helper allows other code
        // to invoke callbacks regardless.

        public static void ExecuteCallback(object callback, object param)
        {
            if (callback.GetType().GetInterface("IJavascriptCallback") == null)
            {
                throw new Exception("Invalid parameter passed for callback!");
            }

            var cb = (IJavascriptCallback) callback;
            using (cb)
            {
                if (cb.CanExecute)
                {
                    cb.ExecuteAsync(param);
                }
            }
        }
    }

    public class BrowserErrorEventArgs : EventArgs
    {
        public string ErrorCode { get; private set; }
        public string ErrorText { get; private set; }
        public string Url { get; private set; }
        public BrowserErrorEventArgs(CefErrorCode errorCode, string errorText, string url)
        {
            this.ErrorCode = errorCode.ToString();
            this.ErrorText = errorText;
            this.Url = url;
        }
    }
    
    public class BrowserLoadEventArgs : EventArgs
    {
        public int HttpStatusCode { get; private set; }
        public string Url { get; private set; }
        public BrowserLoadEventArgs(int httpStatusCode, string url)
        {
            this.HttpStatusCode = httpStatusCode;
            this.Url = url;
        }
    }

    public class BrowserConsoleLogEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public string Source { get; private set; }
        public int Line { get; private set; }
        public BrowserConsoleLogEventArgs(string message, string source, int line)
        {
            this.Message = message;
            this.Source = source;
            this.Line = line;
        }
    }

    public class RendererFeatureRequestEventArgs : EventArgs
    {
        public string Request { get; private set; }
        public RendererFeatureRequestEventArgs(string request)
        {
            this.Request = request;
        }
    }

    public class HandlerCallEventArgs : EventArgs
    {
        public JObject Payload { get; private set; }
        public Action<JObject> Callback { get; private set; }
        public HandlerCallEventArgs(JObject payload, Action<JObject> callback)
        {
            this.Payload = payload;
            this.Callback = callback;
        }
    }

    public class BrowserWrapper : ChromiumWebBrowser, IRenderWebBrowser
    {
        OverlayForm form;

        public BrowserWrapper(string address = "", BrowserSettings browserSettings = null,
            RequestContext requestContext = null, bool automaticallyCreateBrowser = true, OverlayForm form = null) :
            base(address, browserSettings, requestContext, automaticallyCreateBrowser)
        {
            this.form = form;
        }

        void IRenderWebBrowser.OnPaint(PaintElementType type, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
            form.RenderFrame(dirtyRect, buffer, width, height);
        }

        void IRenderWebBrowser.OnCursorChange(IntPtr cursor, CursorType type, CursorInfo customCursorInfo)
        {
            form.Cursor = new Cursor(cursor);
        }
    }
}
