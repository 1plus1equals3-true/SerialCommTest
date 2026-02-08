using System;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace SerialCommTest
{
    public partial class Form1 : Form
    {
        const char STX = '\x02';
        const char ETX = '\x03';
        private StringBuilder rxBuffer = new StringBuilder();
        private string lastSentCommand = "None";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LogManager.Initialize();
            Log("프로그램 시작 - 로그 파일 생성");

            string[] ports = SerialPort.GetPortNames();

            comboCOM.Items.AddRange(ports);

            if (comboCOM.Items.Count > 0)
            {
                comboCOM.SelectedIndex = 0;
            }

            serialPort1.DataReceived += new SerialDataReceivedEventHandler(serialPort1_DataReceived);
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.Close();
                    Log("연결이 해제되었습니다.");
                    btnConnect.Text = "연결";
                    comboCOM.Enabled = true;
                }
                else
                {
                    if(comboCOM.Text == "")
                    {
                        Log("포트를 선택해주세요.");
                        return;
                    }
                    serialPort1.PortName = comboCOM.Text;
                    serialPort1.BaudRate = 9600;
                    serialPort1.Encoding = Encoding.UTF8;

                    serialPort1.Open();

                    Log($"{comboCOM.Text}포트에 연결되었습니다");
                    btnConnect.Text = "해제";
                    comboCOM.Enabled = false;
                }
            }
            catch (Exception ex) { Log($"에러 발생: {ex.Message}"); }
        }

        private void Log(string msg)
        {
            if (richLog.IsDisposed) return;
            string currentTime = DateTime.Now.ToString("HH:mm:ss");
            richLog.AppendText($"[{currentTime}] {msg}\r\n");
            richLog.ScrollToCaret();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen) 
            { Log("포트가 연결되지 않았습니다."); return; }

            try
            {
                string data = txtSend.Text;
                lastSentCommand = data;
                string packet = $"{STX}{data}{ETX}";
                serialPort1.Write(packet);
                //serialPort1.WriteLine(data);
                Log($"[TX] 보냄 : {data} (STX/ETX)");
                LogManager.WriteBytes("TX", data);
                txtSend.Clear();
            }
            catch (Exception ex)
            {
                Log($"전송 실패 : {ex.Message}");
            }
            
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen) return;
                string recvData = serialPort1.ReadExisting();
                rxBuffer.Append(recvData);

                LogManager.WriteBytes("RX_RAW", recvData.Replace("\n", "\\n").Replace("\r", "\\r"));

                while (true)
                {
                    string currentBuffer = rxBuffer.ToString();
                    int stxIndex = currentBuffer.IndexOf(STX);
                    int etxIndex = currentBuffer.IndexOf(ETX);

                    if (stxIndex != -1 && etxIndex != -1 && stxIndex < etxIndex)
                    {
                        string msg = currentBuffer.Substring(stxIndex + 1, etxIndex - stxIndex - 1);

                        this.Invoke(new Action(() =>
                        {
                            Log($"[RX] 파싱 : {msg}");
                            LogManager.WriteResult(lastSentCommand, msg, "성공");
                        }));

                        rxBuffer.Remove(0, etxIndex + 1);
                    }
                    else if (etxIndex != -1 && (stxIndex == -1 || etxIndex < stxIndex))
                    {
                        rxBuffer.Remove(0, etxIndex + 1);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!this.IsDisposed)
                {
                    this.Invoke(new Action(() => Log($"수신 실패 : {ex.Message}")));
                }
            }
        }
    }
}
