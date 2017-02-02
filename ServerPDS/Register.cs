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
using System.Security.Cryptography;
namespace ServerPDS
{
    class Register
    {
        private string password;
        private string username;
        private string pathToSync;
        private Socket s;
        Thread t;
        DBConnect db;

        public Register(Socket s, string username, string password)
        {
            this.s = s;
            this.password = password;
            this.username = username;
            //this.pathToSync = pathToSync;
            //creo il thread passando la funzione come parametro
            t = new Thread(new ThreadStart(this.action));
            db = new DBConnect();
            t.Start();
        }
        //penso che vada bene sha1 per questo progetto..altrimenti bcrypt o roba del genere ma diventa lento
        public static string HashPassword(string password)
        {
            var provider = new SHA1CryptoServiceProvider();
            var encoding = new UnicodeEncoding();
            var encrypted_pwd= provider.ComputeHash(encoding.GetBytes(password));
            //converto in questo modo per ottenere esadecimali (sha1dovrebbe tornare esamedicali) con -
            string delimitedHexHash = BitConverter.ToString(encrypted_pwd);
            string result = delimitedHexHash.Replace("-", "");
            return result;
        }
        private void action()
        {
            if(username.Length > 50 || username.Contains("\\") || username.Contains("/") || username.Contains(" "))
            {
                s.Shutdown(SocketShutdown.Both);
                s.Close();
                Console.WriteLine("Username non valido");
                return;
            }
            string query = "select count(*) from utenti where username = " + "'" + username + "'";
            if (db.Count(query) <= 0)
            {

                query = "insert into utenti(username, password,folder) values('" + username + "'" + "," + "'" + HashPassword(password) + "'" + ","+"'" + " " +"'" +")";
                db.Insert(query);
                byte[] msg = Encoding.ASCII.GetBytes("OK");
                s.Send(msg);
                s.Shutdown(SocketShutdown.Both);
                s.Close();
                Console.WriteLine("utente inserito");
                //create folder for user
                string pathString = System.IO.Path.Combine(MyGlobal.rootFolder, username);
                try
                {
                    // Determine whether the directory exists.
                    if (Directory.Exists(pathString))
                    {
                        Console.WriteLine("That path exists already. CONTROLLARE IL CODICE QUI CLASSE REGISTER");
                        //return;
                    }
                    // Try to create the directory.
                    DirectoryInfo di = Directory.CreateDirectory(pathString);
                    Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(pathString));
                }
                catch (Exception e)
                {
                    Console.WriteLine("The process failed: {0}", e.ToString());
                }
            }
            else
            {
                byte[] msg = Encoding.ASCII.GetBytes("E.utenteEsistente/erroreInserimento");
                s.Send(msg);
                s.Shutdown(SocketShutdown.Both);
                s.Close();
                Console.WriteLine("E.utenteEsistente/erroreInserimento");
            }
        }
    }
}
