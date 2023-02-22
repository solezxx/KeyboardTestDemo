using System;
using System.Collections;
using System.IO.Ports;
using System.Text;
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
                Console.WriteLine(1111);
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
        public int count=0;

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
                            //----------------------------------------------------------
                            //链路建立响应消息
                            if (completeData.Contains("3A0D010101"))
                            {
                                if (CheckBox.IsChecked == true)
                                {
                                    WriteToM3("5A 5A 5A 5A 0D 00 00 00 00 00 00 00 3A 0D 01 01 02 11 1C 0A 0D");
                                }
                                else if (CheckBox.IsChecked == false)
                                {
                                    WriteToM3("5A5A5A5A190000000000000001070301000701000901000F01000C0100A4CF0A0D");
                                }
                            }
                            //查询加锁状态
                            if (completeData.Contains("3A0101F5"))
                            {
                                count++;
                                if (count == 3)
                                {
                                    //WriteToM3("5A5A5A5A0C0000000000000014050301A8690A0D");
                                    WriteToM3("5A 5A 5A 5A 0C 00 00 00 00 00 00 00 14 01 01 01 0A D3 0A 0D");
                                    count = 0;
                                }
                            }

                            //解锁消息
                            //if (completeData.Contains("14050301"))
                            //{
                            //    if (completeData.Contains("140503010000"))
                            //    {
                            //        WriteToM3("5A5A5A5A190000000000000001070301000701000901000F01000C0100A4CF0A0D");
                            //    }
                            //    else
                            //    {
                            //        //WriteToM3("5A5A5A5A0C0000000000000014050301A8690A0D");
                            //        WriteToM3("5A 5A 5A 5A 0C 00 00 00 00 00 00 00 14 01 01 01 0A D3 0A 0D");
                            //    }
                            //}

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

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (M3 != null)
            {
                if (SendBox.Text != null)
                {
                    try
                    {
                        WriteToM3(SendBox.Text);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        throw;
                    }
                }
            }
        }

        public bool WriteToM3(string hex)
        {
            try
            {
                hex = hex.Replace(" ", "");
                int NumberChars = hex.Length;
                byte[] bytes = new byte[NumberChars / 2];
                for (int i = 0; i < NumberChars; i += 2)
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                if (M3 != null)
                {
                    M3.Write(bytes, 0, bytes.Length); // 发送数据
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private void DisConnect_Click(object sender, RoutedEventArgs e)
        {
            if (M3!=null)
            {
                M3.Close();
            }
        }
    }
}