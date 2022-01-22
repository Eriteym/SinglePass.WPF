﻿using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.Input;
using NLog;
using PasswordManager.Collections;
using PasswordManager.Enums;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Services;
using PasswordManager.Views.MessageBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PasswordManager.ViewModels
{
    public class PasswordsViewModel : NavigationItemViewModel
    {
        #region Design time instance
        private static readonly Lazy<PasswordsViewModel> _lazy = new(GetDesignTimeVM);
        public static PasswordsViewModel DesignTimeInstance => _lazy.Value;

        private static PasswordsViewModel GetDesignTimeVM()
        {
            var vm = new PasswordsViewModel();
            var cred = new Credential();
            cred.NameField.Value = "Test";
            cred.LoginField.Value = "TestLogin";
            cred.PasswordField.Value = "TestPass";
            cred.OtherField.Value = "TestOther";
            var credVm = new CredentialViewModel(cred);
            vm.DisplayedCredentials.Add(credVm);
            return vm;
        }
        #endregion

        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;
        private readonly List<CredentialViewModel> _credentials = new();

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();
        public CredentialsDialogViewModel ActiveCredentialDialogViewModel { get; }

        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set
            {
                SetProperty(ref _selectedCredential, value);
                ActiveCredentialDialogViewModel.Mode = CredentialsDialogMode.View;
                ActiveCredentialDialogViewModel.CredentialViewModel = value;
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                _ = FilterCredentialsAsync(_searchText);
            }
        }

        private bool _searchTextFocused;
        public bool SearchTextFocused
        {
            get => _searchTextFocused;
            set => SetProperty(ref _searchTextFocused, value);
        }

        private PasswordsViewModel() { }

        public PasswordsViewModel(
            SettingsService settingsService,
            ILogger logger,
            CredentialsDialogViewModel credentialsDialogViewModel)
        {
            _settingsService = settingsService;
            _logger = logger;

            Name = "Credentials";
            ItemIndex = PasswordsNavigationItemIndex;
            IconKind = PackIconKind.Password;
            ActiveCredentialDialogViewModel = credentialsDialogViewModel;
            ActiveCredentialDialogViewModel.Accept += ActiveCredentialDialogViewModel_Accept;
            ActiveCredentialDialogViewModel.Cancel += ActiveCredentialDialogViewModel_Cancel;
            ActiveCredentialDialogViewModel.Delete += ActiveCredentialDialogViewModel_Delete;
        }

        private async void ActiveCredentialDialogViewModel_Delete(CredentialViewModel credVM)
        {
            var result = await MaterialMessageBox.ShowAsync(
                "Delete credential?",
                $"Name: {credVM.NameFieldVM.Value}",
                MaterialMessageBoxButtons.YesNo,
                MvvmHelper.MainWindowDialogName,
                PackIconKind.Delete);
            if (result == MaterialDialogResult.Yes)
            {
                await _settingsService.DeleteCredential(credVM.Model);
                _credentials.Remove(credVM);
                DisplayedCredentials.Remove(credVM);
            }
        }

        private void ActiveCredentialDialogViewModel_Cancel()
        {
            ActiveCredentialDialogViewModel.CredentialViewModel = SelectedCredential;
        }

        private async void ActiveCredentialDialogViewModel_Accept(CredentialViewModel credVM)
        {
            if (ActiveCredentialDialogViewModel.Mode == CredentialsDialogMode.New)
            {
                await _settingsService.AddCredential(credVM.Model);
                _credentials.Add(credVM);
                await FilterCredentialsAsync(null);
            }
            else if (ActiveCredentialDialogViewModel.Mode == CredentialsDialogMode.Edit)
            {
                await _settingsService.EditCredential(credVM.Model);
                var staleCredVM = _credentials.FirstOrDefault(c => c.Model.Equals(credVM.Model));
                var staleIndex = _credentials.IndexOf(staleCredVM);
                _credentials.Remove(staleCredVM);
                _credentials.Insert(staleIndex, credVM);
                await FilterCredentialsAsync(null);
            }

            SelectedCredential = credVM;
        }

        public async Task LoadCredentialsAsync()
        {
            try
            {
                Loading = true;
                await _settingsService.LoadCredentialsAsync();
                var credentials = _settingsService.Credentials;
                using var delayed = DisplayedCredentials.DelayNotifications();
                foreach (var cred in credentials)
                {
                    var credVM = new CredentialViewModel(cred);
                    _credentials.Add(credVM);
                    delayed.Add(credVM);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task FilterCredentialsAsync(string filterText)
        {
            try
            {
                Loading = true;
                List<CredentialViewModel> filteredCredentials = null;

                if (string.IsNullOrEmpty(filterText))
                {
                    filteredCredentials = _credentials;
                }
                else
                {
                    filteredCredentials = await Task.Run(() =>
                    {
                        var fCreds = new List<CredentialViewModel>();
                        foreach (var cred in _credentials)
                        {
                            if (cred.NameFieldVM.Value.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                fCreds.Add(cred);
                            }
                        }
                        return fCreds;
                    });
                }

                DisplayedCredentials = new ObservableCollectionDelayed<CredentialViewModel>(filteredCredentials);
                OnPropertyChanged(nameof(DisplayedCredentials));

                // Selected credential always first according to search request
                SelectedCredential = DisplayedCredentials.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            finally
            {
                Loading = false;
            }
        }

        private void AddCredential()
        {
            ActiveCredentialDialogViewModel.CredentialViewModel = new CredentialViewModel(new Credential());
            ActiveCredentialDialogViewModel.Mode = CredentialsDialogMode.New;
        }

        private void EditCredential(CredentialViewModel credentialVM)
        {
            ActiveCredentialDialogViewModel.CredentialViewModel = credentialVM.Clone();
            ActiveCredentialDialogViewModel.Mode = CredentialsDialogMode.Edit;
        }

        private async Task DeleteCredentialAsync(CredentialViewModel credVM)
        {
            var result = await MaterialMessageBox.ShowAsync(
                "Delete credential?",
                $"Name: {credVM.NameFieldVM.Value}",
                MaterialMessageBoxButtons.YesNo,
                MvvmHelper.MainWindowDialogName,
                PackIconKind.Delete);
            if (result == MaterialDialogResult.Yes)
            {
                await _settingsService.DeleteCredential(credVM.Model);
                _credentials.Remove(credVM);
                DisplayedCredentials.Remove(credVM);
            }
        }

        private void CopyToClipboard(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            try
            {
                System.Windows.Clipboard.SetText(data);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private RelayCommand _addCredentialCommand;
        public RelayCommand AddCredentialCommand => _addCredentialCommand ??= new RelayCommand(AddCredential);

        private RelayCommand<CredentialViewModel> _editCredentialCommand;
        public RelayCommand<CredentialViewModel> EditCredentialCommand => _editCredentialCommand ??= new RelayCommand<CredentialViewModel>(EditCredential);

        private AsyncRelayCommand<CredentialViewModel> _deleteCredentialCommand;
        public AsyncRelayCommand<CredentialViewModel> DeleteCredentialCommand => _deleteCredentialCommand ??= new AsyncRelayCommand<CredentialViewModel>(DeleteCredentialAsync);

        private RelayCommand<string> _copyToClipboardCommand;
        public RelayCommand<string> CopyToClipboardCommand => _copyToClipboardCommand ??= new RelayCommand<string>(CopyToClipboard);
    }
}
