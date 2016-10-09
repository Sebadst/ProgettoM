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
        //aggiunto da seba
        //TODO: change var name
        Dictionary<string, string> dictionary =new Dictionary<string, string>();
        Dictionary<string, string> file_hash = new Dictionary<string, string>();
        List<string> view_list = new List<string>();
        List<string> copylist = new List<string>();
        List<string> sendlist = new List<string>();


        public Logger(Socket sock, string username, string password, BlockingCollection<string> ac)
        {
            this.s = sock;
            this.password = password;
            this.username = username;
            //creo il thread passando la funzione come parametro
            t = new Thread(new ThreadStart(this.action));
            db = new DBConnect();
            this.ac = ac;
            t.Start();
        }

        public void GestoreClient() 
        {
            try
            {
                while (true)
                {
                    byte[] bytes = new Byte[1024];

                    //ricevo la cartalle da sincronizzare
                    int bytesRec = s.Receive(bytes);
                    string cmd = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                    //verifico il comando
                    if (String.Compare(cmd.Substring(0, 2), "S:") != 0 && String.Compare(cmd.Substring(0, 2), "A:") != 0 && String.Compare(cmd.Substring(0, 2), "V:") != 0 && String.Compare(cmd.Substring(0, 2), "D:") != 0)
                    {
                        byte[] msg = Encoding.ASCII.GetBytes("E.comando errato");
                        s.Send(msg);
                        s.Shutdown(SocketShutdown.Both);
                        throw new Exception("E.comando errato diverso da S:path");
                    }
                    //splitto la tringa
                    char[] delimiterChars = { ':' };
                    string[] words = cmd.Split(delimiterChars);
                    if (words.Length != 2)
                    {
                        byte[] msg = Encoding.ASCII.GetBytes("E.comando errato");
                        s.Send(msg);
                        s.Shutdown(SocketShutdown.Both);
                        throw new Exception("E.comando errato words.Leght!=3");
                    }


                    //TODO substitute with switch case. subsitute maybe all this with many classes, one per request   
                    //ask for presence of directory to know if client has to load viewfolder or addfolder
                    if (String.Compare(cmd.Substring(0, 2), "A:") == 0)
                    {
                        //recupero il nome della directory che l'utente vuole sincronizzare
                        //path to sync deve essere nella forma \cartella1\cartella2\ senza c:
                        //cioè il Path assoluto senza la "c:"


                        //recupero la root dellutente nel server
                        string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                        //modificato da seba
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
                    //view request
                    else if (String.Compare(cmd.Substring(0, 2), "V:") == 0)
                    {
                        //recupero la root dellutente nel server
                        string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                        //modificato da seba
                        //TODO fare con un for, questo e tutto il resto
                        string pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, "1");
                        string json = null;

                        if (Directory.Exists(pathIntoServer))
                        {
                            //start constructing the json
                            view_list.Clear();
                            //TODO aggiungi data e ora della cartella
                            view_list.Add(Directory.GetLastWriteTime(pathIntoServer).ToString("yyyyMMdd-HHmm"));
                            browse_folder_list_version(pathIntoServer, view_list);
                            //add to json
                            //json = JsonConvert.SerializeObject(view_dictionary);

                        }
                        pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, "2");
                        if (Directory.Exists(pathIntoServer))
                        {
                            //continue constructing the json
                            //view_dictionary.Clear();
                            //TODO aggiungi data e ora della cartella
                            view_list.Add(Directory.GetLastWriteTime(pathIntoServer).ToString("yyyyMMdd-HHmm"));

                            browse_folder_list_version(pathIntoServer, view_list);
                            //add to json
                            //json += JsonConvert.SerializeObject(view_dictionary);
                        }
                        pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, "3");
                        if (Directory.Exists(pathIntoServer))
                        {
                            //finish constructing the json
                            //view_dictionary.Clear();
                            //TODO aggiungi data e ora della cartella
                            view_list.Add(Directory.GetLastWriteTime(pathIntoServer).ToString("yyyyMMdd-HHmm"));

                            browse_folder_list_version(pathIntoServer, view_list);
                            //add to json
                            //json += JsonConvert.SerializeObject(view_dictionary);
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
                        //out from the loop after V request
                        break;
                    }
                    //synchronize file or directory
                    else if (String.Compare(cmd.Substring(0, 2), "S:") == 0)
                    {
                        //recupero il nome della directory che l'utente vuole sincronizzare
                        //path to sync deve essere nella forma \cartella1\cartella2\ senza c:
                        //cioè il Path assoluto senza la "c:"
                        string pathIntoClient = words[1];

                        //recupero la root dellutente nel server
                        string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                        //modificato da seba
                        userFolderIntoServer = System.IO.Path.Combine(userFolderIntoServer, "1");
                        //cerco la cartella //DA CAMBIARE
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
                        //out of the loop with S request
                        break;
                    }
                    else if (String.Compare(cmd.Substring(0, 2), "D:") == 0)
                    {
                        //recupero la root dellutente nel server
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
                            //TODO change it like for the others, maybe with the throw
                            byte[] msg = Encoding.ASCII.GetBytes("E. not present");
                            s.Send(msg);

                        }
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


        //modificato da seba
        public void sincronizzaDirectory(string ufis, string pic, string pis)
        {
            try
            {
                
                //string pisu1 = System.IO.Path.Combine(MyGlobal.rootFolder, "u1");
                //modificato da seba
                string escapedPath = pic.Replace(@"\", @"\\").Replace("'", @"\'");
                string query = "update utenti set folder='"+escapedPath+"' where username='"+ username + "'";
                db.Update(query);
                
                //invio OK
                byte[] msg = Encoding.ASCII.GetBytes("OK");
                s.Send(msg);

                //ricevo il file.zip
                //vediamo dove lo salva
                FileStream fStream = new FileStream("u1.zip", FileMode.Create);

                // read the file in chunks of 1KB
                var buffer = new byte[1024];
                int bytesRead;

                //leggo la lunghezza
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
                //creo la prima cartella di upload per il client
                DirectoryInfo u1 = Directory.CreateDirectory(System.IO.Path.Combine("1", pis));
                Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(pis));

                //estraggo
                //modificato da seba
                ZipFile.ExtractToDirectory("u1.zip", pis );
                Console.WriteLine("file estratto");

                //invio un ultimo ok al client
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
        private Tuple<bool, string> check_3rd(string userFolderIntoServer, string pathIntoClient, string pathIntoServer, string pathString, bool flag)
        {
            browse_folder(System.IO.Path.Combine(userFolderIntoServer, "3"),dictionary);
            //un file "percorso(radice la directory iniziale) hash" uno per riga

            //System.IO.StreamReader myfile = new System.IO.StreamReader(filePath);
            foreach (var line in file_hash)
            {
                string key = line.Key;
                string value = line.Value;
                if (dictionary.ContainsKey("3\\" + key))
                {
                    if (value == dictionary["3\\" + key])
                    {
                        //because the folder 3 will become the 2, so I will put 2
                        copylist.Add("2\\" + key);
                    }
                    else
                    {
                        sendlist.Add(key);
                    }
                    //i remove to do the check on files deletede afterwards
                    dictionary.Remove("3\\" + key);
                }
                else
                {
                    sendlist.Add(key);
                }
                
            }
            //if deleted files remain then count>0
           
            if (dictionary.Count>0 || sendlist.Count != 0)
            {

                flag = true;
                //cancella la 1
                pathIntoClient = "1";


                //cerco la cartella
                pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
                string pathIntoClient2 = "2";
                //recupero la root dellutente nel server
                string userFolderIntoServer2 = System.IO.Path.Combine(MyGlobal.rootFolder, username);

                //cerco la cartella
                string pathIntoServer2 = System.IO.Path.Combine(userFolderIntoServer2, pathIntoClient2);
                string pathIntoClient3 = "3";
                //recupero la root dellutente nel server
                string userFolderIntoServer3 = System.IO.Path.Combine(MyGlobal.rootFolder, username);

                //cerco la cartella
                string pathIntoServer3 = System.IO.Path.Combine(userFolderIntoServer3, pathIntoClient3);

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
                //Directory.Move(pathIntoServer2, pathIntoServer);
                DeleteDirectory(pathIntoServer2);
                Directory.CreateDirectory(pathIntoServer2);
                //la 3 diventa 2
                //Directory.Move(pathIntoServer3, pathIntoServer2);
                foreach (string dirPath in Directory.GetDirectories(pathIntoServer3, "*",
SearchOption.AllDirectories))
                    Directory.CreateDirectory(dirPath.Replace(pathIntoServer3, pathIntoServer2));

                //Copy all the files & Replaces any files with the same name
                foreach (string newPath in Directory.GetFiles(pathIntoServer3, "*.*",
                    SearchOption.AllDirectories))
                    File.Copy(newPath, newPath.Replace(pathIntoServer3, pathIntoServer2), true);
                DeleteDirectory(pathIntoServer3);
                //chiamo questa 3
                pathIntoClient = "3";
                //recupero la root dellutente nel server
                userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);

                //cerco la cartella
                pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);

                pathString = pathIntoServer2;
            }
            return Tuple.Create(flag, pathIntoServer); 
        }

        private Tuple<bool, string> check_2nd(string userFolderIntoServer,string pathIntoClient,string pathIntoServer,string pathString,bool flag)
        {
            browse_folder(System.IO.Path.Combine(userFolderIntoServer, "2"),dictionary);
            //un file "percorso(radice la directory iniziale) hash" uno per riga

            //System.IO.StreamReader myfile = new System.IO.StreamReader(filePath);
            foreach (var line in file_hash)
            {
                string key = line.Key;
                string value = line.Value;


                if (dictionary.ContainsKey("2\\" + key))
                {
                    if (value == dictionary["2\\" + key])
                    {
                        copylist.Add("2\\" + key);
                    }
                    else
                    {
                        sendlist.Add(key);
                    }
                }
                else
                {
                    sendlist.Add(key);
                }
                //i remove to do the check on files deletede afterwards
                dictionary.Remove("2\\" + key);
            }
            
            //if deleted files remain then count>0

            if (dictionary.Count > 0 || sendlist.Count != 0)
            {

                flag = true;
                //questa diventa 3
                pathIntoClient = "3";

                //cerco la cartella
                pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
                string pathIntoClient2 = "2";
                //recupero la root dellutente nel server
                string userFolderIntoServer2 = System.IO.Path.Combine(MyGlobal.rootFolder, username);

                //cerco la cartella
                string pathIntoServer2 = System.IO.Path.Combine(userFolderIntoServer2, pathIntoClient2);
                pathString = pathIntoServer2;
            }
            return Tuple.Create(flag, pathIntoServer); 
        }
        private Tuple<bool, string> check_1st(string userFolderIntoServer,string pathIntoClient,string pathIntoServer,string pathString,bool flag)
        {
            browse_folder(System.IO.Path.Combine(userFolderIntoServer, "1"),dictionary);
            //un file "percorso(radice la directory iniziale) hash" uno per riga

            //System.IO.StreamReader myfile = new System.IO.StreamReader(filePath);
            foreach (var line in file_hash)
            {
                string key = line.Key;
                string value = line.Value;


                if (dictionary.ContainsKey("1\\" + key))
                {
                    if (value == dictionary["1\\" + key])
                    {
                        copylist.Add("1\\" + key);
                    }
                    else
                    {
                        sendlist.Add(key);
                    }
                }
                else
                {
                    sendlist.Add(key);
                }
                //i remove to do the check on files deletede afterwards
                dictionary.Remove("1\\" + key);
            }

            //if deleted files remain then count>0

            if (dictionary.Count > 0 || sendlist.Count != 0)
            {
                flag = true;
                //quello che ho fatto fin'ora
                pathIntoClient = "2";

                //cerco la cartella
                pathIntoServer = System.IO.Path.Combine(userFolderIntoServer, pathIntoClient);
                pathString = System.IO.Path.Combine(MyGlobal.rootFolder, username);
            }
            return Tuple.Create(flag, pathIntoServer); 
        }
        //modificato da seba
        public void sincronizzaFiles(string path)
        {
            try
            {
                //TODO: dare un nome utile al flag
                bool flag = false;

                Console.WriteLine("Sincronizzazione file. Invio ok!");
                //invio OK
                byte[] msg = Encoding.ASCII.GetBytes("OK");
                s.Send(msg);

                
                //ricevo la lista dei file:
                //receiveFile(filePath);
                //in json:
                receiveFile_json();
               
                
                string pathString=null;
                //fare un check sull'ultima cartella
                string pathIntoClient=null;
                //recupero la root dellutente nel server
                string userFolderIntoServer = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                string pathIntoServer = null; //=System.IO.Path.Combine(MyGlobal.rootFolder, username);
                
                if (Directory.Exists(System.IO.Path.Combine(userFolderIntoServer, "3")))
                {
                    Tuple<bool,string> i=check_3rd(userFolderIntoServer,pathIntoClient,pathIntoServer,pathString,flag);
                    flag = i.Item1;
                    pathIntoServer = i.Item2;
                }
                else if (Directory.Exists(System.IO.Path.Combine(userFolderIntoServer, "2")))
                {
                    Tuple<bool, string> i = check_2nd(userFolderIntoServer, pathIntoClient, pathIntoServer, pathString, flag);
                    flag = i.Item1;
                    pathIntoServer = i.Item2;
                }
                else
                {
                    Tuple<bool, string> i = check_1st(userFolderIntoServer, pathIntoClient, pathIntoServer, pathString, flag);
                    flag = i.Item1;
                    pathIntoServer = i.Item2;
                }


                // Read the file and display it line by line.
                //TODO: now it should be ok with flag used in this way (without the else commented
                //below but with the if sendlist==0) for the deleted files. check it some other times to be sure
                if (flag == true)
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
                   /* //TODO: check if this breaks something. it is for deleted files
                    if (dictionary.Count>0)
                    {

                    }*/
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
                }
                //else
                //{
                    //nothing in sendlist
                if (sendlist.Count == 0)
                {
                    msg = Encoding.ASCII.GetBytes("END");
                    s.Send(msg);
                }
                //}
            }
           
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Eccezione in server..sincronizza files");
            }
        }

        public void copyFile(string sourcePath,string targetPath, string fileName)
        {
         
           

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

            // To copy all the files in one directory to another directory.
            // Get the files in the source folder. (To recursively iterate through
            // all subfolders under the current directory, see
            // "How to: Iterate Through a Directory Tree.")
            // Note: Check for target path was performed previously
            //       in this code example.
            //useless
            /*
            if (System.IO.Directory.Exists(sourcePath))
            {
                string[] files = System.IO.Directory.GetFiles(sourcePath);

                // Copy the files and overwrite destination files if they already exist.
                foreach (string s in files)
                {
                    // Use static Path methods to extract only the file name from the path.
                    fileName = System.IO.Path.GetFileName(s);
                    destFile = System.IO.Path.Combine(targetPath, fileName);
                    System.IO.File.Copy(s, destFile, true);
                }
            }
            else
            {
                Console.WriteLine("Source path does not exist!");
            }
            */
        
        }

        public void receiveFile_json()
        {
            //TODO: 15 MB should be enough for just the list of files but careful because it should be corrected probably sending the dimension and dimensioning the buffer in that way, like for the normal files
            //TODO: fix synchronize that doesn t work yet, for instance with the eurecom folder i put in desktop-sebadst
            byte[] rcv = new byte[15728640];
            
            int byteCount = s.Receive(rcv, SocketFlags.None);
            string rx = (string)Encoding.UTF8.GetString(rcv).Clone();
            file_hash = JsonConvert.DeserializeObject<Dictionary<string,string>>(rx);
        }

        public void receiveFile(string fileName)
        {
            try
            {
                FileStream fStream = new FileStream(fileName, FileMode.Create);

                // read the file in chunks of 1MB
                var buffer = new byte[1028576];
                int bytesRead;

                //leggo la lunghezza
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

        //aggiunto da seba
        public string compute_md5(string filename)
        {
            var md5 = MD5.Create();

            FileStream stream = File.OpenRead(filename);
            
            string k=BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
            stream.Flush();
            stream.Close();
            return k;

        }
        //aggiunto da seba
        //I added the parameter dictionary, check if works
        public void browse_folder(string filename,Dictionary<string,string> dictionary)
        {
            DirectoryInfo d = new DirectoryInfo(filename);
            foreach (var dir in d.GetDirectories())
                browse_folder(dir.FullName,dictionary);
            foreach (var file in d.GetFiles())
            {
                Console.WriteLine(file.FullName);
                string hash = compute_md5(file.FullName);

                //to delete the first part of the path
                string namefile = file.FullName.Substring(MyGlobal.rootFolder.Length + 1+username.Length+1); 

                dictionary.Add(namefile, hash);
               
            }


        }
        //TODO: fix the problem that i send only the files while i should send also folder in order to display empty folder in the client
        //or maybe i can leave it in this way since i will not have to download empty folders but only files (or folders with something)
        //see at the very end
        public void browse_folder_list_version(string filename,List<string> list)
        {
            DirectoryInfo d = new DirectoryInfo(filename);
            //doing this before than iterating on folders permits to have everything already ordered
            foreach (var file in d.GetFiles())
            {
                Console.WriteLine(file.FullName);
                string hash = compute_md5(file.FullName);
                //to delete the first part of the path
                // +1 to remove the //
                string namefile = file.FullName.Substring(MyGlobal.rootFolder.Length+1+username.Length+1); 
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
            dictionary =new Dictionary<string, string>();
            file_hash = new Dictionary<string, string>();
            copylist = new List<string>();
            sendlist = new List<string>();
        }
        public void wrap_send_directory(string directory)
        {

            //  ZIP THE FILE
            string startPath = directory;
            //TODO change the path
            string zipPath = MyGlobal.rootFolder+"\\"+username+".zip";
            // string extractPath = @"C:\Users\sds\Desktop\progetto";
            ZipFile.CreateFromDirectory(startPath, zipPath);
            wrap_send_file(zipPath);
            //TODO delete the zip from the server
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
                
                if (response>0)
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
