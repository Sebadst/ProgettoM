using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Microsoft.VisualBasic;
namespace ServerPDS
{
    class Logger
    {
        private string password;
        private string username;
        private Socket s;
        Thread t;
        DBConnect db;
        BlockingCollection<string> ac;
        //vars aggiunte da seba
        //TODO: change var name and check which one are useless (for instance ac)
        Dictionary<string, string> dictionary = new Dictionary<string, string>();
        Dictionary<string, string> file_hash = new Dictionary<string, string>();
        List<string> view_list = new List<string>();
        List<string> copylist = new List<string>();
        List<string> sendlist = new List<string>();

        //TODO: HOW THE HELL DO I DO EVERYTHING ATOMIC NOW? I MUST USE MYSQL ROLLBACK, NO OTHER WAY ACCORDING TO ME..
        //IT ALSO SOLVES MY OTHER ISSUE OF HAVING MULTIPLE TIMES THE SAME FILE INTO THE SERVER. THIS MEANS HOWEVER THAT ALL THE SYNCHRONIZE
        //MUST BE CHANGED. LET S TRY AT LEAST TO DO NOT CHANGE THE VISUALIZE 
        public Logger(Socket sock, string username, string password, BlockingCollection<string> ac)
        {
            this.s = sock;
            this.password = password;
            this.username = username;
            //instantiate the thread
            t = new Thread(new ThreadStart(this.action));
            db = new DBConnect();
            this.ac = ac;
            t.Start();
        }

        public void ask_presence()
        {
            /*
             * handle the request of the client to know if a folder has been already stored
             */
            //recover the name of folder that user wants to synch
            //path to sync in format without c:
            //recover the root of the user into the server
            string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
            userFolderIntoServer = System.IO.Path.Combine(userFolderIntoServer, "1");

            if (Directory.Exists(userFolderIntoServer))
            {
                string query = "select * from utenti where username='" + username + "'";
                string to_send = db.Select(query).ElementAt(2).First();
                byte[] msg = Encoding.ASCII.GetBytes(to_send);
                s.Send(msg);
            }
            else
            {
                //invio OK
                byte[] msg = Encoding.ASCII.GetBytes("NULL");
                s.Send(msg);
            }
            Console.WriteLine("Risposta al comando ask inviata");
        }

        public void view_request()
        {
            /*
             * handle the request of client to view what is stored on server side
             */
            //recover the root of user into server
            string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
            string json = null;
            for (int i = 1; i <= MyGlobal.num_versions; i++)
            {
                string pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, i.ToString());        
                if (Directory.Exists(pathIntoServer))
                {
                    if (i == 1)
                    {
                        //start constructing the json
                        view_list.Clear();
                    }
                    string query = "select data from cartelle where username='" + username + "' and versione ='"+i+"'";

                    string date = db.Select(query).ElementAt(0).First();
                    string formatted_date = date.Substring(6, 4) + date.Substring(3, 2) + date.Substring(0, 2) + "-" + date.Substring(11, 2) + date.Substring(14, 2);

                    view_list.Add(formatted_date);
                    browse_folder_list_version(pathIntoServer, view_list);
                }
            }
            //finally send the json
            json = JsonConvert.SerializeObject(view_list);
            byte[] credentials = Encoding.UTF8.GetBytes(json);
            //send the dimension in order to prepare a coherent buffer in client
            byte[] length = Encoding.UTF8.GetBytes(credentials.Length.ToString());
            s.Send(length, SocketFlags.None);
            //receive ok
            byte[] bytes_rec = new Byte[1024];
            int response = s.Receive(bytes_rec);
            if (response > 0)
            {
                //send file
                s.Send(credentials, SocketFlags.None);
            }
        }

        public void synchronize_request(string[] words)
        {
            /*
             * handle the synchronization request of the client
             */
            //recover the name of folder that user wants to synch
            //path to sync in format without c:
            string pathIntoClient = words[1];
            //recover the root of user into server
            string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
            userFolderIntoServer = System.IO.Path.Combine(userFolderIntoServer, "1");
            //look for the folder
            string pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
            if (Directory.Exists(pathIntoServer))
            {
                sincronizzaFiles(pathIntoServer);
            }
            else
            {
                sincronizzaDirectory(userFolderIntoServer, pathIntoClient, pathIntoServer);
            }
            Console.WriteLine("sincronizzazione Finita");
        }

