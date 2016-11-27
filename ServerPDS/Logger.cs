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
        List<Tuple<string,string>> sendlist = new List<Tuple<string,string>>();

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
            string query = "select count(*) from cartelle where username='" + username + "'";
            int count = db.Count(query);
            if (count>0)
            {
                query = "select * from utenti where username='" + username + "'";
                string to_send = db.Select(query,new List<string>[3]).ElementAt(2).First();
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
            //string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
            string json = null;
            string query = "select min(folder_version) from files where username='" + username + "'";
            int min_version= Convert.ToInt32(db.Select(query, new List<string>[1]).ElementAt(0).First());
            
            for (int i = min_version; i <= MyGlobal.num_versions+min_version; i++)
            {
                query = "select filename from files where username='" + username + "' and folder_version='"+i.ToString()+"'";
                List<string> filenames=db.Select(query,new List<string>[1]).ElementAt(0);
                if (filenames.Count==0)
                {
                    break;
                }
                if (i == min_version)
                {
                    view_list.Clear();
                }
                query = "select data from cartelle where username='" + username + "' and versione ='" + i + "'";//TODO: check if i should leave it like this or between 1 and 3, according to what i show to the user

                string date = db.Select(query, new List<string>[1]).ElementAt(0).First();
                string formatted_date = date.Substring(6, 4) + date.Substring(3, 2) + date.Substring(0, 2) + "-" + date.Substring(11, 2) + date.Substring(14, 2);

                view_list.Add(formatted_date);
                //SORT ACCORDING TO NUMBER OF SEPARATORS. WITHOUT THIS, THE VIEW DOES NOT WORK IN CLIENT
                var sorted_filenames = filenames.OrderBy(
    p => p.Count(c => c == Path.DirectorySeparatorChar
        || c == Path.AltDirectorySeparatorChar));
                foreach (var el  in  sorted_filenames)
                {
                    view_list.Add(Path.Combine(i.ToString(), el));

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
            ///userFolderIntoServer = System.IO.Path.Combine(userFolderIntoServer, "1");
            string pathIntoServer;
            userFolderIntoServer = db.Select("SELECT MAX(FOLDER_VERSION) FROM FILES WHERE USERNAME='" + username + "'", new List<string>[1]).ElementAt(0).First();
            if (userFolderIntoServer !="")
            {            
                pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
                sincronizzaFiles(pathIntoServer,userFolderIntoServer);
            }
            else
            {
                //if is first synch
                userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                userFolderIntoServer = System.IO.Path.Combine(userFolderIntoServer, "1");
                pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);

                sincronizzaDirectory(pathIntoClient, pathIntoServer);
            }
            Console.WriteLine("sincronizzazione Finita");
        }

        public void download_request(string cmd)
        {
            /*
             * handle the download request of the client (of a file or folder)
             */
            //recover the root of user into server
            //TODO: after synch
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


        public void sincronizzaDirectory( string pic, string pis)
        {
            /*
             * called for 1st synch
             */
            //TODO: set it atomic. i must be sure that i will do undo only of the ones that are written here, so check if i 
            //am doing a new connection with db or not
            string query;

            using (var con = db.getConnection())
            {
                db.OpenConnection();
                MySqlTransaction tran = con.BeginTransaction();

                try
                {
                    string escapedPath = pic.Replace(@"\", @"\\").Replace("'", @"\'");

                    query = "update utenti set folder='" + escapedPath + "' where username='" + username + "'";
                    db.Update(query, false);
                    //send OK
                    byte[] msg = Encoding.ASCII.GetBytes("OK");
                    s.Send(msg);
                    //receive the file.zip
                    FileStream fStream = new FileStream(username+".zip", FileMode.Create);
                    //chunks of 1MB
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
                db.Insert(query,false);
                //extract
                ZipFile.ExtractToDirectory(username+".zip", pis);
                Console.WriteLine("file estratto");
                //store also the files    
                Dictionary<string,string> files_to_store=new Dictionary<string,string>();
                this.browse_folder(pis,files_to_store);
                foreach (KeyValuePair<string, string> entry in files_to_store)
                {
                    //escape the path after removing the info concerning the version , 1 in this case
                    escapedPath = entry.Key.Substring(2).Replace(@"\", @"\\").Replace("'", @"\'");
                    
                    query = "INSERT INTO FILES VALUES('" + username + "','" + escapedPath + "','" + entry.Value + "','1','1')";
                    db.Insert(query,false);
                }
                    //send the last ok to client
                    msg = Encoding.ASCII.GetBytes("OK");
                    s.Send(msg);
                    s.Close();
                    Console.WriteLine("Sessione terminata");
                    tran.Commit();
                    db.CloseConnection();
                }
                catch (Exception e)
                {
                    tran.Rollback();
                    db.CloseConnection();
                    Console.WriteLine(e.StackTrace);
                    s.Close();
                    if (File.Exists(username+".zip"))
                    {
                        File.Delete(username+".zip");
                    }
                    if (Directory.Exists(pis))
                    {
                        DeleteDirectory(pis);
                    }
                    Console.WriteLine("Sessione terminata");
                    //TODO: remove also the file (zip if is present, folder if is present)
                    return;
                }
            }
        }

       
        public void sincronizzaFiles(string path,string last_version)
        {
            /*
             * called for all the synch but the 1st
             */
            Dictionary<string,string> remove_dict=new Dictionary<string,string>();
            List<string> new_paths = new List<string>();
            //recover root user into server
            string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
            string pathIntoServer = null;
            using (var con = db.getConnection())
            {
                db.OpenConnection();
                MySqlTransaction tran = con.BeginTransaction();

                try
                {
                    bool files_to_load = false;
                    Console.WriteLine("Sincronizzazione file");
                    //send OK
                    byte[] msg = Encoding.ASCII.GetBytes("OK");
                    s.Send(msg);
                    //receive filelist:
                    receiveFile_json();
                    

                    //check the files of the last version 
                    string query = "select filename,hash,path from files where username='" + username + "' and folder_version='" + last_version + "'";
                    List<string>[] old_files = db.Select(query, new List<string>[3],false);
                    dictionary = old_files[0].Zip(old_files[1], (k, v) => new { k, v })
                      .ToDictionary(x => x.k, x => x.v);
                    //fill copylist and sendlist
                    foreach (var line in file_hash)
                    {
                        string key = line.Key;
                        string value = line.Value;
                        if (dictionary.ContainsKey(key))
                        {
                            if (value == dictionary[key])
                            {
                                copylist.Add(key);
                            }
                            else
                            {
                                sendlist.Add(new Tuple<string, string>(key, value));
                            }
                            //i remove to do the check on files deletede afterwards
                            dictionary.Remove(key);
                        }
                        else
                        {
                            sendlist.Add(new Tuple<string, string>(key, value));
                        }
                    }

                    //if deleted files remain then count>0. in both cases a new version has to be created
                    if (dictionary.Count > 0 || sendlist.Count != 0)
                    {
                        files_to_load = true;
                        //insert the files of the copylist into the db with the new version number
                        //query = "select username,filename,hash,path from files where username='" + username + "' and folder_version='" + (Convert.ToInt32(last_version) - 1).ToString() + "'";
                        //List<string>[] old_files = db.Select(query, new List<string>[5]);
                        for (int entry = 0; entry < old_files[0].Count; entry++)
                        {
                            if (copylist.Contains(old_files[0][entry]))
                            {
                                query = "insert into files values('" + username + "','" + old_files[0][entry].Replace(@"\", @"\\").Replace("'", @"\'") + "','" + old_files[1][entry] + "','" + old_files[2][entry] + "','" + (Convert.ToInt32(last_version) + 1).ToString() + "')";
                                db.Insert(query,false);
                            }
                        }
                        //remember that if i insert or delete but not commit and then i do select, i see the result like if i committed if 
                        //i am in the same transaction
                        //remove from db oldest version and put in remvoelist_ those files to be removed phisically of the old version that are not referenced in other versions
                        if (Convert.ToInt32(last_version) >= MyGlobal.num_versions)
                        {
                            query = "select filename,path from files f1 JOIN (SELECT Min(folder_version) AS min_id FROM files where username='"+username+"') f2 WHERE f1.username='"+username+"' and f1.folder_version  = f2.min_id and not exists (select 1 from files f3 where f1.filename = f3.filename and f1.path = f3.path and f3.folder_version>min_id)";
                            List<string>[] files_to_remove = db.Select(query, new List<string>[2],false);
                            remove_dict = files_to_remove[0].Zip(files_to_remove[1], (k, v) => new { k, v })
                      .ToDictionary(x => x.k, x => x.v);
                          

                            query = "delete f1 FROM files f1 JOIN (SELECT Min(folder_version) AS min_id FROM files where username='"+username+"') f2 WHERE f1.folder_version  = f2.min_id and f1.username='"+username+"'";
                            db.Delete(query,false);
                        }
                        int new_path = 0;


                        foreach (var file in sendlist)
                        {
                            //see which path i will have to assign him
                            string filename = file.Item1.Replace(@"\", @"\\").Replace("'", @"\'");
                            query = "select path from files where username='" + username + "'and filename='" + filename + "'";
                            List<string> paths = db.Select(query, new List<string>[1],false).ElementAt(0);
                            for (int i = 1; i <= MyGlobal.num_versions; i++)
                            {
                                if (!paths.Contains(i.ToString()))
                                {
                                    new_path = i;
                                    new_paths.Add(new_path.ToString());
                                    break;
                                }
                            }
                            //insert the new files into the db
                            query = "insert into files values('" + username + "','" + filename + "','" + file.Item2 + "','" + new_path.ToString() + "','" + (Convert.ToInt32(last_version) + 1).ToString() + "')";
                            db.Insert(query, false);
                        }
                        //store into the db the date of the last version. 
                        string creation_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
                        //store the date into the db cartelle. 
                        query = "insert into cartelle values('" + (Convert.ToInt32(last_version) + 1).ToString() + "','" + username + "','" + creation_time + "')";
                        db.Insert(query,false);
                    }

                    if (files_to_load == true)
                    {
                        if (sendlist.Count > 0)
                        {
                            //prepare to receive the new files from the client 
                            string json = JsonConvert.SerializeObject(sendlist.Select(t => t.Item1).ToList());
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
                            //now let's store these files somewhere
                            //according to the values stored in the db


                           
                            //then i will only have to delete what must be deleted and to move everything
                            if (Convert.ToInt32(last_version) >= MyGlobal.num_versions)
                            {
                                
                                //if we already have more than 3 folders store in tmp since it may happen 
                                //that all the folders already have a file with the same name and i cannot remove the previous 
                                //if i m not sure that i have finished the transaction
                                pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, "tmp");
                                if (!Directory.Exists(pathIntoServer))
                                {
                                    Directory.CreateDirectory(pathIntoServer);
                                }

                                foreach (var f in sendlist)
                                {
                                    
                                    pathIntoServer = Path.Combine(pathIntoServer, f.Item1);
                                    string folder = Path.GetDirectoryName(pathIntoServer);
                                    if (!Directory.Exists(folder))
                                    {
                                        //create the folder
                                        //TODO: undo of this operation in case of rollback
                                        DirectoryInfo u1 = Directory.CreateDirectory(folder);
                                    }
                                    receiveFile(pathIntoServer);
                                    msg = Encoding.ASCII.GetBytes("OK");
                                    s.Send(msg);
                                }
                               
                               
                            }
                            else
                            {
                                

                                
                                int i = 0;
                                foreach (var f in sendlist)
                                {
                                    pathIntoServer = Path.Combine(userFolderIntoServer, new_paths.ElementAt(i));
                                    pathIntoServer = Path.Combine(pathIntoServer, f.Item1);
                                    string folder = Path.GetDirectoryName(pathIntoServer);
                                    if (!Directory.Exists(folder))
                                    {
                                        //create the folder
                                        //TODO: undo of this operation in case of rollback
                                        DirectoryInfo u1 = Directory.CreateDirectory(folder);
                                        Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(folder));
                                        //at the very end store date into the db
                                        string creation_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
                                    }
                                    
                                    receiveFile(pathIntoServer);
                                    msg = Encoding.ASCII.GetBytes("OK");
                                    s.Send(msg);
                                    i = i + 1;
                                }

                            }

                        }


                    }
                    //nothing in sendlist
                    if (sendlist.Count == 0)
                    {
                        msg = Encoding.ASCII.GetBytes("END");
                        s.Send(msg);
                    }
                    tran.Commit();
                    db.CloseConnection();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    db.CloseConnection();
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("Eccezione in server..sincronizza files");
                }
                //now i can remove files 
                foreach (KeyValuePair<string, string> entry in remove_dict)
                {
                    string file = Path.Combine(entry.Value, entry.Key);
                    file = Path.Combine(userFolderIntoServer, file);
                    File.Delete(file);//TODO: put it into try catch
                }
                //and move the ones i saved in tmp
                int index = 0;
                foreach (var f in sendlist)//TODO: a check to execute this loop only if i used the tmp
                {
                    pathIntoServer = Path.Combine(userFolderIntoServer, new_paths.ElementAt(index));
                    pathIntoServer = Path.Combine(pathIntoServer, f.Item1);
                    string folder = Path.GetDirectoryName(pathIntoServer);
                    if (!Directory.Exists(folder))
                    {
                        DirectoryInfo u1 = Directory.CreateDirectory(folder);
                    }
                    //do the actualy cut-paste from tmp
                    string tmp=Path.Combine(userFolderIntoServer,"tmp");
                    tmp = Path.Combine(tmp, f.Item1);
                    System.IO.File.Move(tmp, pathIntoServer);
                    index = index + 1;
                }
                    //TODO: problem. i have to handle the view request here, or i will have a view request after 
                    //the synch. it may be executed before the commit so it will read wrong data
                    byte[] bytes = new Byte[1024];
                    //receive the folder to synch
                    int bytesRec = s.Receive(bytes);
                    string cmd = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    //verify the command
                    if (String.Compare(cmd.Substring(0, 2), "V:") == 0)
                    {
                        view_request();
                    }
                
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
            sendlist = new List<Tuple<string,string>>();
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
