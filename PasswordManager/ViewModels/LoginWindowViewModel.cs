﻿using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class LoginWindowViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<LoginWindowViewModel> _lazy = new Lazy<LoginWindowViewModel>(() => new LoginWindowViewModel(null, null));
        public static LoginWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion

        public event Action Accept;

        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly ILogger<LoginWindowViewModel> _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _credentialsFileExist;
        private AsyncRelayCommand _loadCredentialsCommand;
        private AsyncRelayCommand _loadingCommand;
        private bool _loading;
        private string _helperText;
        private string _hintText = "Password";

        public AsyncRelayCommand LoadCredentialsCommand => _loadCredentialsCommand ??= new AsyncRelayCommand(LoadCredentialsAsync);
        public AsyncRelayCommand LoadingCommand => _loadingCommand ??= new AsyncRelayCommand(LoadingAsync);

        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }

        public string HelperText
        {
            get => _helperText;
            set => SetProperty(ref _helperText, value);
        }

        public string HintText
        {
            get => _hintText;
            set => SetProperty(ref _hintText, value);
        }

        public string Password { get; set; }

        public LoginWindowViewModel(
            CredentialsCryptoService credentialsCryptoService,
            ILogger<LoginWindowViewModel> logger)
        {
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
        }

        public async Task LoadCredentialsAsync()
        {
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
            {
                HelperText = "Minimum 8 characters";
                return;
            }
            else
            {
                HelperText = string.Empty;
            }

            try
            {
                Loading = true;
                if (!_credentialsFileExist)
                {
                    await _credentialsCryptoService.SetNewPassword(Password);
                    Accept?.Invoke();
                }
                else
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource = new();
                    var cancellationToken = _cancellationTokenSource.Token;

                    var loadingResult = await _credentialsCryptoService.LoadCredentialsAsync(Password);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!loadingResult)
                    {
                        HelperText = "Password is incorrect";
                    }
                    else
                    {
                        Accept?.Invoke();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                Loading = false;
            }
        }

        private async Task LoadingAsync()
        {
            _credentialsFileExist = await _credentialsCryptoService.IsCredentialsFileExistAsync();
            if (!_credentialsFileExist)
            {
                HintText = "New password";
            }
        }
    }
}
