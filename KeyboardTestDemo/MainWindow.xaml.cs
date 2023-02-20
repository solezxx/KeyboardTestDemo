using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace KeyboardTestDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ReadComAndConnect();
        }

        public async void ReadComAndConnect()
        {
            await Task.Run((() =>
            {
                string[] oldPortNames = SerialPort.GetPortNames(); // 记录当前可用的串口
                while (true)
                {
                    string[] newPortNames = SerialPort.GetPortNames(); // 获取当前可用的串口
                    foreach (string newPortName in newPortNames)
                    {
                        if (!Array.Exists(oldPortNames, oldPortName => oldPortName == newPortName))
                        {
                            Dispatcher.BeginInvoke(new Action((() =>
                            {
                                M3 = new SerialPort(newPortName, 460800, Parity.None, 8, StopBits.One);
                                while (true)
                                {
                                    try
                                    {
                                        if (!M3.IsOpen)
                                        {
                                            M3.Open();
                                            break;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                    }
                                }
                                // 在这里可以进行串口数据的读取、写入等操作
                                M3.DataReceived += M3_DataReceived;
                                TextBox1.AppendText($"串口{newPortName}打开成功\r\n");
                                byte[] data = { 0x5A, 0x5A, 0x5A, 0x5A, 0x19, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x07, 0x03, 0x01, 0x00, 0x07, 0x01, 0x00, 0x09, 0x01, 0x00, 0x0F, 0x01, 0x00, 0x0C, 0x01, 0x00, 0xA4, 0xCF, 0x0A, 0x0D };
                                M3.Write(data, 0, data.Length); // 发送数据
                                TextBox1.AppendText($"新键盘响应成功\r\n");
                            })));

                            break;
                        }
                    }
                    oldPortNames = newPortNames; // 更新可用的串口列表
                    Thread.Sleep(1000); // 暂停1秒钟
                }
            }));
        }

        private string dataBuffer = "";

        private void M3_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;

                // 读取串口数据，将每个字节转换为16进制字符串
                byte[] buffer = new byte[sp.BytesToRead];
                sp.Read(buffer, 0, buffer.Length);
                string data = BitConverter.ToString(buffer).Replace("-", "");

                // 将每个16进制字符串添加到缓冲区中
                dataBuffer += data;

                // 判断缓冲区的末尾是否为0D
                if (dataBuffer.EndsWith("0D"))
                {
                    // 判断缓冲区的前一个字符是否为0A
                    int index = dataBuffer.LastIndexOf("0A");
                    if (index >= 0)
                    {
                        // 截取完整的16进制数据并保存
                        string completeData = dataBuffer.Substring(0, index + 4);
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            //按键信息
                            Regex regex = new Regex("[^0]"); // 匹配除0以外的任何字符

                            //先判断3A0302，因为win键3A0302 3A0301两个信息都有
                            if (completeData.Contains("3A0302"))
                            {
                                var i = completeData.IndexOf("3A0302");

                                var res = completeData.Substring(i + 6, 4);
                                if (regex.IsMatch(res))
                                    TextBox1.AppendText($"{DateTime.Now}收到数据==>{res}\r\n");
                            }
                            else
                            {
                                if (completeData.Contains("3A0301"))
                                {
                                    var i = completeData.IndexOf("3A0301");
                                    var res = completeData.Substring(i + 10, 2);
                                    if (regex.IsMatch(res))
                                    {
                                        TextBox1.AppendText($"{DateTime.Now}收到数据==>{res}\r\n");
                                    }
                                    else
                                    {
                                        var newres = completeData.Substring(i + 6, 2);
                                        if (regex.IsMatch(newres))
                                        {
                                            TextBox1.AppendText($"{DateTime.Now}收到数据==>{newres}\r\n");
                                        }
                                    }
                                }
                            }

                            //TP信息
                            if (completeData.Contains("3A040303"))
                            {
                                int x = completeData.IndexOf("3A040303");
                                var resx = completeData.Substring(x + 10, 2) + completeData.Substring(x + 8, 2);//高低位互换
                                var resy = completeData.Substring(x + 14, 2) + completeData.Substring(x + 12, 2);//高低位互换

                                LabelX.Content = $"X:{Convert.ToInt32(resx, 16)}";
                                LabelY.Content = $"Y:{Convert.ToInt32(resy, 16)}";
                                if (completeData.Substring(completeData.Length - 10, 2) == "01")
                                {
                                    TextBox1.AppendText($"TP按下，位置{LabelX.Content}{LabelY.Content}\r\n");
                                }
                            }

                            TextBox2.AppendText(completeData.Replace("0A0D", "0A0D\r\n"));
                        }));

                        // 清空缓冲区
                        dataBuffer = "";
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        public SerialPort M3;

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            if (M3 != null && M3.IsOpen)
                M3.Close();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            TextBox1.Text = "";
            TextBox2.Text = "";
        }

        private void SendMsg1_Click(object sender, RoutedEventArgs e)
        {
            byte[] data = { 0x5A, 0x5A, 0x5A, 0x5A, 0x19, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x07, 0x03, 0x01, 0x00, 0x07, 0x01, 0x00, 0x09, 0x01, 0x00, 0x0F, 0x01, 0x00, 0x0C, 0x01, 0x00, 0xA4, 0xCF, 0x0A, 0x0D };
            M3.Write(data, 0, data.Length); // 发送数据
        }

        private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox1.ScrollToEnd();
            TextBox2.ScrollToEnd();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (M3 == null)
            {
                M3 = new SerialPort("COM4", 460800, Parity.None, 8, StopBits.One);
                if (!M3.IsOpen)
                {
                    M3.Open();
                }
                M3.DataReceived += M3_DataReceived;
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    TextBox1.AppendText("COM4连接成功\r\n");
                }));
            }
        }
    }
}