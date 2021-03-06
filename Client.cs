﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using System.Windows;
namespace ProgettoPDS
{
    
    public class Client
        //R. signup
        //L. login
        //S: sendzip,synchronize
        //A: ask presence of folder
        //V: view folder
        //D: download file or folder
    {
        private TcpClient tcpclnt;
        private string ipAddress;
        private int port;
        public string username;
        private string password;
        private Dictionary<string, string> file_hash = new Dictionary<string, string>();
        bool path_too_long = false;
        public Client(string username, string password){
            this.ipAddress = "192.168.0.1";
            //this.ipAddress = "10.30.216.191";
            this.port = 11000;
            this.username = username;
            this.password = password;
        }
       
        public TcpClient Tcpclnt
        {
            get;
            set;
        }
        public string Password
        {
            get
            {
                return password;
            }
            
        }
       
        public int receive(){
            /*
             * function that wrap the Receive.
             * returns 1 if OK, 0 if response!=OK, -1 if error connection
             */
            try
            {
                byte[] rcv = new byte[1500];
                int byteCount = tcpclnt.Client.Receive(rcv, SocketFlags.None);
                if (byteCount > 0)
                {
                    string rx = (string)Encoding.UTF8.GetString(rcv).Clone();
                    if (String.Compare(rx.Substring(0, 2), "OK") == 0)
                        return 1;
                    else return 0;
                }
                else return -1;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        
        public void connect_to_server()
        {
            /*
             * open a tcp connection with the server
             */
            try
            {
                tcpclnt = new TcpClient();
                Console.WriteLine("Connecting.....");
                var result=tcpclnt.BeginConnect(ipAddress, port,null,null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                if (!success)
                {
                    Console.WriteLine("Failed to connect");
                }
                // use the ipaddress as in the server program
                else
                {
                    Console.WriteLine("Connected");
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public int signup(string username,string password)
        {
            /*
             * send signup request to the server R.username.password
             */
            try
            {
                byte[] credentials = Encoding.UTF8.GetBytes("R"+"."+username+"."+password+"<EOF>");
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                int response = receive();
                if (response == 1)
                {
                    //proceed with signup
                    this.username = username;
                    this.password = password;
                }
                return response; 
            }
            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
                return -1;
            }
        }
        
        public int login(string username,string password)
        {
            /*
             * send login request. L.username.password
             */
            try
            {
                byte[] credentials = Encoding.UTF8.GetBytes("L" + "." + username + "." + password + "<EOF>");
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                int response = receive();
                return response;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
                return -1;
            }
        }

        public string ask_presence(string username)
        {
            /*
             *send request to ask if user already store a folder. A:username
             */
            try
            {
                byte[] credentials = Encoding.UTF8.GetBytes("A" + ":" + username);
                byte[] rcv = new byte[1500];
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                int byteCount = tcpclnt.Client.Receive(rcv, SocketFlags.None);
                if (byteCount > 0)
                {
                    string rx = (string)Encoding.UTF8.GetString(rcv).Clone();
                    rx=rx.Substring(0,rx.IndexOf('\0'));
                    return rx;
                }
                else
                {
                    return "Error connection with the server";
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
                return "Exception in ask presence";
            }
        }

        public int send_zip(string path,string file)
        {
            /*
             * send zipped folder the first time. S:path
             */
                //int l = path2.Length - 3;
                //string path = path2.Substring(3, l);
                byte[] credentials = Encoding.UTF8.GetBytes("S" + ":" + path);
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                int response = receive();
                if (response == 1)
                {
                    wrap_send_file(file);
                }
                else
                {
                    throw new Exception();
                }   
                //receive response after sending the zip
                response = receive();
                if (response==1)
                {
                    tcpclnt.Client.Close();
                    return 1;
                }
                else
                {
                    throw new Exception();
                }
             }
          

        public void browse_folder_json(string filename)
        {
            /*
             * browse list of files with md5. called in synchronize 
             */
            try
            {
                DirectoryInfo d = new DirectoryInfo(filename);
                foreach (var dir in d.GetDirectories())
                    browse_folder_json(dir.FullName);
                foreach (var file in d.GetFiles())
                {
                    Console.WriteLine(file.FullName);
                    //check on length of absolute path. if too long just don't send his info. veeery important
                    if (file.FullName.Length+MyGlobalClient.rootFolderServer.Length+username.Length < 255)
                    {
                        string hash = compute_md5(file.FullName);
                        int l = file.FullName.Length - 3;
                        string namefile = file.FullName.Substring(3, l);
                        file_hash.Add(namefile, hash);
                    }
                    else
                    {
                        Console.WriteLine("Path too long");
                        this.path_too_long = true;
                        
                    }
                }
            }
            catch
            {
                Console.WriteLine("Eccezione in browse folder json");
                throw;
            }
        }
        
        public string compute_md5(string filename)
        {
            var md5 = MD5.Create();
            var stream = File.OpenRead(filename);
            return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
        }

        public void wrap_send_file(string file)
        {
            /*
             * wrap the send of a file
             */
            try
            {
                StreamWriter sWriter = new StreamWriter(tcpclnt.GetStream()); //first chance exception system.io.ioexception
                //byte[] bytes = File.ReadAllBytes(file);
                
                
                //Console.WriteLine(bytes.Length.ToString());
                sWriter.WriteLine(new System.IO.FileInfo(file).Length.ToString());
                sWriter.Flush();
                //receive OK
                int response = receive();
                if (response==1)
                {
                   //send file
                   tcpclnt.Client.SendFile(file);
                }
                else
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }

        public void wrap_recv_zipfile(string f,string down_folder)
        {
            /*
             * wrap the receive of a file. used to download a selected folder
             */
            try
            {
                
                f = f.Substring(f.LastIndexOf("\\"), f.Length - f.LastIndexOf("\\"));
                FileStream fStream = new FileStream(down_folder+ f + ".zip", FileMode.Create);
                // read the file in chunks of 1MB
                var buffer = new byte[1024];
                int bytesRead;
                //read length and send ok
                bytesRead = tcpclnt.Client.Receive(buffer);
                string cmdFileSize = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                int length = Convert.ToInt32(cmdFileSize);
                buffer = new byte[1048576];
                byte[] credentials = Encoding.UTF8.GetBytes("OK");
                tcpclnt.Client.Send(credentials, SocketFlags.None);

                int received = 0;
                while (received < length)
                {
                    bytesRead = tcpclnt.Client.Receive(buffer);
                    received += bytesRead;
                    if (received >= length)
                    {
                        bytesRead = bytesRead - (received - length);
                    }
                    fStream.Write(buffer, 0, bytesRead);
                    Console.WriteLine("ricevuti: " + bytesRead + " qualcosa XD");
                }
                fStream.Flush();
                fStream.Close();
                Console.WriteLine("File Ricevuto");
            }
       
            catch (Exception ex)
            {
                throw;
            }
        }
        
        //change funcgtion name
        public string recv_file_json()
        {
            try
            {
                var buff = new byte[1024];
                int bytesRead;
                bytesRead = tcpclnt.Client.Receive(buff);
                string check = (string)Encoding.UTF8.GetString(buff).Clone();
                if (String.Compare(check.Substring(0, 3), "END") == 0)
                    return check;
                else
                {
                    string cmdFileSize = Encoding.ASCII.GetString(buff, 0, bytesRead);
                    int length = Convert.ToInt32(cmdFileSize);
                    //now receive the list
                    byte[] rcv = new byte[length];
                    byte[] credentials = Encoding.UTF8.GetBytes("OK");
                    tcpclnt.Client.Send(credentials, SocketFlags.None);
                    int received = 0;
                    int old_received = 0;
                    string rx = "";
                    buff = new byte[length];
                    while (received < length)
                    {
                        var buffer = new byte[length];

                        bytesRead = tcpclnt.Client.Receive(buffer);
                        old_received = received;
                        received += bytesRead;
                        if (received >= length)
                        {
                            bytesRead = bytesRead - (received - length);
                        }
                        System.Buffer.BlockCopy(buffer, 0, buff, old_received, bytesRead);

                        //wrap_write_file(fstream);//in which i should write the fstream part..
                        // string str = (string)Encoding.UTF8.GetString(buffer).Clone();
                        //rx += str;
                        Console.WriteLine("ricevuti: " + bytesRead + " qualcosa XD");
                    }
                    rx = (string)Encoding.UTF8.GetString(buff).Clone();
                    return rx;
                }
            }
            catch
            {
                Console.WriteLine("Errore in recv_file_json");
                throw;
            }
        }

        public void wrap_recv_file(string f,string down_folder)
        {
            /*
             * wrap the receive of a file
             */
            try
            {
                f = Path.GetFileName(f);
                f = Path.Combine(down_folder, f);
                FileStream fStream = new FileStream(f, FileMode.Create);
                // read the file in chunks of 1MB
                var buffer = new byte[1048576];
                int bytesRead;
                //read the length
                bytesRead = tcpclnt.Client.Receive(buffer);
                string cmdFileSize = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                int length = Convert.ToInt32(cmdFileSize);
                byte[] credentials = Encoding.UTF8.GetBytes("OK");
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                int received = 0;
                while (received < length)
                {
                    bytesRead = tcpclnt.Client.Receive(buffer);
                    received += bytesRead;
                    if (received >= length)
                    {
                        bytesRead = bytesRead - (received - length);
                    }
                    fStream.Write(buffer, 0, bytesRead);
                    Console.WriteLine("ricevuti: " + bytesRead + " qualcosa XD");
                }
                fStream.Flush();
                fStream.Close();
                Console.WriteLine("File Ricevuto");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void synchronize(string path)
        {
            string disk = path.Substring(0, 2);
            /*
             * synch request. S:path
             */
            try
            {
                //login at every synch
                if (login(username, password) != 1)
                {
                    throw new Exception();
                }
                //send synch request
                file_hash = new Dictionary<string, string>();
                //path MUST contain the C: or D: 
                byte[] credentials = Encoding.UTF8.GetBytes("S" + ":" + path);
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                int response = receive();
                if (response == 1)
                {
                    string dir = path;
                    //create file
                    this.path_too_long = false;
                    browse_folder_json(dir);
                    string json = JsonConvert.SerializeObject(file_hash);
                    credentials = Encoding.UTF8.GetBytes(json);
                    tcpclnt.Client.Send(credentials, SocketFlags.None);
                    //check if empty sendlist
                    string rx=recv_file_json();
                    if (String.Compare(rx.Substring(0, 3), "END") != 0)
                        {
                           List<string> to_send = JsonConvert.DeserializeObject<List<string>>(rx);
                           //send the new requested files
                           //read file line by line
                           foreach (var f in to_send)
                           {
                                string newf = f;
                                string file_to_send = disk +"\\"+ newf;
                                wrap_send_file(file_to_send);
                                //receive something at each send
                                if (receive()!=1)
                                    break;
                            }

                        }
                            // close socket after every synch
                            //if sendlist empty just close socket
                            // tcpclnt.Client.Close(); //i will not close the socket and use the same for the next visualization
                            //i need to do this to keep the server simple
                    }
            }
            catch (Exception e)
            {
                throw new SynchronizeException(); //to distinguish whether the synch was not ok or only the visualization
            }
            try
            {
                //connect_to_server();
                List<string> items = new List<string>();
                items = view_folders();
                //ViewFolder.viewFolder.folders.Items.Clear();
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.GetType() == typeof(ViewFolder))
                        {
                            (window as ViewFolder).folders.Items.Clear();
                            (window as ViewFolder).create_tree(items, (window as ViewFolder));
                        }
                    }
                    //(Application.Current.MainWindow as ViewFolder).folders.Items.Clear();
                    // Window owner = System.Windows.Application.Current.MainWindow;

                    // Use owner here - it must be used on the UI thread as well..
                    //ShowMyWindow(owner);
                }));
                if (this.path_too_long)
                {
                     throw new PathTooLongException();
                }
            }
            catch (Exception e)
            {
                throw;
            }
                //ViewFolder.viewFolder.create_tree(items); 
                
            //}
        }

        public List<string> view_folders(bool first_synch=false){
            /*
             * send view request for visualize files in the server. V:username
             */
            try
            {
                if (first_synch == true)
                {
                    if (login(username, password) != 1)
                    {
                        throw new Exception();
                    }
                }
                //view request
                byte[] credentials = Encoding.UTF8.GetBytes("V:" + username);
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                //receive the list of folders, and the list of files in json. show them
                //receive the size before to prepare a coherent buffer
                string rx = recv_file_json();
                List<string> to_view = JsonConvert.DeserializeObject<List<string>>(rx);
                // close socket after every synch
                tcpclnt.Client.Close();
                return to_view;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }

        //TODO: change class in which this method is located, maybe add a folder class
        public long dirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += dirSize(di);
            }
            return size;
        }

        public void download_file(String f,String download_folder,bool isDirectory)
        {
            /*
             * send download request. D:filename
             */
            try
            {
                if (login(username, password) != 1)
                {
                    throw new Exception();
                }
                byte[] credentials = Encoding.UTF8.GetBytes("D:" + f);
                tcpclnt.Client.Send(credentials, SocketFlags.None);
                //var ext = System.IO.Path.GetExtension(f);
                if (isDirectory)
                {
                    //It's a directory
                    wrap_recv_zipfile(f,download_folder);
                }
                else
                {
                    wrap_recv_file(f,download_folder);
                }
                tcpclnt.Client.Close();
            }
            catch
            {
                throw;
            }
        }
        //this would be the version with task in place of backgroundoworker
        /*
        //we will call this periodic method after we checked we already have something synchronized
        public async Task periodicSynchronization(TimeSpan dueTime, 
                                             TimeSpan interval,
                                             CancellationToken token,string path2)
        {
            
            // Initial wait time before we begin the periodic loop.
            if (dueTime > TimeSpan.Zero)
                await Task.Delay(dueTime, token);
            
            // Repeat this loop until cancelled.
            while (!token.IsCancellationRequested)
            {
                // Do some kind of work here. 
                Console.WriteLine("synchronization");
                {
                    connect_to_server();
                    synchronize(path2);
                }
                // Wait to repeat again.
                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, token);
            }
           
              }
    */
        
       
    }

    public class SynchronizeException : Exception
    {
        public SynchronizeException() 
        { 
        
        }
    }

    
}
