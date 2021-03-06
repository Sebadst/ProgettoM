﻿using System;
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
using System.Windows.Forms;
namespace ProgettoPDS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// prova modifica
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.check_folders();
        }
        
        public MainWindow(string message)
        {
            InitializeComponent();
            this.message.Content = message;
            this.check_folders();
        }

        private void signup_Click(object sender, RoutedEventArgs e)
        {
            /*
             *called when signup button is clicked 
             */
            Signup_window signup_win = new Signup_window();        
            signup_win.Show();
            this.Close();
        }

        private void login_Click(object sender, RoutedEventArgs e)
        {
            /*
             * called when login button is clicked
             */
            message.Content = "";
            if (username.Text == "" || password.Password == "")
                message.Content = "Non lasciare campi vuoti";
            else
            {
                Client client = new Client(username.Text,password.Password);
                client.connect_to_server();                      
                int login = client.login(username.Text, password.Password);
                //login ok
                if (login == 1)
                {
                    //check if client already chose a folder
                    string folder_present = client.ask_presence(username.Text);
                    if (folder_present!="NULL")
                    {
                       //if yes, open the viewfolder window
                        ViewFolder view_f = new ViewFolder(client,folder_present);
                        view_f.Show();
                        this.Close();
                    }
                    else if(folder_present=="NULL")
                    {            
                        //if not, open the addfolder window
                        AddFolder add_f = new AddFolder(client);
                        add_f.Show();
                        this.Close();
                    }     
                    else
                    {
                        //connection error
                        message.Content = "Nessuna risposta dal server";
                    }
                }
                //wrong credentials
                else if (login == 0)
                {
                    message.Content = "Username o pwd errati";
                }
                else
                {
                    //connection error
                    message.Content = "Nessuna risposta dal server";
                }
            }   
        }


        //create the necessary folder defined on MyglobalClient.cs
        private void check_folders()
        {
            if (! System.IO.Directory.Exists(MyGlobalClient.downloadDirectory))
            {
                System.IO.Directory.CreateDirectory(MyGlobalClient.downloadDirectory);
            }

            if (!System.IO.Directory.Exists(MyGlobalClient.zipDirectory))
            {
                System.IO.Directory.CreateDirectory(MyGlobalClient.zipDirectory);
            }
        }
    }
}
