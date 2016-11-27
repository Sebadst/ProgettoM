using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;

namespace ServerPDS
{
    class DBConnect
    {
        private MySqlConnection connection;
        private string server;
        private string database;
        private string uid;
        private string password;

        //Constructor
        public DBConnect()
        {
            Initialize();
        }

        //Initialize values
        private void Initialize()
        {
            server = "localhost";
            database = "progettom";
            uid = "root";
            //password = "password";
            string connectionString;
            connectionString = "SERVER=" + server + ";" + "DATABASE=" +
            database + ";" + "UID=" + uid + ";";// + "PASSWORD=" + password + ";";

            connection = new MySqlConnection(connectionString);
        }
        public MySqlConnection getConnection()
        {
            return this.connection;
        }
        //open connection to database
        public bool OpenConnection()
        {
            try
            {
                connection.Open();
                return true;
            }
            catch (MySqlException ex)
            {
                //When handling errors, you can your application's response based 
                //on the error number.
                //The two most common error numbers when connecting are as follows:
                //0: Cannot connect to server.
                //1045: Invalid user name and/or password.
                switch (ex.Number)
                {
                    case 0:
                        Console.WriteLine("Cannot connect to server.  Contact administrator");
                        break;

                    case 1045:
                        Console.WriteLine("Invalid username/password, please try again");
                        break;
                }
                return false;
            }

        }

        //Close connection
        public bool CloseConnection()
        {
            try
            {
                connection.Close();
                return true;
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

        }

        //Insert statement
        public void Insert(string qry,bool open_connect=true)
        {
            string query = qry;
            if (open_connect == true)
            {
                this.OpenConnection();
            }
                //create command and assign the query and connection from the constructor
                MySqlCommand cmd = new MySqlCommand(query, connection);

                //Execute command
                cmd.ExecuteNonQuery();

            if (open_connect ==true)
            {
                //close connection
                this.CloseConnection();
            }
            

        }

        //Update statement
        public void Update(string qry,bool open_connect=true)
        {
            string query = qry;

            //Open connection
            if (open_connect == true)
            {
                this.OpenConnection();
            }
            
                //create mysql command
                MySqlCommand cmd = new MySqlCommand();
                //Assign the query using CommandText
                cmd.CommandText = query;
                //Assign the connection using Connection
                cmd.Connection = connection;

                //Execute query
                cmd.ExecuteNonQuery();
            if (open_connect==true){
                //close connection
                this.CloseConnection();
            }
            
        }

        //Delete statement
        public void Delete(string qry,bool open_connect=true)
        {
            string query = qry;
            if (open_connect == true)
            {
                this.OpenConnection();
            }
 
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.ExecuteNonQuery();
                if (open_connect == true)
                {
                    this.CloseConnection();
                }
            
 
        }


        //Count statement
        public int Count(string qry,bool open_connect=true)
        {
            string query = qry;
            int Count = -1;

            if (open_connect == true)
            {
                this.OpenConnection();
            }
                //Create Mysql Command
                MySqlCommand cmd = new MySqlCommand(query, connection);

                //ExecuteScalar will return one value
                Count = int.Parse(cmd.ExecuteScalar() + "");

                //close Connection
            if(open_connect==true){
            this.CloseConnection();
            }

                return Count;
            
        }

        //Select statements

        public List<string>[] Select(string qry,List<string>[] container,bool open_connect=true)
        {
            string query = qry;

            if (open_connect == true)
            {
                this.OpenConnection();
            }
            
                //Create Command
                MySqlCommand cmd = new MySqlCommand(query, connection);
                //Create a data reader and Execute the command
                MySqlDataReader dataReader = cmd.ExecuteReader();

                //Read the data and store them in the list
                for (int i = 0; i < container.Length; i++)
                {
                    container[i]=new List<string>();
                }
                while (dataReader.Read())
                {
                        for(int i=0;i<container.Length;i++){
                            container[i].Add(dataReader[i] + "");
                        }
                }
                
                //close Data Reader
                dataReader.Close();
            if(open_connect==true)
                //close Connection
                this.CloseConnection();

                //return list to be displayed
                return container;
            
        }

    }
}
