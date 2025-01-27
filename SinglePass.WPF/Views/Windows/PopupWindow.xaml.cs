﻿using SinglePass.WPF.Controls;
using SinglePass.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    public partial class PopupWindow : MaterialWindow
    {
        public bool IsClosed { get; private set; }

        private PopupViewModel ViewModel { get; }

        public IntPtr ForegroundHWND
        {
            get => ViewModel.ForegroundHWND;
            set => ViewModel.ForegroundHWND = value;
        }

        public PopupWindow(PopupViewModel popupViewModel)
        {
            InitializeComponent();

            popupViewModel.Accept += PopupViewModel_Accept;
            popupViewModel.ScrollIntoViewRequired += PopupViewModel_ScrollIntoViewRequired;
            DataContext = popupViewModel;
            ViewModel = popupViewModel;
        }

        private void PopupViewModel_ScrollIntoViewRequired(CredentialViewModel vm)
        {
            CredListBox.ScrollIntoView(vm);
        }

        private void PopupViewModel_Accept()
        {
            Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!IsClosed)
                Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }

        private void MaterialWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => SearchTextBox.Focus());
        }

        private void MaterialWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var isCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            var isEnterDown = e.Key == Key.Enter;

            if (isEnterDown)
            {
                if (isCtrlDown)
                {
                    // This is Ctrl + Enter
                    ViewModel.SetAndCloseCommand.Execute(ViewModel.SelectedCredentialVM.PasswordFieldVM);
                }
                else
                {
                    // This is Enter
                    ViewModel.SetAndCloseCommand.Execute(ViewModel.SelectedCredentialVM.LoginFieldVM);
                }
            }

            if (e.Key == System.Windows.Input.Key.Escape)
                Close();
        }

        private void MaterialWindow_Closed(object sender, EventArgs e)
        {
            ViewModel.Accept -= PopupViewModel_Accept;
            ViewModel.ScrollIntoViewRequired -= PopupViewModel_ScrollIntoViewRequired;
        }
    }
}
