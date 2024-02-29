using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LibVLCSharp.WPF;
using System.Reflection.Emit;
using System.Runtime;
using System.Windows.Forms;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using Ookii.Dialogs;

namespace CBDownloader;

public partial class MainWindow : Window
{
    
    private readonly LibVLC libVLC = new();
    private readonly List<string> checkList = new();
    private readonly DispatcherTimer checkTimer = new() { Interval = TimeSpan.FromSeconds(1200) };
    private string filePath = "";
    private readonly BackgroundWorker initialBG = new();
    private readonly List<string> startList = new();
    LibVLCSharp.Shared.MediaPlayer mediaPlayer;
    LibVLC _libVLC; 
    LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
    private string vid_url = "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";
    private Task recordTask;
    private CancellationTokenSource stoprecordCTS=new CancellationTokenSource();
    private List<string> liverecordingList = new List<string>();
    public MainWindow()
    {
        InitializeComponent();
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
        CBLive.MediaPlayer = _mediaPlayer;
        checkTimer.Tick += checkTimerTick;
        initialBG.DoWork += InitialBG_DoWork;
        initialBG.RunWorkerCompleted += InitialBG_RunWorkerCompleted;
        initialBG.RunWorkerAsync();
        initialList.ItemsSource = startList;
        CBLive.Loaded += VideoView_Loaded;
        locationTxt.Text = filePath;
    }

    private async void checkTimerTick(object sender, EventArgs e)
    {
        await refresh();
    }

    async Task refresh()
    {
        
        var temp = new List<string>();
        if (startList.Count > 0)
            for (var i = 0; i < startList.Count; i++)
                if (check_creator_online(startList[i]))
                    temp.Add(startList[i]);
        //Thread.Sleep(rnd.Next(3000,6000));
        checkedList.ItemsSource = temp;
        checkedList.Items.Refresh();
        checkTimer.Stop();
        checkTimer.Start();
    }

    private void InitialBG_DoWork(object sender, DoWorkEventArgs e)
    {
        initialTasks();
        checkTimer.Start();
    }

