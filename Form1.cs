using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialCommTest
{
    public partial class Form1 : Form
    {
        const char STX = '\x02';
        const char ETX = '\x03';
        private StringBuilder rxBuffer = new StringBuilder();

        private bool isAutoRunning = false;
        private bool isResponseReceived = false;
        private string lastSentCommand = "";

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

            dgvList.ColumnCount = 4;
            dgvList.Columns[0].Name = "순번";
            dgvList.Columns[1].Name = "명령어";
            dgvList.Columns[2].Name = "응답값";
            dgvList.Columns[3].Name = "결과";
            dgvList.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
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
                LogManager.WriteBytes("RX_RAW", recvData);

                if (chkRaw.Checked)
                {
                    this.Invoke(new Action(() => Log($"[Raw] {recvData.Trim()}")));
                }

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

                            if (isAutoRunning)
                            {
                                int currentRow = dgvList.CurrentCell.RowIndex;
                                dgvList.Rows[currentRow].Cells[2].Value = msg;
                                isResponseReceived = true;
                            }

                            // 시뮬레이터 모드
                            if (chkDeviceMode.Checked)
                            {
                                string replyMsg = $"ACK:{msg}";
                                string replyPacket = $"{STX}{replyMsg}{ETX}";
                                serialPort1.Write(replyPacket);
                                Log($"[Auto-Reply] {replyMsg}");
                            }

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

        private void btnLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                dgvList.Rows.Clear();

                string[] lines = File.ReadAllLines(ofd.FileName, Encoding.Default);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] cols = lines[i].Split(',');
                    if (cols.Length > 0)
                    {
                        dgvList.Rows.Add(i + 1, cols[0], "", "대기");
                    }
                }
                Log($"파일 로드 완료: {lines.Length}개 데이터");
            }
        }

        private async void btnAutoStart_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen) { Log("먼저 포트를 연결해주세요."); return; }
            if (isAutoRunning) { Log("이미 실행 중입니다."); return; }

            isAutoRunning = true;
            btnAutoStart.Enabled = false;
            btnLoad.Enabled = false;

            Log("=== 자동 테스트 시작 ===");

            for (int i = 0; i < dgvList.Rows.Count; i++)
            {
                if (dgvList.Rows[i].Cells[1].Value == null) continue;
                string cmd = dgvList.Rows[i].Cells[1].Value.ToString();

                dgvList.Rows[i].Cells[3].Value = "진행중...";
                dgvList.Rows[i].DefaultCellStyle.BackColor = Color.Yellow;
                dgvList.CurrentCell = dgvList.Rows[i].Cells[0];

                SendCommand(cmd);
                isResponseReceived = false;

                int timeOut = 20;
                while (timeOut > 0)
                {
                    if (isResponseReceived) break;
                    await Task.Delay(100);
                    timeOut--;
                }

                if (isResponseReceived)
                {
                    dgvList.Rows[i].Cells[3].Value = "성공";
                    dgvList.Rows[i].DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    dgvList.Rows[i].Cells[2].Value = "(응답없음)";
                    dgvList.Rows[i].Cells[3].Value = "실패(Timeout)";
                    dgvList.Rows[i].DefaultCellStyle.BackColor = Color.LightPink;
                    LogManager.WriteResult(cmd, "TIMEOUT", "실패");
                }

                await Task.Delay(200);
            }

            Log("=== 자동 테스트 종료 ===");
            isAutoRunning = false;
            btnAutoStart.Enabled = true;
            btnLoad.Enabled = true;
        }

        private void SendCommand(string data)
        {
            try
            {
                lastSentCommand = data;
                string packet = $"{STX}{data}{ETX}";
                serialPort1.Write(packet);
                Log($"[TX] {data}");
                LogManager.WriteBytes("TX", data);
            }
            catch (Exception ex) { Log($"전송 에러: {ex.Message}"); }
        }
    }
}
