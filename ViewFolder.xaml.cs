﻿using System;
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
namespace ProgettoPDS
{
    /// <summary>
    /// Logica di interazione per ViewFolder.xaml
    /// </summary>
    public partial class ViewFolder : Window
    {
        Client client;
        string path_to_synch;
        bool path_too_long = false;
        List<string> items = new List<string>();
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
                items = client.view_folders();
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
                pbar.IsIndeterminate = false;
                pbar.Visibility = Visibility.Hidden;
                periodicSynchronization(dueTime, interval, CancellationToken.None);
            }
            else
            {
                message.Content="errore di connessione col server. per favore chiudere e riaprire l'app";
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
            MenuItem root = new MenuItem() { Title = System.IO.Path.GetDirectoryName(items[1]) };
            root.Items.Add(new MenuItem() { Title = items[1]});
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
                    MenuItem childitem1 = new MenuItem() { Title = path };
                    //the file
                    childitem1.Items.Add(new MenuItem() { Title = filename });
                    d[previous_path].Items.Add(childitem1);
                    paths.Add(path);
                    previous_path = path;
                    d.Add(path, childitem1);
                    //childitemPrevious.Items.Add(childitem1);
                }
                else if (path == previous_path)
                {
                    //same subfolder
                    d[path].Items.Add(new MenuItem() { Title = filename });
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
                            MenuItem childItem=new MenuItem() { Title = path };
                            //the file
                            childItem.Items.Add(new MenuItem() { Title = filename });
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
                            v.folders.Items.Add(other_data);
                        }
                        //first time will not do it, other times yes
                        
                        if (other_root != null)
                        {
                            
                            v.folders.Items.Add(other_root);
                        }
                        other_root = new MenuItem() { Title = path };
                        other_root.Items.Add(new MenuItem() { Title = filename });
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
            v.folders.Items.Add(other_data);
            v.folders.Items.Add(other_root);
        }

        private void choose_folder_Click(object sender, RoutedEventArgs e)
        {
            /*
              * called when choose folder is clicked
              */
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
            string[] format = { "yyyyMMdd-HHmm" };
            DateTime date;
            String f;
            if (this.path.Text!= "") 
                try
                {
                    message.Content = "";
                    try
                    {
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
                    client.connect_to_server();
                    client.download_file(f,this.path.Text);
                    message.Content = "File scaricato correttamente";
                }
                catch (Exception ex)
                {
                    message.Content = "Errore di connessione al server nel download";
                }
            else
            {
                message.Content = "Scegli in quale cartella vuoi scaricare il file selezionato";
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
                message.Content = "Errore di connessione al server durante la sincronizzazione";
            }   
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                this.path_too_long = false;
                var interval = TimeSpan.FromMinutes(MyGlobalClient.minutes_for_synch);
                Thread.Sleep(interval);
                int result = 0; // used for the worker result
                var arg = (arguments)e.Argument; // to access elements ui from this thread
                (sender as BackgroundWorker).ReportProgress(0); //start pbar
                client.connect_to_server();
                client.synchronize(arg.path);
                e.Result = result;
            }
            catch (PathTooLongException ex)
            {
                e.Result = -1;
                this.path_too_long = true;
            }
            catch (Exception ex)
            {
                e.Result = -1;
            }
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState == null)
            {
                message.Content = "";
                pbar.Visibility = Visibility.Visible;
                pbar.IsIndeterminate = true;   
            }
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            /*
             * when synchronization ends, restart the timeout for next synchronization
             */
            pbar.Visibility = Visibility.Hidden;
            if ((int)e.Result != -1)
            {
                message.Content = "Sincronizzazione ok.";
            }
            else
            {
                if(path_too_long)
                {
                    message.Content = "Sincronizzazione ok. Alcuni file avevano un percorso troppo lungo e non sono stati trasferiti";
                }
                else
                {
                    message.Content = "Errore di connessione al server durante la sincronizzazione";
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

        public ObservableCollection<MenuItem> Items { get; set; }
    }
    public class arguments
    {
        public string path;
    }    
}