    private void InitialBG_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        checkedList.ItemsSource = checkList;
    }


    private void initialTasks()
    {
        try
        {
            
            //Location file update
            if (!File.Exists("location.txt"))
            {
                File.Create("location.txt");
                File.WriteAllText("location.txt", Directory.GetCurrentDirectory(), Encoding.ASCII);
            }
            else
            {
                filePath = File.ReadAllText("location.txt");
                
            }

            if (!File.Exists("performers.txt"))
            {
                File.Create("performers.txt");
            }
            else
            {
                var reader = new StreamReader("performers.txt");
                string line;
                while ((line = reader.ReadLine()) != null) startList.Add(line);
                reader.Close();
            }

            
            //loadProgress.Minimum = 0;
            //loadProgress.Maximum = initialList.Items.Count;
            if (startList.Count > 0)
                for (var i = 0; i < startList.Count; i++)
                    
                    if (check_creator_online(startList[i]))
                        checkList.Add(startList[i]);
            //Thread.Sleep(rnd.Next(3000,6000));
            
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }


    //CB Downloader Functions
    private bool check_creator_online(string creator)
    {
        try
        {
            using (var client = new WebClient())
            {
                client.Headers["User-Agent"] =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36 OPR/107.0.0.0";
                client.Headers["Content-Type"] = "application/json";
                var json = client.DownloadString($"https://chaturbate.com/api/chatvideocontext/{creator}");
                var jsonData = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);
                if (jsonData["room_status"] != "offline" && jsonData["room_status"] != "private") return true;
                return false;
            }
        }
        catch (Exception ex)
        {
            //MessageBox.Show(ex.Message);
            return false;
        }
    }

    public static string GetUrlM3U8(string creator)
    {
        var jsonData = new WebClient().DownloadString($"https://chaturbate.eu/api/chatvideocontext/{creator}");
        var jsonDataObject = JsonConvert.DeserializeObject<JObject>(jsonData);
        var hlsSource = jsonDataObject["hls_source"].ToString();
        var delimiter = '?';
        return hlsSource.Split(delimiter)[0];
    }

    private void DownloadM3U8(string url, string creator)
    {
        try
        {
            var timeNow = DateTime.Now.ToString("dd-MMM-HH-mm-ss-");
            var outFile = $"{timeNow}{creator}-chaturbate.ts";
            if (filePath != "")
            {
                var fullPath = filePath +"\\"+ outFile;
                mediaPlayer = new MediaPlayer(libVLC);
                var media = new Media(libVLC, new Uri(url));
                media.AddOption(":sout=#transcode{vcodec=h264,acodec=aac,ab=128,mux=hls,seglen=6}");
                media.AddOption(":sout=#file{dst=" + fullPath + "}");
                media.AddOption(":sout-keep");
                mediaPlayer.Play(media);
            }
            else
            {
                MessageBox.Show("Please select a folder to save the file", "Error");
            }
        }
        catch (Exception ex)
        {
            LogRT.AppendText(ex.Message+"\n");

        }
    }


    private void record(string creator)
    {
        
        try
        {
           
            DownloadM3U8(GetUrlM3U8(creator), creator);

            
        }
        catch (Exception ex)
        {
            LogRT.AppendText(ex.Message+"\n");
            
        }
        
    }
    private void closeRecord()
    {
        try
        {
            mediaPlayer.Stop();
            liverecordingList.Clear();
        }
        catch (Exception ex)
        {
        }
    }


    private void showLive(object sender, SelectionChangedEventArgs args)
    {
        try
        {

            if (!liverecordingList.Contains(checkedList.SelectedItem.ToString()))
            {
                record_selected_BTN.IsEnabled = true;
                var url_CB = GetUrlM3U8(checkedList.SelectedItem.ToString());
                if (url_CB != null)
                {
                    vid_url = url_CB;
                    _mediaPlayer.Play(new Media(_libVLC, new Uri(vid_url)));

                }
            }
            else
            {
                record_selected_BTN.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            if (ex.Message != "Object reference not set to an instance of an object.")
            {
                LogRT.AppendText(ex.Message + ":: Try Again ::\n");
            }
            
            
        }
    }

    private async void addCreatorClick(object sender, RoutedEventArgs e)
    {
        await addCreatorFn();
    }

    private async Task addCreatorFn()
    {
        try
        {
            if (addCreator.Text.Length > 0)
            {
                var url = addCreator.Text;
                url = url.Replace("https://chaturbate.com/", "");
                url = url.Replace("/", "");
                startList.Add(url);
                addCreator.Text = "";
                if (check_creator_online(url)) checkList.Add(url);
                initialList.ItemsSource = startList;
                initialList.Items.Refresh();
                checkedList.ItemsSource = checkList;
                checkedList.Items.Refresh();
            }
        }
        catch (Exception ex)
        {
            LogRT.AppendText(ex.Message + "\n");
        }
    }

    private void cbClosing(object sender, CancelEventArgs e)
    {
        var writer = new StreamWriter("performers.txt");
        for (var i = 0; i < startList.Count; i++) writer.WriteLine(startList[i]);
        writer.Close();
    }
    void VideoView_Loaded(object sender, RoutedEventArgs e)
    {
        _mediaPlayer.Play(new Media(_libVLC, new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4")));
    }

    void selectLocationBTN(object sender, RoutedEventArgs e)
    {
        
        var folderBrowserDialog1 = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
        if ((bool)folderBrowserDialog1.ShowDialog(this))
        {
            filePath = folderBrowserDialog1.SelectedPath;
            MessageBox.Show(filePath);
            File.WriteAllText("location.txt", filePath);
            locationTxt.Text = filePath;
        }
    }

    private void stopLiveBTN_Click(object sender, RoutedEventArgs e)
    {
        _mediaPlayer.Stop();
        
    }

       
    private void record_sel_click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selected_creator = checkedList.SelectedItem.ToString();
            liverecordingList.Add(selected_creator);
            recordTask = Task.Run(() =>
            {
                record(selected_creator);
            });
            recordTask.Wait();
            LogRT.AppendText(selected_creator + " is getting recorded!" + "\n");
        }
        catch (Exception exception)
        {
            LogRT.AppendText(exception.Message + "\n");
        }

    }
    private void stop_all_click(object sender, RoutedEventArgs e)
    {
       
        closeRecord();
    }


    private void InitialList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var creatorName = initialList.SelectedItem.ToString();
            startList.Remove(creatorName);
            initialList.ItemsSource = startList;
            initialList.Items.Refresh();
        }
        catch{}
    }
}