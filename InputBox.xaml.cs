using System.Windows;


namespace Pixiv_Nginx_GUI
{
    /// <summary>
    /// InputBox.xaml 的交互逻辑
    /// </summary>
    public partial class InputBox : Window
    {
        // 窗口构造函数
        public InputBox()
        {
            InitializeComponent();
        }

        // InputText属性，用于获取或设置输入框中的文本
        public string InputText
        {
            get { return InputTextBox.Text; }
            set { InputTextBox.Text = value; }
        }

        // InitialText属性，用于获取或设置提示文本
        public string InitialText { get; set; }

        // InitialTitle属性，用于获取或设置窗口的初始标题
        public string InitialTitle { get; set; }

        // 窗口加载完成的事件
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 如果 InitialText 不为空或null，则设置 TipText 控件的文本为 InitialText ，并设置窗口标题为 InitialTitle
            if (!string.IsNullOrEmpty(InitialText))
            {
                TipText.Text = InitialText;
                this.Title = InitialTitle;
            }
        }

        // 确定按钮的点击事件
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        // 取消按钮的点击事件
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
