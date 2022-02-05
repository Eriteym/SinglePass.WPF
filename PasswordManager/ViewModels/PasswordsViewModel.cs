﻿using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
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

        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly ILogger<PasswordsViewModel> _logger;
        private readonly List<CredentialViewModel> _credentials = new();

        public event Action<CredentialViewModel> CredentialSelected;

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
                ActiveCredentialDialogViewModel.IsPasswordVisible = false;
                CredentialSelected?.Invoke(value);
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                _ = FilterCredentialsAsync();
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
            CredentialsCryptoService credentialsCryptoService,
            ILogger<PasswordsViewModel> logger,
            CredentialsDialogViewModel credentialsDialogViewModel)
        {
            _credentialsCryptoService = credentialsCryptoService;
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
                await _credentialsCryptoService.DeleteCredential(credVM.Model);
                _credentials.Remove(credVM);
                var dIndex = DisplayedCredentials.IndexOf(credVM);
                var countAfterDeletion = DisplayedCredentials.Count - 1;
                var sIndex = dIndex >= countAfterDeletion ? countAfterDeletion - 1 : dIndex;
                await FilterCredentialsAsync();
                if (sIndex >= 0)
                {
                    SelectedCredential = DisplayedCredentials.ElementAt(sIndex);
                }
            }
        }

        private void ActiveCredentialDialogViewModel_Cancel()
        {
            ActiveCredentialDialogViewModel.IsPasswordVisible = false;
            ActiveCredentialDialogViewModel.Mode = CredentialsDialogMode.View;
            ActiveCredentialDialogViewModel.CredentialViewModel = SelectedCredential;
        }

        private async void ActiveCredentialDialogViewModel_Accept(CredentialViewModel newCredVM, CredentialsDialogMode mode)
        {
            newCredVM.LastModifiedTime = DateTime.Now;
            if (mode == CredentialsDialogMode.New)
            {
                await _credentialsCryptoService.AddCredential(newCredVM.Model);
                _credentials.Add(newCredVM);
                await FilterCredentialsAsync();
            }
            else if (mode == CredentialsDialogMode.Edit)
            {
                await _credentialsCryptoService.EditCredential(newCredVM.Model);
                var staleCredVM = _credentials.FirstOrDefault(c => c.Model.Equals(newCredVM.Model));
                var staleIndex = _credentials.IndexOf(staleCredVM);
                _credentials.Remove(staleCredVM);
                _credentials.Insert(staleIndex, newCredVM);
                await FilterCredentialsAsync();
            }

            SelectedCredential = newCredVM;
        }

        public void LoadCredentials()
        {
            try
            {
                Loading = true;
                var credentials = _credentialsCryptoService.Credentials;
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
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task FilterCredentialsAsync()
        {
            try
            {
                Loading = true;
                List<CredentialViewModel> filteredCredentials = null;
                var filterText = SearchText;

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
                _logger.LogError(ex, string.Empty);
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
            ActiveCredentialDialogViewModel.IsPasswordVisible = true;
        }

        private RelayCommand _addCredentialCommand;
        public RelayCommand AddCredentialCommand => _addCredentialCommand ??= new RelayCommand(AddCredential);
    }
}
