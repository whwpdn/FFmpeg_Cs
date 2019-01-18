using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ffmpegTestApp
{
    public partial class ConcatForm : Form
    {
        Process pFFmpeg = new Process();
        string srcFileList = "filelist.ffc";
        public ConcatForm()
        {
            InitializeComponent();
            this.cbFormat.SelectedIndex = 1;
            this.cbResolution.SelectedIndex = 1;
            
        }

        private string ConvertBackSlash(string ori)
        {
            string outstring ="";
            outstring = ori.Replace('\\', '/');
            return outstring;
        }

        private bool MakeFileList()
        {
            try
            {
                StreamWriter outputFile = new StreamWriter(srcFileList);
                outputFile.WriteLine("ffconcat version 1.0");

                foreach (var itemPath in this.listClips.Items)
                {
                    outputFile.WriteLine("file "+ConvertBackSlash(itemPath.ToString()));
                }
                outputFile.Close();
            }
            catch(IOException e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        private int GetTotalDuration()
        {
            Process pFfprobe = new Process();
            ProcessStartInfo psiProcInfo = new ProcessStartInfo();
            
            psiProcInfo.FileName = Application.StartupPath + "\\x64\\ffprobe.exe";
            psiProcInfo.UseShellExecute = false;
            psiProcInfo.WindowStyle = ProcessWindowStyle.Hidden;
            psiProcInfo.RedirectStandardError = true;
            psiProcInfo.RedirectStandardOutput = true;
            psiProcInfo.CreateNoWindow = true;
           
            double totalDur = 0;
            using (StreamReader sr = new StreamReader(srcFileList))
            {
                string afilePath;
                while ((afilePath = sr.ReadLine()) != null)
                {
                    // substring "file "
                    string strFFMPEGCmd = "-v quiet -of csv=p=0 -show_format_entry duration " + afilePath.Substring(4);

                    psiProcInfo.Arguments = strFFMPEGCmd;
                    pFfprobe.StartInfo = psiProcInfo;
                    pFfprobe.Start();
                    StreamReader srFFmpeg = pFfprobe.StandardOutput;
                    string strFFMPEGOut = srFFmpeg.ReadLine();
                    totalDur += Convert.ToDouble(strFFMPEGOut);
                }
            }
            return (int)((totalDur+1)*1000);
        }

        private void ConcatenateClips(string dstFile, int totalDuration)
        {
            try
            {
                string strFFMPEGOut;
                ProcessStartInfo psiProcInfo = new ProcessStartInfo();
                TimeSpan estimatedTime = TimeSpan.MaxValue;
                StreamReader srFFmpeg;
                string strFFMPEGCmd = "-f concat -safe 0 -i " + srcFileList + " -c:v libx264 -y " + dstFile;
                //psiProcInfo.FileName = Application.StartupPath + ((IntPtr.Size == 8) ? "\\x64" : "\\x86") + "\\ffmpeg.exe";
                psiProcInfo.FileName = Application.StartupPath + "\\x64\\ffmpeg.exe";
                psiProcInfo.Arguments = strFFMPEGCmd;
                psiProcInfo.UseShellExecute = false;
                psiProcInfo.WindowStyle = ProcessWindowStyle.Hidden;
                psiProcInfo.RedirectStandardError = true;
                psiProcInfo.RedirectStandardOutput = true;
                psiProcInfo.CreateNoWindow = true;
                
                pFFmpeg.StartInfo = psiProcInfo;
                pFFmpeg.Start();
                srFFmpeg = pFFmpeg.StandardError;

                do
                {
                    strFFMPEGOut = srFFmpeg.ReadLine();
                    if (strFFMPEGOut != null)
                    {
                        string time = "time=";
                        int startPos = strFFMPEGOut.IndexOf(time);
                        if (startPos != -1)
                        {
                            string text = strFFMPEGOut.Substring(startPos + time.Length);
                            int pos = text.IndexOf(" ");
                            string current = text.Substring(0, pos);

                            TimeSpan currentTime = TimeSpan.Parse(current);

                            int progresss = (int)currentTime.TotalMilliseconds;
                            this.BeginInvoke(new MethodInvoker(() =>
                            {
                                this.toolStripProgressBar1.Value = progresss;

                            }));
                        }
                        this.BeginInvoke(new MethodInvoker(() =>
                        {
                            this.toolStripStatusLabel1.Text = strFFMPEGOut;
                        }));
                        
                        
                    }
                } while (pFFmpeg.HasExited == false);
            }
            catch(Exception e)
            {
                this.BeginInvoke(new MethodInvoker(() =>
                {
                    Console.WriteLine(e);
                    this.toolStripStatusLabel1.Text = "failed : " + e.Message;
                    string[] history = { dstFile, "failed" };
                    this.listHistory.Items.Add(new ListViewItem(history));
                    this.btnStart.Enabled = true;
                    this.btnStop.Enabled = false;
                }));
            }
            finally
            {
                if (pFFmpeg.ExitCode != -1)
                {
                    this.BeginInvoke(new MethodInvoker(() =>
                    {
                        this.toolStripStatusLabel1.Text = "Done";
                        this.btnStart.Enabled = true;
                        this.btnStop.Enabled = false;
                        this.toolStripProgressBar1.Value = totalDuration;
                        string[] history = { dstFile, "Done" };
                        this.listHistory.Items.Add(new ListViewItem(history));
                    }));
                }
            }
        }

        // events
        private void btnAdd_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "mxf| *.mxf|mp4|*.mp4|All(*.*)|*.*";
            dlg.Multiselect = true;
            if(dlg.ShowDialog() == DialogResult.OK)
            {
                foreach (string strFileName in dlg.FileNames)
                {
                    this.listClips.Items.Add(strFileName);
                }
            }
        }

        private void btnOutBrowse_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "mp4|*.mp4|All(*.*)|*.*";

            if(dlg.ShowDialog() == DialogResult.OK)
            {
                this.tbOutputPath.Text = ConvertBackSlash(dlg.FileName);
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if(this.listClips.SelectedIndex < 0)
            {
                return;
            }
            else
            {
                int idx = this.listClips.SelectedIndex;
                this.listClips.Items.Remove(this.listClips.SelectedItem);
                if( this.listClips.Items.Count >0)
                {
                    if (idx == 0)
                    {
                        this.listClips.SelectedIndex = 0;
                    }
                    else
                        this.listClips.SelectedIndex = (idx-1);
                }
                
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            pFFmpeg.Kill();
            string[] history = { this.tbOutputPath.Text, "stopped" };
            this.listHistory.Items.Add(new ListViewItem(history));
            this.btnStart.Enabled = true;
            this.btnStop.Enabled = false;
            this.toolStripProgressBar1.Value = 0;
            this.toolStripStatusLabel1.Text = "Stopped";
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            this.btnStart.Enabled = false;
            this.btnStop.Enabled = true;
            this.toolStripProgressBar1.Value = 0;
            this.toolStripStatusLabel1.Text = "";

            MakeFileList();
            this.toolStripProgressBar1.Maximum = GetTotalDuration();


            this.toolStripStatusLabel1.Text = "concatenate...";

            ThreadPool.QueueUserWorkItem((object state) =>
            {
                string outFile = this.tbOutputPath.Text;
                ConcatenateClips(outFile, this.toolStripProgressBar1.Maximum);
            });
        }

        private void ResetControls(bool enabled)
        {
            this.btnStart.Enabled = enabled;
            this.btnStop.Enabled = !enabled;
            this.toolStripProgressBar1.Value = 0;
            this.toolStripStatusLabel1.Text = "";
        }

        private void btnListUp_Click(object sender, EventArgs e)
        {
            if(this.listClips.SelectedIndex >0)
            {
                SwapListItem(-1);   
            }
        }

        private void btnListDown_Click(object sender, EventArgs e)
        {
            if(this.listClips.SelectedIndex < (this.listClips.Items.Count-1))
            {
                SwapListItem(1);
            }
        }
        private void SwapListItem(int direction)
        {
            int itemIdx = this.listClips.SelectedIndex;
            var item = this.listClips.Items[itemIdx];
            this.listClips.Items.RemoveAt(itemIdx );
            this.listClips.Items.Insert((itemIdx + direction), item);
            this.listClips.SelectedIndex = itemIdx + direction;
        }

        private void listClips_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                string ext =Path.GetExtension(file);
                if(ext.Contains("mxf") || ext.Contains("mp4"))
                {
                    this.listClips.Items.Add(file);
                }
                else
                {
                    this.toolStripStatusLabel1.Text = "Can't add file(type error) - " + file;
                }
                

            }
        }

        private void listClips_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) 
            e.Effect = DragDropEffects.Copy;
        }
    }
}