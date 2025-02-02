﻿using Advanced_Combat_Tracker;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;
using RainbowMage.HtmlRenderer;
using System;
using System.Drawing;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using RainbowMage.OverlayPlugin.Overlays;
using System.Threading.Tasks;

namespace RainbowMage.OverlayPlugin
{
    public class PluginMain
    {
        TabPage tabPage;
        Label label;
        ControlPanel controlPanel;

        TabPage wsTabPage;
        WSConfigPanel wsConfigPanel;

        internal System.Timers.Timer xivWindowTimer;

        internal PluginConfig Config { get; private set; }
        internal List<IOverlay> Overlays { get; private set; }

        public static Logger Logger { get; private set; }
        internal static string PluginDirectory { get; private set; }

        public PluginMain(string pluginDirectory, Logger logger)
        {
            PluginDirectory = pluginDirectory;
            Logger = logger;

            Registry.Register(this);
        }

        /// <summary>
        /// プラグインが有効化されたときに呼び出されます。
        /// </summary>
        /// <param name="pluginScreenSpace"></param>
        /// <param name="pluginStatusText"></param>
        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            try
            {
                this.tabPage = pluginScreenSpace;
                this.label = pluginStatusText;

#if DEBUG
                Logger.Log(LogLevel.Warning, "##################################");
                Logger.Log(LogLevel.Warning, "    THIS IS THE DEBUG BUILD");
                Logger.Log(LogLevel.Warning, "##################################");
#endif

                Logger.Log(LogLevel.Info, "InitPlugin: PluginDirectory = {0}", PluginDirectory);

#if DEBUG
                Stopwatch watch = new Stopwatch();
                watch.Start();
#endif

                try
                {
                    Renderer.Initialize(PluginDirectory);
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Error, "InitPlugin: {0}", e);
                }

#if DEBUG
                Logger.Log(LogLevel.Debug, "CEF init took {0}s.", watch.Elapsed.TotalSeconds);
                watch.Reset();
#endif

                FFXIVExportVariables.Init();

                // プラグイン読み込み
                LoadAddons();

                // コンフィグ系読み込み
                LoadConfig();

#if DEBUG
                Logger.Log(LogLevel.Debug, "Addon and config load took {0}s.", watch.Elapsed.TotalSeconds);
                watch.Reset();
#endif

                if (Config.WSServerRunning)
                {
                    try
                    {
                        WSServer.Initialize();
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogLevel.Error, "InitPlugin: {0}", e);
                    }
                }

                // プラグイン間のメッセージ関連
                OverlayApi.BroadcastMessage += (o, e) =>
                {
                    Task.Run(() =>
                    {
                        foreach (var overlay in this.Overlays)
                        {
                            overlay.SendMessage(e.Message);
                        }
                    });
                };
                OverlayApi.SendMessage += (o, e) =>
                {
                    Task.Run(() =>
                    {
                        var targetOverlay = this.Overlays.FirstOrDefault(x => x.Name == e.Target);
                        if (targetOverlay != null)
                        {
                            targetOverlay.SendMessage(e.Message);
                        }
                    });
                };
                OverlayApi.OverlayMessage += (o, e) =>
                {
                    Task.Run(() =>
                    {
                        var targetOverlay = this.Overlays.FirstOrDefault(x => x.Name == e.Target);
                        if (targetOverlay != null)
                        {
                            targetOverlay.OverlayMessage(e.Message);
                        }
                    });
                };

#if DEBUG
                watch.Reset();
#endif
                InitializeOverlays();

#if DEBUG
                Logger.Log(LogLevel.Debug, "ES and overlay init took {0}s.", watch.Elapsed.TotalSeconds);
                watch.Stop();
#endif

                // コンフィグUI系初期化
                this.controlPanel = new ControlPanel(this, this.Config);
                this.controlPanel.Dock = DockStyle.Fill;
                this.tabPage.Controls.Add(this.controlPanel);
                this.tabPage.Name = "OverlayPlugin";

                this.wsConfigPanel = new WSConfigPanel(this.Config);
                this.wsConfigPanel.Dock = DockStyle.Fill;

                this.wsTabPage = new TabPage("OverlayPlugin WSServer");
                this.wsTabPage.Controls.Add(wsConfigPanel);
                ((TabControl)this.tabPage.Parent).TabPages.Add(this.wsTabPage);
                
