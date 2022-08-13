﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Extensions.Logging;
using SinglePass.FavIcons.Application;
using SinglePass.FavIcons.Repository;
using SinglePass.WPF.Authorization.Brokers;
using SinglePass.WPF.Authorization.TokenHolders;
using SinglePass.WPF.Clouds.Services;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Hotkeys;
using SinglePass.WPF.Options;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.Views;
using System;
using System.Threading;
using System.Windows;

namespace SinglePass.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private readonly Mutex _mutex;
        private static IConfiguration _configuration;

        private Logger _logger;
        private TrayIcon _trayIcon;

        public IHost Host { get; private set; }
        private bool IsFirstInstance { get; }

        public App()
        {
            _mutex = new Mutex(true, "SinglePass_CBD9AADE-1A82-48A2-9F7F-4F0EAAABEA30", out bool isFirstInstance);
            IsFirstInstance = isFirstInstance;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.Error(e.Exception, "Dispatcher unhandled exception");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger?.Error(e.ExceptionObject as Exception, "Domain unhandled exception");
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            InitializeComponent();

            // Override culture
            //SinglePass.Language.Properties.Resources.Culture = new System.Globalization.CultureInfo("en-US");

            if (IsFirstInstance)
            {
                // Welcome window
                var welcomeWindow = new WelcomeWindow();
                welcomeWindow.Show();

                _configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                _logger = LogManager.Setup()
                    .LoadConfigurationFromSection(_configuration)
                    .GetCurrentClassLogger();

                Host = CreateHostBuilder().Build();
                _logger.Info("Log session started!");

                Constants.EnsurePaths();

                // Resolve theme
                var themeService = Host.Services.GetService<ThemeService>();
                themeService.Init();

                // Create tray icon
                _trayIcon = new TrayIcon();

                // Login
                using (var loginScope = Host.Services.CreateScope())
                {
                    var loginWindow = Host.Services.GetService<LoginWindow>();
                    welcomeWindow.Close();
                    bool? dialogResult = loginWindow.ShowDialog(); // Stop here

                    if (dialogResult != true)
                    {
                        Shutdown();
                        return;
                    }
                }

                // Open main window
                var mainWindow = Host.Services.GetService<MainWindow>();
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }

        private IHostBuilder CreateHostBuilder() =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddOptions();

                // NLog
                services.AddLogging(lb =>
                {
                    lb.ClearProviders();
                    lb.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    lb.AddNLog(_configuration);
                });

                services.AddHttpClient();

                // Clouds
                // Google
                services.Configure<GoogleDriveConfig>(_configuration.GetSection("Settings:GoogleDriveConfig"));
                services.AddTransient<GoogleAuthorizationBroker>();
                services.AddTransient<GoogleDriveTokenHolder>();
                services.AddTransient<GoogleDriveCloudService>();
                services.AddTransient<CryptoService>();
                services.AddSingleton<CloudServiceProvider>();

                // Windows
                services.AddScoped<LoginWindow>();
                services.AddScoped<LoginWindowViewModel>();

                services.AddScoped<MainWindow>();
                services.AddScoped<MainWindowViewModel>();
                services.AddScoped<PasswordsViewModel>();
                services.AddScoped<CloudSyncViewModel>();
                services.AddScoped<SettingsViewModel>();
                services.AddScoped<CredentialsDialogViewModel>();

                services.AddTransient<PopupControl>();
                services.AddTransient<PopupViewModel>();

                // Main services
                services.AddSingleton<CredentialsCryptoService>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<AppSettingsService>();
                services.AddSingleton<SyncService>();
                services.AddSingleton<HotkeysService>();
                services.AddSingleton<ImageService>();
                services.AddSingleton<CredentialViewModelFactory>();
                services.AddSingleton<AddressBarExtractor>();

                // favicons
                services.AddSingleton<IFavIconCollector, FavIconCollector>();
                services.Configure<FavIconCacheOptions>(_configuration.GetSection("FavIconCacheOptions"));
                services.AddScoped<FavIconCacheService>();
                services.AddScoped<IFavIconRepository, FavIconRepository>();
                services.AddDbContext<FavIconDbContext>((sp, options) => options.UseSqlite(sp.GetService<IOptions<FavIconCacheOptions>>().Value.ConnectionString));
            });

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _logger?.Info($"The application is shutting down...{Environment.NewLine}");
        }
    }
}