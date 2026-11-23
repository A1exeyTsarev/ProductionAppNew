using Microsoft.EntityFrameworkCore;
using ProductionApp.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ProductionApp
{
    public partial class MainWindow : Window
    {
        private const int ProductNameLimit = 200;
        private const int SerialNumberLimit = 100;
        private const int NotesLimit = 6000;
        private const int ServiceNameLimit = 250;

        private AppDbContext? _editDb;
        private Product _product = new Product();
        private string _originalActPath = "";
        private bool _changesPending;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ConnectDatabaseAsync();
        }

        private async void RetryDatabase_Click(object sender, RoutedEventArgs e)
        {
            await ConnectDatabaseAsync();
        }

        private async Task ConnectDatabaseAsync()
        {
            RetryDatabaseButton.Visibility = Visibility.Collapsed;
            DatabaseDetails.Visibility = Visibility.Collapsed;
            DatabaseDetails.IsExpanded = false;
            HomeStatus.Text = "Подключение к базе данных…";
            try
            {
                using var db = App.GetDbContext();
                await DatabaseStartup.InitializeAsync(db);
                RefreshProducts();
                RegisterButton.IsEnabled = true;
            }
            catch (Exception exception)
            {
                HomeStatus.Text = "Не удалось подключиться к базе. Попробуйте ещё раз.";
                DatabaseErrorText.Text = exception.GetBaseException().Message;
                DatabaseDetails.Visibility = Visibility.Visible;
                RetryDatabaseButton.Visibility = Visibility.Visible;
            }
        }

        private void HomeModules_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool compact = e.NewSize.Width < 665;
            Grid.SetColumnSpan(RegisterButton, compact ? 6 : 2);
            Grid.SetRow(NextStage, compact ? 1 : 0);
            Grid.SetColumn(NextStage, compact ? 0 : 2);
            Grid.SetColumnSpan(NextStage, compact ? 3 : 2);
            Grid.SetRow(FutureSection, compact ? 1 : 0);
            Grid.SetColumn(FutureSection, compact ? 3 : 4);
            Grid.SetColumnSpan(FutureSection, compact ? 3 : 2);
        }

        private void RegistrationFields_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RegistrationFields.Columns = e.NewSize.Width < 665 ? 2 : 3;
        }

        private void RefreshProducts()
        {
            using var db = App.GetDbContext();
            var products = db.Products.AsNoTracking().OrderByDescending(item => item.ProductId).ToList();
            SavedProducts.ItemsSource = products;
            SavedProductsPanel.Visibility = products.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            HomeStatus.Text = products.Count > 0 ? "Сохранённые изделия" : "Сохранённых изделий пока нет.";
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            _editDb = App.GetDbContext();
            OpenRegistration(new Product());
        }

        private void OpenProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not Product selected)
            {
                return;
            }

            try
            {
                _editDb = App.GetDbContext();
                var product = _editDb.Products
                    .Include(item => item.Components.OrderBy(component => component.Id))
                    .Include(item => item.ServiceWorks.OrderBy(work => work.Id))
                    .AsSplitQuery()
                    .FirstOrDefault(item => item.ProductId == selected.ProductId);
                if (product != null)
                {
                    OpenRegistration(product);
                }
                else
                {
                    CloseRegistration();
                    RefreshProducts();
                }
            }
            catch (Exception exception)
            {
                CloseRegistration();
                HomeStatus.Text = "Не удалось открыть изделие. " + exception.GetBaseException().Message;
            }
        }

        private void OpenRegistration(Product product)
        {
            _product = product;
            _originalActPath = product.ActFilePath;
            ComponentsList.ItemsSource = product.Components;
            WorksList.ItemsSource = product.ServiceWorks;
            HomeFeedback.Text = "";
            RegistrationStatus.Text = "";
            RefreshFields();
            RefreshServiceSummary();
            ShowPage(RegistrationPage, "Регистрация изделия");
            RegistrationScroll.ScrollToTop();
            NameFieldButton.Focus();
        }

        private void EditField_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            string title = AutomationProperties.GetName(button);
            string? field = button.Tag as string;
            FieldWindow dialog;

            if (field == "Name")
                dialog = new FieldWindow(title, ProductNameLimit, _product.Name);
            else if (field == "Serial")
                dialog = new FieldWindow(title, SerialNumberLimit, _product.SerialNumber);
            else if (field == "Date")
                dialog = new FieldWindow(title, 0, date: _product.ReceivedDate);
            else if (field == "Malfunction")
                dialog = new FieldWindow(title, NotesLimit, _product.MalfunctionDesc, multiline: true, actPath: _product.ActFilePath);
            else if (field == "Inspection")
                dialog = new FieldWindow(title, NotesLimit, _product.ExternalInspection, multiline: true);
            else
                dialog = new FieldWindow(title, NotesLimit, _product.FaultDetection, multiline: true);

            if (!ShowEditor(dialog))
            {
                return;
            }

            if (field == "Name")
                _product.Name = dialog.Value;
            else if (field == "Serial")
                _product.SerialNumber = dialog.Value;
            else if (field == "Date")
                _product.ReceivedDate = dialog.Date.GetValueOrDefault();
            else if (field == "Malfunction")
            {
                _product.MalfunctionDesc = dialog.Value;
                _product.ActFilePath = dialog.ActPath;
            }
            else if (field == "Inspection")
                _product.ExternalInspection = dialog.Value;
            else
                _product.FaultDetection = dialog.Value;

            _changesPending = true;
            RegistrationStatus.Text = "";
            RefreshFields();
        }

        private bool ShowEditor(FieldWindow dialog)
        {
            dialog.Owner = this;
            dialog.Width = Math.Min(dialog.Width, ActualWidth);
            return dialog.ShowDialog() == true;
        }

        private void RefreshFields()
        {
            NamePreview.Text = Preview(_product.Name, "Обязательное поле");
            SerialPreview.Text = Preview(_product.SerialNumber, "Обязательное поле");
            DatePreview.Text = _product.ReceivedDate == DateTime.MinValue
                ? "Обязательное поле"
                : _product.ReceivedDate.ToString("dd.MM.yyyy");
            MalfunctionPreview.Text = Preview(_product.MalfunctionDesc, "Текст или скан акта");
            if (_product.ActFilePath.Length > 0)
            {
                string attachment = string.IsNullOrWhiteSpace(_product.MalfunctionDesc) ? "Акт" : "Текст и акт";
                MalfunctionPreview.Text = attachment + " · " + Path.GetFileName(_product.ActFilePath);
            }
            InspectionPreview.Text = Preview(_product.ExternalInspection, "Не заполнено");
            DiagnosticsPreview.Text = Preview(_product.FaultDetection, "Не заполнено");
        }

        private static string Preview(string value, string emptyText)
        {
            return string.IsNullOrWhiteSpace(value) ? emptyText : value.ReplaceLineEndings(" ");
        }

        private void RefreshServiceSummary()
        {
            ServiceSummary.Text = _product.Components.Count + _product.ServiceWorks.Count > 0
                ? $"Компоненты ({_product.Components.Count}), работы ({_product.ServiceWorks.Count})"
                : "";
        }


        private void SaveProduct_Click(object sender, RoutedEventArgs e)
        {
            if (_editDb == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_product.Name) || string.IsNullOrWhiteSpace(_product.SerialNumber)
                || _product.ReceivedDate == DateTime.MinValue)
            {
                RegistrationStatus.Text = "Заполните название, серийный номер и дату поступления.";
                if (string.IsNullOrWhiteSpace(_product.Name))
                    NameFieldButton.Focus();
                else if (string.IsNullOrWhiteSpace(_product.SerialNumber))
                    SerialFieldButton.Focus();
                else
                    DateFieldButton.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(_product.MalfunctionDesc) && _product.ActFilePath.Length == 0)
            {
                RegistrationStatus.Text = "Введите описание неисправности или прикрепите акт.";
                MalfunctionFieldButton.Focus();
                return;
            }

            try
            {
                if (_product.ActFilePath.Length > 0 && _product.ActFilePath != _originalActPath)
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ProductionApp", "Acts", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    string copy = Path.Combine(folder, Path.GetFileName(_product.ActFilePath));
                    File.Copy(_product.ActFilePath, copy);
                    _product.ActFilePath = copy;
                    _originalActPath = copy;
                }

                if (_editDb.Entry(_product).State == EntityState.Detached)
                {
                    _editDb.Products.Add(_product);
                }

                _editDb.SaveChanges();
            }
            catch (Exception exception)
            {
                RegistrationStatus.Text = "Не удалось сохранить изделие. " + exception.GetBaseException().Message;
                return;
            }

            CloseRegistration();
            ShowPage(HomePage, "Главная");
            try
            {
                RefreshProducts();
                HomeFeedback.Text = "Изделие сохранено.";
            }
            catch (Exception exception)
            {
                HomeStatus.Text = "Изделие сохранено. Не удалось обновить список. " + exception.GetBaseException().Message;
            }
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            string name = _product.Name.Length > 0 ? _product.Name : "Новое изделие";
            ServiceProduct.Text = string.IsNullOrWhiteSpace(_product.SerialNumber)
                ? name
                : name + " · " + _product.SerialNumber;
            ShowPage(ServicePage, "Сервисные работы");
            AddComponentButton.Focus();
        }

        private void AddComponent_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FieldWindow("Добавить компонент", ServiceNameLimit, requireText: true);
            if (ShowEditor(dialog))
            {
                var component = new ProductComponent { Name = dialog.Value };
                _product.Components.Add(component);
                ComponentsList.SelectedItem = component;
                ComponentsList.ScrollIntoView(component);
                _changesPending = true;
            }
        }

        private void AddWork_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FieldWindow("Добавить работу", ServiceNameLimit, requireText: true);
            if (ShowEditor(dialog))
            {
                var work = new ProductServiceWork { Name = dialog.Value };
                _product.ServiceWorks.Add(work);
                WorksList.SelectedItem = work;
                WorksList.ScrollIntoView(work);
                _changesPending = true;
            }
        }

        private void RemoveComponent_Click(object sender, RoutedEventArgs e)
        {
            if (ComponentsList.SelectedItem is ProductComponent component)
            {
                _product.Components.Remove(component);
                _changesPending = true;
            }
        }

        private void RemoveWork_Click(object sender, RoutedEventArgs e)
        {
            if (WorksList.SelectedItem is ProductServiceWork work)
            {
                _product.ServiceWorks.Remove(work);
                _changesPending = true;
            }
        }

        private void ServiceSelection_Changed(object sender, SelectionChangedEventArgs e)
        {
            RemoveComponentButton.IsEnabled = ComponentsList.SelectedItem != null;
            RemoveWorkButton.IsEnabled = WorksList.SelectedItem != null;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (ServicePage.Visibility == Visibility.Visible)
            {
                RefreshServiceSummary();
                ShowPage(RegistrationPage, "Регистрация изделия");
                ServiceButton.Focus();
            }
            else if (CanLeaveRegistration())
            {
                CloseRegistration();
                ShowPage(HomePage, "Главная");
                RegisterButton.Focus();
            }
        }

        private bool CanLeaveRegistration()
        {
            return !_changesPending || MessageBox.Show(this, "Выйти без сохранения изменений?",
                "Регистрация изделия", MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        private void CloseRegistration()
        {
            _editDb?.Dispose();
            _editDb = null;
            _changesPending = false;
        }

        private void ShowPage(FrameworkElement page, string title)
        {
            HomePage.Visibility = page == HomePage ? Visibility.Visible : Visibility.Collapsed;
            RegistrationPage.Visibility = page == RegistrationPage ? Visibility.Visible : Visibility.Collapsed;
            ServicePage.Visibility = page == ServicePage ? Visibility.Visible : Visibility.Collapsed;
            PageTitle.Text = title;
            ServiceProduct.Visibility = page == ServicePage ? Visibility.Visible : Visibility.Collapsed;
            BackButton.Visibility = page == HomePage ? Visibility.Collapsed : Visibility.Visible;
            BackButton.Content = page == ServicePage ? "К регистрации" : "На главную";
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (CanLeaveRegistration())
            {
                CloseRegistration();
            }
            else
            {
                e.Cancel = true;
            }
        }
    }
}