                Logger.Log(LogLevel.Info, "InitPlugin: Initialized.");
                this.label.Text = "Initialized.";
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, "InitPlugin: {0}", e.ToString());
                MessageBox.Show(e.ToString());

                throw;
            }
        }

        /// <summary>
        /// コンフィグのオーバーレイ設定を基に、オーバーレイを初期化・登録します。
        /// </summary>
        private void InitializeOverlays()
        {
            // オーバーレイ初期化
            this.Overlays = new List<IOverlay>();
            foreach (var overlayConfig in this.Config.Overlays)
            {
                var parameters = new NamedParameterOverloads();
                parameters["config"] = overlayConfig;
                parameters["name"] = overlayConfig.Name;

                var overlay = (IOverlay) Registry.Container.Resolve(overlayConfig.OverlayType, parameters);
                if (overlay != null)
                {
                    RegisterOverlay(overlay);
                }
                else
                {
                    Logger.Log(LogLevel.Error, "InitPlugin: Could not find addon for {0}.", overlayConfig.Name);
                }
            }

            xivWindowTimer = new System.Timers.Timer();
            xivWindowTimer.Interval = 1000;
            xivWindowTimer.Elapsed += (o, e) =>
            {
                if (Config.HideOverlaysWhenNotActive)
                {
                    try
                    {
                        var shouldBeVisible = true;

                        try
                        {
                            uint pid;
                            var hWndFg = NativeMethods.GetForegroundWindow();
                            if (hWndFg == IntPtr.Zero)
                            {
                                return;
                            }
                            NativeMethods.GetWindowThreadProcessId(hWndFg, out pid);
                            var exePath = Process.GetProcessById((int)pid).MainModule.FileName;

                            if (Path.GetFileName(exePath.ToString()) == "ffxiv.exe" ||
                                Path.GetFileName(exePath.ToString()) == "ffxiv_dx11.exe" ||
                                exePath.ToString() == Process.GetCurrentProcess().MainModule.FileName)
                            {
                                shouldBeVisible = true;
                            }
                            else
                            {
                                shouldBeVisible = false;
                            }
                        }
                        catch (System.ComponentModel.Win32Exception ex)
                        {
                            // Ignore access denied errors. Those usually happen if the foreground window is running with
                            // admin permissions but we are not.
                            if (ex.ErrorCode == -2147467259)  // 0x80004005
                            {
                                shouldBeVisible = false;
                            }
                            else
                            {
                                Logger.Log(LogLevel.Error, "XivWindowWatcher: {0}", ex.ToString());
                            }
                        }

                        foreach (var overlay in this.Overlays)
                        {
                            if (overlay.Config.IsVisible) overlay.Visible = shouldBeVisible;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "XivWindowWatcher: {0}", ex.ToString());
                    }
                }
            };

            xivWindowTimer.Start();
        }

        /// <summary>
        /// オーバーレイを登録します。
        /// </summary>
        /// <param name="overlay"></param>
        internal void RegisterOverlay(IOverlay overlay)
        {
            overlay.OnLog += (o, e) => Logger.Log(e.Level, e.Message);
            overlay.Start();
            this.Overlays.Add(overlay);
        }

        /// <summary>
        /// 登録されているオーバーレイを削除します。
        /// </summary>
        /// <param name="overlay">削除するオーバーレイ。</param>
        internal void RemoveOverlay(IOverlay overlay)
        {
            this.Overlays.Remove(overlay);
            overlay.Dispose();
        }

        /// <summary>
        /// プラグインが無効化されたときに呼び出されます。
        /// </summary>
        public void DeInitPlugin()
        {
            xivWindowTimer.Stop();
            xivWindowTimer.Dispose();

            SaveConfig();

            controlPanel.Dispose();

            foreach (var overlay in this.Overlays)
            {
                overlay.Dispose();
            }

            this.Overlays.Clear();

            try { WSServer.Stop(); }
            catch { }

            ((TabControl)this.wsTabPage.Parent).TabPages.Remove(this.wsTabPage);

            Logger.Log(LogLevel.Info, "DeInitPlugin: Finalized.");
            this.label.Text = "Finalized.";
        }

        /// <summary>
        /// アドオンを読み込みます。
        /// </summary>
        private void LoadAddons()
        {
            try
            {
                // <プラグイン本体があるディレクトリ>\plugins\*.dll を検索する
                var directory = Path.Combine(PluginDirectory, "addons");
                if (!Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogLevel.Error, "LoadAddons: {0}", e);
                    }
                }

                var Addons = new List<IOverlayAddonV2>();

                Registry.RegisterOverlay<MiniParseOverlay>();
                Registry.RegisterOverlay<SpellTimerOverlay>();
                Registry.RegisterOverlay<LabelOverlay>();

                Registry.RegisterEventSource<MiniParseEventSource>();

                var version = typeof(PluginMain).Assembly.GetName().Version;

                foreach (var plugin in ActGlobals.oFormActMain.ActPlugins)
                {
                    if (plugin.pluginObj == null) continue;

                    try
                    {
                        if (plugin.pluginObj.GetType().GetInterface(typeof(IOverlayAddonV2).FullName) != null)
                        {
                            try
                            {
                                // プラグインのインスタンスを生成し、アドオンリストに追加する
                                var addon = (IOverlayAddonV2)plugin.pluginObj;
                                addon.Init();

                                Logger.Log(LogLevel.Info, "LoadAddons: {0}: Initialized", plugin.lblPluginTitle.Text);
                            }
                            catch (Exception e)
                            {
                                Logger.Log(LogLevel.Error, "LoadAddons: {0}: {1}", plugin.lblPluginTitle.Text, e);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogLevel.Error, "LoadAddons: {0}: {1}", plugin.lblPluginTitle.Text, e);
                    }
                }

                foreach (var pluginFile in Directory.GetFiles(directory, "*.dll"))
                {
                    try
                    {
                        Logger.Log(LogLevel.Info, "LoadAddons: {0}", pluginFile);

                        // アセンブリが見つかったら読み込む
                        var asm = Assembly.LoadFrom(pluginFile);
                        var incompatible = asm.GetReferencedAssemblies().Where(a => a.FullName != null && a.FullName.StartsWith("Xilium.CefGlue")).Count() > 0;
                        if (incompatible)
                        {
                            Logger.Log(LogLevel.Error, "LoadAddons: Skipped {0} because it's incompatible with this version of OverlayPlugin.", asm.FullName);
                            continue;
                        }

                        // アセンブリから IOverlayAddon を実装した public クラスを列挙し...
                        var types = asm.GetExportedTypes().Where(t => 
                                t.GetInterface(typeof(IOverlayAddonV2).FullName) != null && t.IsClass);
                        foreach (var type in types)
                        {
                            try
                            {
                                // プラグインのインスタンスを生成し、アドオンリストに追加する
                                var addon = (IOverlayAddonV2)asm.CreateInstance(type.FullName);
                                addon.Init();

                                Logger.Log(LogLevel.Info, "LoadAddons: {0}: Initialized", type.FullName);
                            }
                            catch (Exception e)
                            {
                                Logger.Log(LogLevel.Error, "LoadAddons: {0}: {1}", type.FullName, e);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogLevel.Error, "LoadAddons: {0}: {1}", pluginFile, e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, "LoadAddons: {0}", e);
            }
        }

        /// <summary>
        /// 設定を読み込みます。
        /// </summary>
        private void LoadConfig()
        {
            var found = true;
            try
            {
                Config = PluginConfig.LoadJson(GetConfigPath());
            }
            catch (FileNotFoundException)
            {
                Config = null;
                found = false;
            }
            catch (Exception e)
            {
                Config = null;
                Logger.Log(LogLevel.Error, "LoadConfig: {0}", e);
            }

            if (!found)
            {
                try
                {
                    Config = PluginConfig.LoadXml(PluginDirectory, GetConfigPath(true));
                }
                catch (Exception e)
                {
                    // 設定ファイルが存在しない、もしくは破損している場合は作り直す
                    Logger.Log(LogLevel.Warning, "LoadConfig: {0}", e);
                    Config = null;
                }
            }

            if (Config == null)
            {
                Logger.Log(LogLevel.Info, "LoadConfig: Creating new configuration.");
                Config = new PluginConfig();
                Config.SetDefaultOverlayConfigs(PluginDirectory);
            }

            Registry.Register(Config);
            Registry.Register<IPluginConfig>(Config);

            foreach (var es in Registry.EventSources)
            {
                es.LoadConfig(Config);
                es.Start();
            }
        }

        /// <summary>
        /// 設定を保存します。
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                foreach (var overlay in this.Overlays)
                {
                    overlay.SavePositionAndSize();
                }

                Config.SaveJson(GetConfigPath());
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, "SaveConfig: {0}", e);
                MessageBox.Show(e.ToString());
            }
        }

        /// <summary>
        /// 設定ファイルのパスを取得します。
        /// </summary>
        /// <returns></returns>
        private static string GetConfigPath(bool xml = false)
        {
            var path = System.IO.Path.Combine(
                ActGlobals.oFormActMain.AppDataFolder.FullName,
                "Config",
                "RainbowMage.OverlayPlugin.config." + (xml ? "xml" : "json"));

            return path;
        }
    }
}
