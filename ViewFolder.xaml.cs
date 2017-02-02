using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ProgettoPDS
{
    /// <summary>
    /// Logica di interazione per ViewFolder.xaml
    /// </summary>
    /// 
    public partial class ViewFolder : Window
    {
        
        Client client;
        string path_to_synch;
        bool path_too_long = false;
        bool view_error = false;
        List<string> items = new List<string>();

        private static readonly Object monitor = new Object(); //used here in place of Sleep in order to interrupt it when doing logout
        private static readonly Object pbar_monitor = new Object(); //used to access the pbar from the main thread and the background

        bool must_logout = false;
        public ViewFolder(Client client,string path)
        {
            /*
             * constructor of viewfolder. in charge of calling the synchronize for the first time
             */
            try
            {
                InitializeComponent();
                path_to_synch = path;
                this.client = client;
                client.connect_to_server();
                //to do in asynch way
                //this.items = client.view_folders();
                first_synch();
                
            }
            catch (Exception ex)
            {
                message.Content = "Errore di connessione al server";
            }
            
        }

        public void first_synch()
        {
            //asynchronous logic
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += firstsynch_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += firstsynch_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        void firstsynch_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                int result = 0; // used for the worker result
                (sender as BackgroundWorker).ReportProgress(0); //start pbar

                items = client.view_folders(true);

                e.Result = result;
            }
            catch
            {
                e.Result = -1;
            }
        }

        void firstsynch_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if ((int)e.Result != -1)
            {
                create_tree(this.items, this); //it makes sense because i call the method also somewhere else (in client class for ex)
                //here I will call the periodic method
                var dueTime = TimeSpan.FromMinutes(1);
                var interval = TimeSpan.FromMinutes(1);
                lock (pbar_monitor) // I think i should put it as a best practice
                {
                    pbar.IsIndeterminate = false;
                    pbar.Visibility = Visibility.Hidden;
                }
                
                periodicSynchronization(dueTime, interval, CancellationToken.None);
            }
            else
            {
                message.Content="errore di connessione col server."+Environment.NewLine+"per favore chiudere e riaprire l'app";
            }
        }
       
        public void create_tree(List<string>items,ViewFolder v){
            /*
             * create tree of folders to be visualized for the user
             */
            string []format = {"yyyyMMdd-HHmm"};
            DateTime date;
            MenuItem other_root = null;
            List<string> paths=new List<string>();
            Dictionary<string, MenuItem> d = new Dictionary<string, MenuItem>();
            Console.WriteLine(items[0]);
            MenuItem data = new MenuItem() { Title = items[0] };
            MenuItem other_data = null;
            v.folders.Items.Add(data);
            MenuItem root = new MenuItem() { Title = System.IO.Path.GetDirectoryName(items[1]), Icon = this.getIcon(System.IO.Path.GetDirectoryName(items[1]), true, true) ,isDirectory=true};
            root.Items.Add(new MenuItem() { Title = items[1], Icon = this.getIcon(items[1], true, false),isDirectory=false });
            d.Add(System.IO.Path.GetDirectoryName(items[1]), root);
            string previous_path = System.IO.Path.GetDirectoryName(items[1]);
            paths.Add(System.IO.Path.GetDirectoryName(items[1]));
            //starting from the 3rd element
            foreach (string filename in items.GetRange(2,items.Count-2))
            {
                if (DateTime.TryParseExact(filename, format, new CultureInfo("en-US"),
                              DateTimeStyles.None, out date))
                {
                    Console.WriteLine(filename);
                    other_data = new MenuItem() { Title = filename };                   
                    continue;
                }
                //take the path
                string path=System.IO.Path.GetDirectoryName(filename);
                //check if it contains the previous path
                if (path.Contains(previous_path) && path!=previous_path)
                {
                    //add as subfolder or subfile
                    //the folder
                    MenuItem childitem1 = new MenuItem() { Title = path ,   Icon = this.getIcon(path,true,true),isDirectory=true};
                    //the file
                    childitem1.Items.Add(new MenuItem() { Title = filename, Icon = this.getIcon(filename, true, false),isDirectory=false });
                    d[previous_path].Items.Add(childitem1);
                    paths.Add(path);
                    previous_path = path;
                    d.Add(path, childitem1);
                    //childitemPrevious.Items.Add(childitem1);
                }
                else if (path == previous_path)
                {
                    //same subfolder
                    d[path].Items.Add(new MenuItem() { Title = filename,   Icon = this.getIcon(filename,true,false),isDirectory=false });
                }
                //do another folder
                else
                {
                    bool found = false;
                    //i have to save all previous folders and check where I have to put this new one.
                    paths.Sort(CompareByLength);
                    foreach (string s in paths)
                    {                    
                        if(path.Contains(s)){
                            //the folder
                            MenuItem childItem=new MenuItem() { Title = path ,   Icon = this.getIcon(path,true,true),isDirectory=true};
                            //the file
                            childItem.Items.Add(new MenuItem() { Title = filename, Icon = this.getIcon(filename, true, false),isDirectory=false });
                            d[s].Items.Add(childItem);
                            previous_path=path;
                            d.Add(path,childItem);
                            found = true;
                            paths.Add(path);
                            break;
                        }
                    }
                    //for the 2nd and 3rd (and also more if any) versions
                    if (!found)
                    {
                        if (root != null)
                        {
                            v.folders.Items.Add(root);
                            root = null;
                            if(other_data!=null)
                                v.folders.Items.Add(other_data);
                            other_data = null;
                        }
                        //first time will not do it, other times yes
                        
                        if (other_root != null)
                        {
                            
                            v.folders.Items.Add(other_root);
                        }
                        other_root = new MenuItem() { Title = path, Icon = this.getIcon(path, true, true),isDirectory=true };
                        other_root.Items.Add(new MenuItem() { Title = filename, Icon = this.getIcon(filename, true, false),isDirectory=false });
                        d.Add(path, other_root);
                        previous_path = path;
                        paths.Add(path);
                    }
                    found = false;
                }
            }
            if (root != null)
                v.folders.Items.Add(root);
            //for the last time
            if(other_data!=null)
                v.folders.Items.Add(other_data); 
            v.folders.Items.Add(other_root);
            
        }

        private void choose_folder_Click(object sender, RoutedEventArgs e)
        {
            /*
              * called when choose folder is clicked
              */
            message.Content = "";
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            // Get the selected file name and display in a TextBox 
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Open document 
                string filename = dialog.SelectedPath;
                path.Text = filename;
            }
        }

        private void download_Click(object sender, RoutedEventArgs e)
        {
            /*
             * called when download_button is clicked
             */
            message.Content = "";
            string[] format = { "yyyyMMdd-HHmm" };
            DateTime date;
            String f;
            if (this.path.Text!= "")
                try
                {
                    message.Content = "";
                    try
                    {
                       /*if ((folders.SelectedItem as MenuItem).isDirectory == true)
                        {
                            message.Content = "Puoi scaricare solo files, non cartelle";
                            return;
                        }*/
                        f = (folders.SelectedItem as MenuItem).Title;
                    }
                    catch
                    {
                        message.Content = "Scegli qualcosa da scaricare prima";
                        return;
                    }
                    if (DateTime.TryParseExact(f, format, new CultureInfo("en-US"),
                                    DateTimeStyles.None, out date))
                        return;
                    lock (pbar_monitor)
                    {
                        if (pbar.Visibility == Visibility.Hidden)
                        {
                            client.connect_to_server();
                            client.download_file(f, this.path.Text);
                            message.Content = "File scaricato correttamente";//possibility of file not present : I will print just errore di connessione
                        }
                        else
                        {
                            message.Content = "Sincronizzazione in corso."+Environment.NewLine+"Attendere la fine e riprovare il download";
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    message.Content = "Errore di connessione al server"+Environment.NewLine+"nel download";
                }
            else
            {
                message.Content = "Scegli in quale cartella"+Environment.NewLine+"vuoi scaricare il file";
            }
        }

        public void periodicSynchronization(TimeSpan dueTime,
                                            TimeSpan interval,
                                            CancellationToken token)
        {
            try
            {
                //asynchronous logic
                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.DoWork += worker_DoWork;
                worker.ProgressChanged += worker_ProgressChanged;
                worker.RunWorkerCompleted += worker_RunWorkerCompleted;
                var arg = new arguments() { path = path_to_synch };
                worker.RunWorkerAsync(arg);
                
            }
            catch (Exception ex)
            {
                message.Content = "Errore di connessione al server"+Environment.NewLine+"durante la sincronizzazione";
            }   
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                this.path_too_long = false;
                var interval = TimeSpan.FromMinutes(MyGlobalClient.minutes_for_synch);
                //Thread.Sleep(interval);
                lock (monitor)
                {
                    if (must_logout)
                    {
                        return;
                    }
                    Monitor.Wait(monitor, interval);
                    if (must_logout)
                    {
                        return;
                    }
                    else
                    {
                        int result = 0; // used for the worker result
                        var arg = (arguments)e.Argument; // to access elements ui from this thread
                        (sender as BackgroundWorker).ReportProgress(0); //start pbar
                        client.connect_to_server();
                        client.synchronize(arg.path);
                        e.Result = result;
                    }
                }
                
            }
            catch (PathTooLongException ex)
            {
                e.Result = -1;
                this.path_too_long = true;
            }
            catch(SynchronizeException ex)
            {
                e.Result = -1;
                
            }
            catch (Exception ex)
            {
                e.Result = -1;
                this.view_error = true;
            }
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState == null)
            {
                message.Content = "";
                lock(pbar_monitor)
                {
                    pbar.Visibility = Visibility.Visible;
                    pbar.IsIndeterminate = true;   
                }
                
            }
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            /*
             * when synchronization ends, restart the timeout for next synchronization
             */
            lock (pbar_monitor)
            {
                pbar.Visibility = Visibility.Hidden;
            }
            if (e.Result == null)
            {
                return;
            }
            if ((int)e.Result != -1)
            {
                message.Content = "Sincronizzazione ok.";
            }
            else
            {
                if(path_too_long)
                {
                    message.Content = "Sincronizzazione ok."+Environment.NewLine+"Alcuni file avevano un percorso troppo lungo e"+Environment.NewLine+ "non sono stati trasferiti";
                }
                else if(view_error)
                {
                    message.Content = "Sincronizzazione ok."+Environment.NewLine+ "Errore di visualizzazione";

                }
                else
                {
                    message.Content = "Errore di connessione al server"+Environment.NewLine+"durante la sincronizzazione";
                }
            }
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            var arg = new arguments() { path = path_to_synch };
            worker.RunWorkerAsync(arg);
            
        }

        private void set_interval(object sender, RoutedEventArgs e)
        {
            message.Content = "";
            if (this.interval.Value is int && this.interval.Value>0 && this.interval.Value<301)
            {
                MyGlobalClient.minutes_for_synch = (int)this.interval.Value;
                this.message.Content = "Intervallo settato";
            }
            else
            {
                this.message.Content = "Inserisci un val tra 1 e 300";
            }
        }

        private void logout(object sender, RoutedEventArgs e)
        {
            lock (pbar_monitor) //used to avoid that I read that is hidden and immediately after it becomes visible from the backgroundworker
            {
                if (pbar.Visibility == Visibility.Hidden)
                {
                    lock (monitor)
                    {
                        //this is used to stop the thread that was sleeping avoiding him to spawn a backgroundworker
                        must_logout = true;
                        Monitor.Pulse(monitor);
                    }
                    MainWindow window = new MainWindow();
                    window.Show();
                    this.Close();
                }
                else
                {
                    message.Content = "C'è un'operazione in corso."+Environment.NewLine+"Attendere la fine e riprovare il logout";
                }
            }
            
        }
        

        //TODO: refactor this code
        /*
         * LOGIC TO SHOW ICONS
         */
        public ImageSource getIcon(string path, bool smallIcon, bool isDirectory)
        {
            // SHGFI_USEFILEATTRIBUTES takes the file name and attributes into account if it doesn't exist
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            if (smallIcon)
                flags |= SHGFI_SMALLICON;

            uint attributes = FILE_ATTRIBUTE_NORMAL;
            if (isDirectory)
                attributes |= FILE_ATTRIBUTE_DIRECTORY;

            SHFILEINFO shfi;
            if (0 != SHGetFileInfo(
                        path,
                        attributes,
                        out shfi,
                        (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                        flags))
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32")]
        private static extern int SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint flags);

        private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
        private const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
        private const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        private const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
        private const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        private const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
        private const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
        private const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;

        private const uint SHGFI_ICON = 0x000000100;     // get icon
        private const uint SHGFI_DISPLAYNAME = 0x000000200;     // get display name
        private const uint SHGFI_TYPENAME = 0x000000400;     // get type name
        private const uint SHGFI_ATTRIBUTES = 0x000000800;     // get attributes
        private const uint SHGFI_ICONLOCATION = 0x000001000;     // get icon location
        private const uint SHGFI_EXETYPE = 0x000002000;     // return exe type
        private const uint SHGFI_SYSICONINDEX = 0x000004000;     // get system icon index
        private const uint SHGFI_LINKOVERLAY = 0x000008000;     // put a link overlay on icon
        private const uint SHGFI_SELECTED = 0x000010000;     // show icon in selected state
        private const uint SHGFI_ATTR_SPECIFIED = 0x000020000;     // get only specified attributes
        private const uint SHGFI_LARGEICON = 0x000000000;     // get large icon
        private const uint SHGFI_SMALLICON = 0x000000001;     // get small icon
        private const uint SHGFI_OPENICON = 0x000000002;     // get open icon
        private const uint SHGFI_SHELLICONSIZE = 0x000000004;     // get shell size icon
        private const uint SHGFI_PIDL = 0x000000008;     // pszPath is a pidl
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;     // use passed dwFileAttribute


        private static int CompareByLength(string x, string y)
        {
            /*
             * used to sort the folders for a correct visualization
             */
            if (x == null)
            {
                if (y == null)
                {
                    // If x is null and y is null, they're equal. 
                    return 0;
                }
                else
                {
                    // If x is null and y is not null, y is greater. 
                    return 1;
                }
            }
            else
            {
                // If x is not null...
                if (y == null)
                // ...and y is null, x is greater.
                {
                    return -1;
                }
                else
                {
                    // ...and y is not null, compare the lengths of the two strings.
                    int retval = x.Length.CompareTo(y.Length);
                    if (retval != 0)
                    {
                        // If the strings are not of equal length, the longer string is greater.
                        if (retval == 1)
                            return -1;
                        else return 1;
                    }
                    else
                    {
                        // If the strings are of equal length, sort them with ordinary string comparison.
                        return x.CompareTo(y);
                    }
                }
            }
        }
    }

    public class MenuItem
    {
        /*
         * object that represents each element visualized in this window for the user
         */
        public MenuItem()
        {
            this.Items = new ObservableCollection<MenuItem>();
        }

        public string Title { get; set; }
        public ImageSource Icon { get; set; }
        public bool isDirectory;
        
        public ObservableCollection<MenuItem> Items { get; set; }
    }
    public class arguments
    {
        public string path;
    }    
}