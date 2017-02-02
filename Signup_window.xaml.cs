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

namespace ProgettoPDS
{
    /// <summary>
    /// Logica di interazione per signup_window.xaml
    /// </summary>
    public partial class Signup_window : Window
    {
        
        public Signup_window()
        {
            InitializeComponent();
        }

        private void Signup(object sender, RoutedEventArgs e)
        {
            /*
             * called when signup button is clicked
             */
            message.Content = "";
            if (username.Text == "" || password.Password == "" || repassword.Password == "")
                message.Content = "Non lasciare campi vuoti";
            else if (password.Password != repassword.Password)
                message.Content = "password diversa da ripeti password";
            else if (username.Text.Contains(".") || password.Password.Contains("."))
                message.Content = "I campi non possono contenere '.'";
            else if (username.Text.Contains("\\") || username.Text.Contains("/"))
                message.Content = "I campi non possono contenere char speciali";
            else if (username.Text.Length > 50)
                message.Content = "Dimensione massima username 50 char";
            else
            {
                message.Content = "Ok";
                //check if username already present in db
                Client client = new Client(username.Text, password.Password);
                client.connect_to_server();
                int signup = client.signup(username.Text, password.Password);
                if (signup == 1)
                {
                    //signup ok. open user main_window
                    MainWindow m = new MainWindow("Registrazione effettuata");
                    m.Show();
                    this.Close();
                }
                else if (signup == 0)
                {
                    //wrong credentials
                    message.Content = "L'utente e' gia' presente";
                }
                else
                {
                    //connection error
                    message.Content = "Nessuna risposta dal server";
                }
            }
        }
        private void Back(object sender, RoutedEventArgs e)
        {
            MainWindow m = new MainWindow();
            m.Show();
            this.Close();
        }
    }       
}
