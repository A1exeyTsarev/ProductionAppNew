using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ProductionApp
{
    public partial class FieldWindow : Window
    {
        private readonly string _initialValue;
        private readonly bool _requireText;
        private bool _invalidDate;

        public string Value => ValueInput.Text == _initialValue ? _initialValue : ValueInput.Text.Trim();
        public DateTime? Date => DateInput.SelectedDate;
        public string ActPath { get; private set; }

        public FieldWindow(string title, int maxLength, string value = "", bool multiline = false,
            bool requireText = false, DateTime? date = null, string? actPath = null)
        {
            InitializeComponent();
            Title = title;
            HeadingText.Text = title;
            _initialValue = value;
            _requireText = requireText;
            ActPath = actPath ?? "";
            ValueInput.MaxLength = maxLength;
            ValueInput.Text = value;
            EditorHint.Text = $"До {maxLength} символов.";

            if (date.HasValue)
            {
                EditorHint.Text = "Выберите дату или введите её в формате ДД.ММ.ГГГГ.";
                ValueInput.Visibility = Visibility.Collapsed;
                DateInput.Visibility = Visibility.Visible;
                DateInput.SelectedDate = date == DateTime.MinValue ? null : date;
            }
            else if (multiline)
            {
                Width = 570;
                ValueInput.Height = 124;
                ValueInput.AcceptsReturn = true;
                ValueInput.TextWrapping = TextWrapping.Wrap;
                ValueInput.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }

            if (requireText)
            {
                SaveButton.Content = "Добавить";
            }

            if (actPath != null)
            {
                EditorHint.Text = $"Введите до {maxLength} символов или приложите скан акта.";
                ActControls.Visibility = Visibility.Visible;
                ShowActName();
            }
        }

        private void FieldWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DateInput.Visibility == Visibility.Visible)
            {
                DateInput.Focus();
            }
            else
            {
                ValueInput.Focus();
                ValueInput.SelectAll();
            }
        }

        private void ChooseAct_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выбор файла акта",
                Filter = "Скан акта (PDF или изображение)|*.pdf;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                if (!IsActFile(dialog.FileName))
                {
                    Status.Text = "Выберите файл PDF или изображение.";
                    return;
                }

                ActPath = dialog.FileName;
                ShowActName();
            }
        }

        private void OpenAct_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(ActPath) || !IsActFile(ActPath))
            {
                Status.Text = "Файл акта недоступен. Выберите его снова.";
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(ActPath) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                Status.Text = "Windows не смогла открыть файл акта.";
            }
        }

        private void RemoveAct_Click(object sender, RoutedEventArgs e)
        {
            ActPath = "";
            ShowActName();
        }

        private void ShowActName()
        {
            bool attached = ActPath.Length > 0;
            ActName.Text = attached ? Path.GetFileName(ActPath) : "";
            ActName.ToolTip = ActName.Text;
            ActName.Visibility = attached ? Visibility.Visible : Visibility.Collapsed;
            OpenActButton.Visibility = ActName.Visibility;
            RemoveActButton.Visibility = ActName.Visibility;
            ChooseActButton.Content = attached ? "Выбрать другой файл" : "Прикрепить акт";
            Status.Text = "";
        }

        private static bool IsActFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".pdf" or ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".bmp";
        }

        private void DateInput_Invalid(object sender, DatePickerDateValidationErrorEventArgs e)
        {
            _invalidDate = true;
            Status.Text = "Введите дату в формате ДД.ММ.ГГГГ.";
        }

        private void DateInput_Changed(object sender, SelectionChangedEventArgs e)
        {
            _invalidDate = false;
            Status.Text = "";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ValueInput.Text != _initialValue && Value.Length > ValueInput.MaxLength)
            {
                Status.Text = $"Сократите текст до {ValueInput.MaxLength} символов.";
                ValueInput.Focus();
                return;
            }

            if (_requireText && string.IsNullOrWhiteSpace(Value))
            {
                Status.Text = "Введите название.";
                ValueInput.Focus();
                return;
            }

            if (DateInput.Visibility == Visibility.Visible && (_invalidDate || Date == null))
            {
                Status.Text = "Укажите дату поступления в формате ДД.ММ.ГГГГ.";
                return;
            }

            DialogResult = true;
        }
    }
}