        public void download_request(string cmd)
        {
            /*
             * handle the download request of the client (of a file or folder)
             */
            //recover the root of user into server
            string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
            string to_download = System.IO.Path.Combine(userFolderIntoServer, cmd.Substring(2, cmd.Length - 2));
            if (Directory.Exists(to_download))
            {
                wrap_send_directory(to_download);
            }
            else if (File.Exists(to_download))
            {
                wrap_send_file(to_download);
            }
            else
            {
                byte[] msg = Encoding.ASCII.GetBytes("E. not present");
                s.Send(msg);
            }
        }

        public void GestoreClient()
        {
            /*
             * check and serve request following a login request 
             */
            try
            {
                while (true)
                {
                    byte[] bytes = new Byte[1024];
                    //receive the folder to synch
                    int bytesRec = s.Receive(bytes);
                    string cmd = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    //verify the command
                    if (String.Compare(cmd.Substring(0, 2), "S:") != 0 && String.Compare(cmd.Substring(0, 2), "A:") != 0 && String.Compare(cmd.Substring(0, 2), "V:") != 0 && String.Compare(cmd.Substring(0, 2), "D:") != 0)
                    {
                        byte[] msg = Encoding.ASCII.GetBytes("E.comando errato");
                        s.Send(msg);
                        s.Shutdown(SocketShutdown.Both);
                        throw new Exception("E.comando errato diverso da S:path");
                    }
                    //split the string
                    char[] delimiterChars = { ':' };
                    string[] words = cmd.Split(delimiterChars);
                    if (words.Length != 2)
                    {
                        byte[] msg = Encoding.ASCII.GetBytes("E.comando errato");
                        s.Send(msg);
                        s.Shutdown(SocketShutdown.Both);
                        throw new Exception("E.comando errato words.Leght!=3");
                    }
                    //ask for presence of directory to know if client has to load viewfolder or addfolder
                    if (String.Compare(cmd.Substring(0, 2), "A:") == 0)
                    {
                        ask_presence();
                    }
                    //view request
                    else if (String.Compare(cmd.Substring(0, 2), "V:") == 0)
                    {
                        view_request();
                        //out from the loop after V request
                        break;
                    }
                    //synchronize file or directory
                    else if (String.Compare(cmd.Substring(0, 2), "S:") == 0)
                    {
                        synchronize_request(words);
                        //out of the loop with S request
                        break;
                    }
                    else if (String.Compare(cmd.Substring(0, 2), "D:") == 0)
                    {
                        download_request(cmd);
                        //out of the loop with D request
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                s.Close();
                Console.WriteLine("Sessione terminata");
            }
        }


        public void sincronizzaDirectory(string ufis, string pic, string pis)
        {
            /*
             * called for 1st synch
             */
            try
            {
                string escapedPath = pic.Replace(@"\", @"\\").Replace("'", @"\'");
                string query = "update utenti set folder='" + escapedPath + "' where username='" + username + "'";
                db.Update(query);
                //send OK
                byte[] msg = Encoding.ASCII.GetBytes("OK");
                s.Send(msg);
                //receive the file.zip
                FileStream fStream = new FileStream("u1.zip", FileMode.Create);
                // read the file in chunks of 1MB
                var buffer = new byte[1048576];
                int bytesRead;
                //read the length
                bytesRead = s.Receive(buffer);
                string cmdFileSize = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                int length = Convert.ToInt32(cmdFileSize);
                s.Send(msg);
                int received = 0;
                while (received < length)
                {
                    bytesRead = s.Receive(buffer);
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
                //create the first upload folder for client
                DirectoryInfo u1 = Directory.CreateDirectory(System.IO.Path.Combine("1", pis));
                Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(pis));
                //store the date into the db cartelle. 
                string creation_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
                //store the date into the db cartelle. 
                query = "insert into cartelle values('1','" + username + "','" + creation_time + "')";
                db.Insert(query);
                //extract
                ZipFile.ExtractToDirectory("u1.zip", pis);
                Console.WriteLine("file estratto");
                //send the last ok to client
                msg = Encoding.ASCII.GetBytes("OK");
                s.Send(msg);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                s.Close();
                Console.WriteLine("Sessione terminata");
            }
        }

        private Tuple<bool, string> check_folder(string userFolderIntoServer, string pathIntoClient, string pathIntoServer, string pathString, bool flag,string version)
        {
            browse_folder(System.IO.Path.Combine(userFolderIntoServer, version), dictionary);
            //un file "percorso(radice la directory iniziale) hash" uno per riga
            //System.IO.StreamReader myfile = new System.IO.StreamReader(filePath);
            foreach (var line in file_hash)
            {
                string key = line.Key;
                string value = line.Value;
                if (dictionary.ContainsKey(version+"\\" + key))
                {
                    if (value == dictionary[version+"\\" + key])
                    {
                        //because the folder 3 will become the 2, so I will put 2
                        if (int.Parse(version) == MyGlobal.num_versions)
                            copylist.Add((int.Parse(version) - 1).ToString() + "\\" + key);
                        else
                            copylist.Add(version + "\\" + key);
                    }
                    else
                    {
                        sendlist.Add(key);
                    }
                    //i remove to do the check on files deletede afterwards
                    dictionary.Remove(version+"\\" + key);
                }
                else
                {
                    sendlist.Add(key);
                }
            }
            
            //if deleted files remain then count>0
            if (dictionary.Count > 0 || sendlist.Count != 0)
            {
                if (int.Parse(version)!=MyGlobal.num_versions)
                {
                    flag = true;
                    //questa diventa 3
                    pathIntoClient = (int.Parse(version)+1).ToString();

                    //cerco la cartella
                    pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
                }
                else
                {
                    flag=true;
                    for (int i=1;i<=MyGlobal.num_versions;i++)
                    {
                        if(i!=MyGlobal.num_versions)
                        {
                            pathIntoClient=i.ToString();
                            pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
                            string pathIntoClient2 = (i + 1).ToString();
                            string userFolderIntoServer2 = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                            string pathIntoServer2 = System.IO.Path.Combine(userFolderIntoServer2, pathIntoClient2);
                            
                            DeleteDirectory(pathIntoServer);
                            Directory.CreateDirectory(pathIntoServer);

                            //la 2 diventa 1
                            // i do a copy in place of a move
                            string[] files = System.IO.Directory.GetFiles(pathIntoServer2);
                            // Copy the files and overwrite destination files if they already exist.
                            foreach (string dirPath in Directory.GetDirectories(pathIntoServer2, "*",
            SearchOption.AllDirectories))
                                Directory.CreateDirectory(dirPath.Replace(pathIntoServer2, pathIntoServer));
                            //Copy all the files & Replaces any files with the same name
                            foreach (string newPath in Directory.GetFiles(pathIntoServer2, "*.*",
                                SearchOption.AllDirectories))
                                File.Copy(newPath, newPath.Replace(pathIntoServer2, pathIntoServer), true);
                        }
                        else
                        {
                            pathIntoClient = i.ToString();
                            pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
                            DeleteDirectory(pathIntoServer);

                                   
                    //store into the db the date of 3rd folder. 
                    string creation_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
                    //store the date into the db cartelle. 
                    string query = "insert into cartelle values('3','" + username + "','" + creation_time + "')";
                    db.Insert(query);
                        }
                    }
                    
                }
                
            }
            return Tuple.Create(flag, pathIntoServer);
        }

        public void sincronizzaFiles(string path)
        {
            /*
             * called for all the synch but the 1st
             */
            try
            {
                bool files_to_load = false;
                Console.WriteLine("Sincronizzazione file");
                //send OK
                byte[] msg = Encoding.ASCII.GetBytes("OK");
                s.Send(msg);
                //receive filelist:
                receiveFile_json();
                string pathString = null;
                string pathIntoClient = null;
                //recover root user into server
                string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                string pathIntoServer = null;
                //general version, without bad check_1st etc
                for (int version = MyGlobal.num_versions; version > 0; version--)
                {
                    if (Directory.Exists(System.IO.Path.Combine(userFolderIntoServer, version.ToString())))
                    {
                        Tuple<bool, string> i = check_folder(userFolderIntoServer, pathIntoClient, pathIntoServer, pathString, files_to_load,version.ToString());
                        files_to_load = i.Item1;
                        pathIntoServer = i.Item2;
                        break;
                    }
                }

                // Read the file and display it line by line.
                if (files_to_load == true)
                {
                    if (sendlist.Count > 0)
                    {

                        string json = JsonConvert.SerializeObject(sendlist);
                        byte[] credentials = Encoding.UTF8.GetBytes(json);
                        //send the dimension in order to prepare a coherent buffer in client
                        byte[] length = Encoding.UTF8.GetBytes(credentials.Length.ToString());
                        s.Send(length, SocketFlags.None);
                        byte[] bytes_rec = new Byte[1024];
                        int response = s.Receive(bytes_rec);
                        if (response > 0)
                        {
                            //before i have to send the length and receive ok
                            s.Send(credentials, SocketFlags.None);
                        }
                    }

                    //now let's create the new folder with the copylist and the files from the sendlist
                    //create the folder
                    DirectoryInfo u1 = Directory.CreateDirectory(pathIntoServer);
                    Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(pathIntoServer));
                    //copy files
                    //commented for the moment
                    foreach (var f in copylist)
                    {
                        //split f with filename in the last part
                        string uFIS = Path.Combine(userFolderIntoServer, Path.GetDirectoryName(f));
                        string filename;
                        filename = Path.GetDirectoryName(f).Substring(2);
                        string pIS = Path.Combine(pathIntoServer, filename);
                        copyFile(uFIS, pIS, Path.GetFileName(f));
                    }
                    //receive files
                    foreach (var f in sendlist)
                    {
                        //receive
                        string pIS = Path.Combine(pathIntoServer, f);
                        //check if directory already exists
                        if (!Directory.Exists(Path.Combine(pathIntoServer, Path.GetDirectoryName(f))))
                        {
                            Directory.CreateDirectory(Path.Combine(pathIntoServer, Path.GetDirectoryName(f)));
                        }

                        receiveFile(pIS);
                        msg = Encoding.ASCII.GetBytes("OK");
                        s.Send(msg);
                    }
                    //at the very end store date into the db
                    string creation_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss");

                    //store the date into the db cartelle. TODO: it works only with 9 versions at most
                    string query = "insert into cartelle values('" + pathIntoServer.Substring(pathIntoServer.Length - 1, 1) + "','" + username + "','" + creation_time + "')";
                    db.Insert(query);
                }
                //nothing in sendlist
                if (sendlist.Count == 0)
                {
                    msg = Encoding.ASCII.GetBytes("END");
                    s.Send(msg);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Eccezione in server..sincronizza files");
            }
        }

        public void copyFile(string sourcePath, string targetPath, string fileName)
        {
            /*
             * function for files to be copied from an old version of the folder to a new one
             */
            // Use Path class to manipulate file and directory paths.
            string sourceFile = System.IO.Path.Combine(sourcePath, fileName);
            string destFile = System.IO.Path.Combine(targetPath, fileName);
            // To copy a folder's contents to a new location:
            // Create a new target folder, if necessary.
            if (!System.IO.Directory.Exists(targetPath))
            {
                System.IO.Directory.CreateDirectory(targetPath);
            }
            // To copy a file to another location and 
            // overwrite the destination file if it already exists.
            System.IO.File.Copy(sourceFile, destFile, true);
            File.SetAttributes(destFile, FileAttributes.Normal);
        }

        public void receiveFile_json()
        {
            //TODO: 15 MB should be enough for just the list of files but careful because it should be corrected probably sending the dimension and dimensioning the buffer in that way, like for the normal files
            byte[] rcv = new byte[15728640];
            int byteCount = s.Receive(rcv, SocketFlags.None);
            string rx = (string)Encoding.UTF8.GetString(rcv).Clone();
            file_hash = JsonConvert.DeserializeObject<Dictionary<string, string>>(rx);
        }

        public void receiveFile(string fileName)
        {
            try
            {
                FileStream fStream = new FileStream(fileName, FileMode.Create);
                // read the file in chunks of 1MB
                var buffer = new byte[1028576];
                int bytesRead;
                //read the length
                bytesRead = s.Receive(buffer);
                string cmdFileSize = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                int length = Convert.ToInt32(cmdFileSize);
                byte[] msg = Encoding.ASCII.GetBytes("OK");
                s.Send(msg);
                int received = 0;
                while (received < length)
                {
                    bytesRead = s.Receive(buffer);
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
            catch
            {
                Console.WriteLine("eccezione in server..receiveFile");
            }
        }


        public string compute_md5(string filename)
        {
            var md5 = MD5.Create();
            FileStream stream = File.OpenRead(filename);
            string k = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
            stream.Flush();
            stream.Close();
            return k;
        }

        //I added the parameter dictionary, check if works
        public void browse_folder(string filename, Dictionary<string, string> dictionary)
        {
            DirectoryInfo d = new DirectoryInfo(filename);
            foreach (var dir in d.GetDirectories())
                browse_folder(dir.FullName, dictionary);
            foreach (var file in d.GetFiles())
            {
                Console.WriteLine(file.FullName);
                string hash = compute_md5(file.FullName);
                //to delete the first part of the path
                string namefile = file.FullName.Substring(MyGlobal.rootFolder.Length + 1 + username.Length + 1);
                dictionary.Add(namefile, hash);
            }
        }
        //TODO: fix the problem that i send only the files while i should send also folder in order to display empty folder in the client
        //or maybe i can leave it in this way since i will not have to download empty folders but only files (or folders with something)
        //see at the very end
        public void browse_folder_list_version(string filename, List<string> list)
        {
            DirectoryInfo d = new DirectoryInfo(filename);
            //doing this before than iterating on folders permits to have everything already ordered
            foreach (var file in d.GetFiles())
            {
                Console.WriteLine(file.FullName);
                string hash = compute_md5(file.FullName);
                //to delete the first part of the path
                // +1 to remove the //
                string namefile = file.FullName.Substring(MyGlobal.rootFolder.Length + 1 + username.Length + 1);
                list.Add(namefile);

            }
            foreach (var dir in d.GetDirectories())
                browse_folder_list_version(dir.FullName, list);

        }


        private void action()
        {
            string query = "select count(*) from utenti where username = " + "'" + username + "'";
            if (db.Count(query) > 0)//utente esistente
            {
                query = "select count(*) from utenti where username = " + "'" + username + "'" + "and password = " + "'" + Register.HashPassword(password) + "'";
                if (db.Count(query) > 0)//password corretta
                {
                    //invio OK
                    byte[] msg = Encoding.ASCII.GetBytes("OK");
                    s.Send(msg);
                    //Console.WriteLine("utente loggato con chiave: "+chiave);
                    Console.WriteLine("utente loggato ip: " + s.RemoteEndPoint);
                    GestoreClient();
                }
                else
                {
                    byte[] msg = Encoding.ASCII.GetBytes("E.password errata!");
                    s.Send(msg);
                    s.Shutdown(SocketShutdown.Both);
                    s.Close();
                    Console.WriteLine("E.password errata!");
                }
            }
            else
            {
                byte[] msg = Encoding.ASCII.GetBytes("E.inesistente/password errata, registrati!");
                s.Send(msg);
                s.Shutdown(SocketShutdown.Both);
                s.Close();
                Console.WriteLine("E.inesistente, registrati!");
            }
            //initialize everything again
            dictionary = new Dictionary<string, string>();
            file_hash = new Dictionary<string, string>();
            copylist = new List<string>();
            sendlist = new List<string>();
        }

        public void wrap_send_directory(string directory)
        {
            //  ZIP THE FILE
            string startPath = directory;
            string zipPath = MyGlobal.rootFolder + "\\" + username + ".zip";
            // string extractPath = @"C:\Users\sds\Desktop\progetto";
            ZipFile.CreateFromDirectory(startPath, zipPath);
            wrap_send_file(zipPath);
            File.Delete(zipPath);
        }

        public void wrap_send_file(string file)
        {
            try
            {
                StreamWriter sWriter = new StreamWriter(new NetworkStream(s)); //first chance exception system.io.ioexception        
                byte[] bytes = File.ReadAllBytes(file);
                Console.WriteLine(bytes.Length.ToString());
                sWriter.WriteLine(bytes.Length.ToString());
                sWriter.Flush();
                //it has to receive ok, like the client version
                byte[] bytes_rec = new Byte[1024];
                int response = s.Receive(bytes_rec);
                if (response > 0)
                {
                    //send file
                    s.SendFile(file);
                }
            }
            catch
            {
                Console.WriteLine("Eccezione in wrap send file");
            }
        }

        public void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);
            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }
            Directory.Delete(target_dir, false);
        }

    }
}
