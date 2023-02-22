using System;
using System.Collections;
using System.IO.Ports;
using System.Linq;
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
        public int count = 0;

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
                            //-------------------------------------------------------------------------------------------
                            //链路建立响应消息
                            if (completeData.Contains("3A0D010101"))
                            {
                                WriteToM3("5A 5A 5A 5A 0D 00 00 00 00 00 00 00 3A 0D 01 01 02 11 1C 0A 0D");
                            }
                            //查询SN
                            if (completeData.Contains("3A0101F5"))
                            {
                                count++;
                                SN = SNKey="";
                                if (count == 3)
                                {
                                    WriteToM3("5A 5A 5A 5A 0C 00 00 00 17 00 00 00 14 05 02 04 90 F0 0A 0D ");
                                    count = 0;
                                }
                            }
                            //获取SN,查询加锁状态
                            if (completeData.Contains("1405020400"))
                            {
                                var indexsn = completeData.IndexOf("1405020400");
                                SNKey = completeData.Substring(indexsn + 10, completeData.Length - indexsn - 18);
                                SN = AscToString(SNKey);
                                TextBox1.AppendText($"SN:{SN}");
                                WriteToM3("5A 5A 5A 5A 0C 00 00 00 18 00 00 00 14 05 03 01 C6 4A 0A 0D");
                            }


                            //查询加锁状态成功，判断是否加锁
                            if (completeData.Contains("1405030100"))
                            {
                                var l = completeData.IndexOf("1405030100");
                                if (completeData.Substring(l + 10, 2) == "00")//无锁
                                {
                                    //解锁消息
                                    WriteToM3("5A 5A 5A 5A 19 00 00 00 22 00 00 00 01 07 03 01 00 07 01 00 09 01 00 0F 01 00 0C 01 00 D2 79 0A 0D");
                                }
                                else
                                {
                                    String input = "1C0000002300000014050303"+SNKey;
                                    var a = StringToHexBytes(input);
                                    CRCXMODEM(a,a.Length);
                                    string s = "5A 5A 5A 5A 1C 00 00 00 23 00 00 00 14 05 03 03" + SNKey  + "AF670A0D";
                                    WriteToM3(s);
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
        private string SN, SNKey;
        private StringBuilder sb;

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

        public byte[] StringToHexBytes(string str)
        {
            str = str.Replace(" ", "");
            int NumberChars = str.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(str.Substring(i, 2), 16);
            return bytes;
        }

        public bool WriteToM3(string hex)
        {
            try
            {
                var bytes = StringToHexBytes(hex);
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

        public string AscToString(string asc)
        {
            try
            {
                sb = new StringBuilder();
                for (int i = 0; i < asc.Length; i += 2)
                {
                    string hexChar = asc.Substring(i, 2);
                    int charValue = Convert.ToInt32(hexChar, 16);
                    sb.Append(Convert.ToChar(charValue));
                }
            }
            catch (Exception e)
            {
             TextBox1.AppendText(e.Message+"\r\n");
            }
            return sb.ToString();
        }
        
        private void DisConnect_Click(object sender, RoutedEventArgs e)
        {
            if (M3 != null)
            {
                M3.Close();
            }
        }

        public static UInt16 CRCXMODEM(byte[] data, int size)
        {
            UInt32 i = 0;
            UInt16 crc = 0;
            for (i = 0; i < size; i++)
            {
                crc = UpdateCRC16(crc, data[i]);
            }
            crc = UpdateCRC16(crc, 0);
            crc = UpdateCRC16(crc, 0);
            return (UInt16)(crc);
        }

        /// <summary>
        /// 更新RCR16校验
        /// </summary>
        /// <param name="crcIn"></param>
        /// <param name="bytee"></param>
        /// <returns></returns>
        private static UInt16 UpdateCRC16(UInt16 crcIn, byte bytee)
        {
            UInt32 crc = crcIn;
            UInt32 ins = (UInt32)bytee | 0x100;
            do
            {
                crc <<= 1;
                ins <<= 1;
                if ((ins & 0x100) == 0x100)
                {
                    ++crc;
                }
                if ((crc & 0x10000) == 0x10000)
                {
                    crc ^= 0x1021;
                }
            }
            while (!((ins & 0x10000) == 0x10000));
            return (UInt16)crc;
        }
    }
}