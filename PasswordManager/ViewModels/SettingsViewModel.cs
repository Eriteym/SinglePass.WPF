﻿using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Services;
using PasswordManager.Views;
using PasswordManager.Views.InputBox;
using PasswordManager.Views.MessageBox;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class SettingsViewModel : NavigationItemViewModel
    {
        #region Design time instance
        private static readonly Lazy<SettingsViewModel> _lazy = new(GetDesignTimeVM);
        public static SettingsViewModel DesignTimeInstance => _lazy.Value;

        private static SettingsViewModel GetDesignTimeVM()
        {
            var vm = new SettingsViewModel();
            vm.FetchingUserInfo = true;
            return vm;
        }
        #endregion

        private readonly ThemeService _themeService;
        private readonly AppSettingsService _appSettingsService;
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly CloudServiceProvider _cloudServiceProvider;
        private readonly CryptoService _cryptoService;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private AsyncRelayCommand<CloudType> _loginCommand;
        private string _googleProfileUrl;
        private string _googleUserName;
        private bool _fetchingUserInfo;

        public BaseTheme ThemeMode
        {
            get => _appSettingsService.ThemeMode;
            set
            {
                _appSettingsService.ThemeMode = value;
                _appSettingsService.Save();
                OnPropertyChanged();
                _themeService.ThemeMode = value;
            }
        }

        public bool GoogleDriveEnabled
        {
            get => _appSettingsService.GoogleDriveEnabled;
            set
            {
                _appSettingsService.GoogleDriveEnabled = value;
                OnPropertyChanged();
            }
        }

        public string GoogleProfileUrl
        {
            get => _googleProfileUrl;
            set => SetProperty(ref _googleProfileUrl, value);
        }

        public string GoogleUserName
        {
            get => _googleUserName;
            set => SetProperty(ref _googleUserName, value);
        }

        public bool FetchingUserInfo
        {
            get => _fetchingUserInfo;
            set => SetProperty(ref _fetchingUserInfo, value);
        }

        public AsyncRelayCommand<CloudType> LoginCommand => _loginCommand ??= new AsyncRelayCommand<CloudType>(Login);

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            ILogger<SettingsViewModel> logger,
            CloudServiceProvider cloudServiceProvider,
            CryptoService cryptoService)
        {
            Name = "Settings";
            ItemIndex = SettingsNavigationItemIndex;
            IconKind = PackIconKind.Settings;

            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _logger = logger;
            _cloudServiceProvider = cloudServiceProvider;
            _cryptoService = cryptoService;
            //_credentialsCryptoService = credentialsCryptoService;
        }

        internal async Task FetchUserInfoIfRequired()
        {
            try
            {
                if (GoogleDriveEnabled
                    && !FetchingUserInfo
                    && string.IsNullOrWhiteSpace(GoogleProfileUrl)
                    && string.IsNullOrWhiteSpace(GoogleUserName))
                {
                    await FetchUserInfoFromCloud(CloudType.GoogleDrive, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        private async Task Login(CloudType cloudType)
        {
            var windowDialogName = MvvmHelper.MainWindowDialogName;
            var authorizing = false;
            switch (cloudType)
            {
                case CloudType.GoogleDrive:
                    authorizing = !GoogleDriveEnabled;
                    break;
            }
            var cloudService = _cloudServiceProvider.GetCloudService(cloudType);

            try
            {
                if (authorizing)
                {
                    // Authorize
                    var processingControl = new ProcessingControl("Authorizing...", "Please, continue authorization or cancel it.", windowDialogName);
                    var token = processingControl.ViewModel.CancellationToken;
                    _ = DialogHost.Show(processingControl, windowDialogName); // Don't await dialog host

                    await cloudService.AuthorizationBroker.AuthorizeAsync(token);
                    _logger.LogInformation($"Authorization process to {cloudType} has been complete.");
                    GoogleDriveEnabled = true;
                    await _appSettingsService.Save();
                    _ = FetchUserInfoFromCloud(cloudType, CancellationToken.None); // Don't await set user info for now

                    processingControl.ViewModel.HeadText = "Checking file...";
                    processingControl.ViewModel.MidText = string.Format("Please, wait while we checking existing passwords on your account. The target file is {0}", Helpers.Constants.PasswordsFileName);

                    using var fileStream = await cloudService.Download(Helpers.Constants.PasswordsFileName, token);
                    if (fileStream != null)
                    {
                        // File is here
                        DialogHost.Close(windowDialogName);
                        var result = await MaterialMessageBox.ShowAsync(
                            "Merge existing credentials?",
                            "Yes - merge\r\nNo - replace from cloud",
                            MaterialMessageBoxButtons.YesNo,
                            windowDialogName,
                            PackIconKind.QuestionMark);

                        var success = false;
                        List<Credential> cloudCredentials = null;
                        do
                        {
                            var cloudPassword = await MaterialInputBox.ShowAsync(
                                "Input password of cloud file",
                                "Password",
                                windowDialogName);

                            try
                            {
                                cloudCredentials = _cryptoService.DecryptFromStream<List<Credential>>(fileStream, cloudPassword);
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, string.Empty);
                            }
                        }
                        while (!success);

                        switch (result)
                        {
                            case MaterialDialogResult.Yes:
                                // Merge
                                break;
                            case MaterialDialogResult.No:
                                // Replace
                                break;
                        }
                    }
                    else
                    {
                        // File doesn't exists, just replace
                        
                    }
                }
                else
                {
                    // Revoke
                    var processingControl = new ProcessingControl("Signing out...", "Please, wait.", windowDialogName);
                    var token = processingControl.ViewModel.CancellationToken;
                    _ = DialogHost.Show(processingControl, windowDialogName); // Don't await dialog host

                    await cloudService.AuthorizationBroker.RevokeToken(token);

                    GoogleDriveEnabled = false;
                    await _appSettingsService.Save();

                    ClearUserInfo(cloudType);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Authorization process to Google Drive has been cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                if (DialogHost.IsDialogOpen(windowDialogName))
                    DialogHost.Close(windowDialogName);
            }
        }

        private void ClearUserInfo(CloudType cloudType)
        {
            switch (cloudType)
            {
                case CloudType.GoogleDrive:
                    GoogleProfileUrl = null;
                    GoogleUserName = null;
                    break;
            }
        }

        private async Task FetchUserInfoFromCloud(CloudType cloudType, CancellationToken cancellationToken)
        {
            try
            {
                FetchingUserInfo = true;
                var cloudService = _cloudServiceProvider.GetCloudService(cloudType);
                var userInfo = await cloudService.GetUserInfo(cancellationToken);
                switch (cloudType)
                {
                    case CloudType.GoogleDrive:
                        GoogleProfileUrl = userInfo.ProfileUrl;
                        GoogleUserName = userInfo.UserName;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                FetchingUserInfo = false;
            }
        }
    }
}
