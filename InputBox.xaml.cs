using System.Windows;


namespace Pixiv_Nginx_GUI
{
    /// <summary>
    /// InputBox.xaml 的交互逻辑
    /// </summary>
    public partial class InputBox : Window
    {

        public InputBox()
        {
            InitializeComponent();
        }

        public string InputText
        {
            get { return InputTextBox.Text; }
            set { InputTextBox.Text = value; }
        }

        public string InitialText { get; set; }
        public string InitialTitle { get; set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(InitialText))
            {
                TipText.Text = InitialText;
                this.Title = InitialTitle;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
