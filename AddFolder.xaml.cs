using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.IO.Compression;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel;

namespace ProgettoPDS
{
    /// <summary>
    /// Logica di interazione per AddFolder.xaml
    /// </summary>
    public partial class AddFolder : Window
    {
        Client client;
        bool redo_login = false;
        public AddFolder(Client client)
        {
            InitializeComponent();
            this.client = client;
            pbar.IsIndeterminate = false;
            pbar.Visibility = Visibility.Hidden;
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
        private void browse_folder_length(string filename)
        {
            /*
             * browse list of files with md5. called in synchronize 
             */
            
            
                DirectoryInfo d = new DirectoryInfo(filename);
                foreach (var dir in d.GetDirectories())
                    browse_folder_length(dir.FullName);
                foreach (var file in d.GetFiles())
                {
                    if (file.FullName.Length + MyGlobalClient.rootFolderServer.Length + client.username.Length > 255)
                    {
                        throw new PathTooLongException();
                    }
                }
            
        }
        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            /*
             * asynchronous logic
             */
            try
            {
                // to access elements ui from this thread
                var arg = (arguments)e.Argument;
                //start pbar
                (sender as BackgroundWorker).ReportProgress(0); 
                //put the operation here
                //check if there is something too long
                try
                {
                    browse_folder_length(arg.path);
                }
                catch
                {
                    e.Result = -2;
                    return;
                }
                string zipPath = System.IO.Path.Combine(MyGlobalClient.zipDirectory,"result.zip");
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                ZipFile.CreateFromDirectory(arg.path, zipPath);
                //check if there was a problem before. in this case open a new socket and do login before
                if (redo_login)
                {
                    
                    client.connect_to_server();
                    int login = client.login(client.username, client.Password);
                }
                //send the zip file
                client.send_zip(arg.path, zipPath);    
                //check if it exists just as best practice, but normally it exists
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                e.Result=1;
            }
            catch (Exception ex)
            {
                //in case of error connection or other errors
                e.Result = -1;
            }              
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState == null)
            {
                pbar.Visibility = Visibility.Visible;
                pbar.IsIndeterminate = true;
                message.Content = "Sincronizzazione in corso, attendi";
            }
         }
        
        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if ((int)e.Result == 1)
                {
                    //we pass also path.text to be used in synchronization afterwards
                    ViewFolder view = new ViewFolder(client, path.Text);
                    view.Show();
                    this.Close();
                }
                else
                {
                    pbar.Visibility = Visibility.Hidden;
                    pbar.IsIndeterminate = false;
                    if ((int)e.Result == -1)
                        message.Content = "Errore, server non raggiungibile";
                    else if ((int)e.Result == -2)
                        message.Content = "Alcuni files hanno un percorso troppo lungo, cambiare e riprovare";
                    redo_login = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }
       
        private void load_folder_Click(object sender, RoutedEventArgs e)
        {
            /*
             * called when the user click the load_folder button
             */
            if (path.Text != "")
            {
                //  zip the file
                string startPath = @path.Text;
                //check dimension<1gb
               /* if (client.dirSize(new DirectoryInfo(startPath)) > MyGlobalClient.folder_max_dim)
                {
                    message.Content = "Non e' possibile caricare cartelle di dimensioni maggiori di 1 GB";
                }
                else
                {*/
                    try
                    {
                        //asynchronous logic
                        BackgroundWorker worker = new BackgroundWorker();
                        worker.WorkerReportsProgress = true;
                        worker.DoWork += worker_DoWork;
                        worker.ProgressChanged += worker_ProgressChanged;
                        worker.RunWorkerCompleted += worker_RunWorkerCompleted;
                        var arg = new arguments() { path = startPath };
                        worker.RunWorkerAsync(arg);
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc.StackTrace);
                        message.Content = "Errore, impossibile contattare il server";
                    }
                    
                //}
            }
   
        }
    }
}
