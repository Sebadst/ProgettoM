﻿#pragma checksum "..\..\ViewFolder.xaml" "{406ea660-64cf-4c82-b6f0-42d48172a799}" "8088AC6622DDABB1126919372BC50A7A"
//------------------------------------------------------------------------------
// <auto-generated>
//     Il codice è stato generato da uno strumento.
//     Versione runtime:4.0.30319.42000
//
//     Le modifiche apportate a questo file possono provocare un comportamento non corretto e andranno perse se
//     il codice viene rigenerato.
// </auto-generated>
//------------------------------------------------------------------------------

using ProgettoPDS;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using Xceed.Wpf.Toolkit;
using Xceed.Wpf.Toolkit.Chromes;
using Xceed.Wpf.Toolkit.Core.Converters;
using Xceed.Wpf.Toolkit.Core.Input;
using Xceed.Wpf.Toolkit.Core.Media;
using Xceed.Wpf.Toolkit.Core.Utilities;
using Xceed.Wpf.Toolkit.Panels;
using Xceed.Wpf.Toolkit.Primitives;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using Xceed.Wpf.Toolkit.PropertyGrid.Commands;
using Xceed.Wpf.Toolkit.PropertyGrid.Converters;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;
using Xceed.Wpf.Toolkit.Zoombox;


namespace ProgettoPDS {
    
    
    /// <summary>
    /// ViewFolder
    /// </summary>
    public partial class ViewFolder : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        
        #line 24 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button download_button;
        
        #line default
        #line hidden
        
        
        #line 25 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button choose_folder;
        
        #line default
        #line hidden
        
        
        #line 26 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBlock path;
        
        #line default
        #line hidden
        
        
        #line 27 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ProgressBar pbar;
        
        #line default
        #line hidden
        
        
        #line 28 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Label message;
        
        #line default
        #line hidden
        
        
        #line 30 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal Xceed.Wpf.Toolkit.IntegerUpDown interval;
        
        #line default
        #line hidden
        
        
        #line 31 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button interval_button;
        
        #line default
        #line hidden
        
        /// <summary>
        /// folders Name Field
        /// </summary>
        
        #line 34 "..\..\ViewFolder.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public System.Windows.Controls.TreeView folders;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/ProgettoPDS;component/viewfolder.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\ViewFolder.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.download_button = ((System.Windows.Controls.Button)(target));
            
            #line 24 "..\..\ViewFolder.xaml"
            this.download_button.Click += new System.Windows.RoutedEventHandler(this.download_Click);
            
            #line default
            #line hidden
            return;
            case 2:
            this.choose_folder = ((System.Windows.Controls.Button)(target));
            
            #line 25 "..\..\ViewFolder.xaml"
            this.choose_folder.Click += new System.Windows.RoutedEventHandler(this.choose_folder_Click);
            
            #line default
            #line hidden
            return;
            case 3:
            this.path = ((System.Windows.Controls.TextBlock)(target));
            return;
            case 4:
            this.pbar = ((System.Windows.Controls.ProgressBar)(target));
            return;
            case 5:
            this.message = ((System.Windows.Controls.Label)(target));
            return;
            case 6:
            this.interval = ((Xceed.Wpf.Toolkit.IntegerUpDown)(target));
            return;
            case 7:
            this.interval_button = ((System.Windows.Controls.Button)(target));
            
            #line 31 "..\..\ViewFolder.xaml"
            this.interval_button.Click += new System.Windows.RoutedEventHandler(this.set_interval);
            
            #line default
            #line hidden
            return;
            case 8:
            this.folders = ((System.Windows.Controls.TreeView)(target));
            return;
            }
            this._contentLoaded = true;
        }
    }
}

