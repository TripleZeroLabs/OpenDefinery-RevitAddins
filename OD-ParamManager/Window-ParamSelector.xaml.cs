﻿using OpenDefinery;
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

namespace OD_ParamManager
{
    /// <summary>
    /// Interaction logic for Window_ParamSelector.xaml
    /// </summary>
    public partial class Window_ParamSelector : Window
    {
        private Window_ParamManager ParamManager {  get; set; }
        public SharedParameter SelectedParameter { get; set; }
        public Window_ParamSelector(Window_ParamManager paramManager)
        {
            InitializeComponent();

            ParamManager = paramManager;

            Button_Confirm.IsEnabled = false;

            ComboBox_ParamSelector.ItemsSource = paramManager.Definery.PublishedCollections;
            ComboBox_ParamSelector.DisplayMemberPath = "Name";
        }

        private void ComboBox_ParamSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Button_Confirm.IsEnabled = false;

            var selectedCollection = ComboBox_ParamSelector.SelectedItem as Collection;
            var parameters = Collection.GetParameters(ParamManager.Definery, selectedCollection);

            ListBox_ParamSelector.ItemsSource = parameters;
            ListBox_ParamSelector.DisplayMemberPath = "Name";
        }

        private void Button_Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (ListBox_ParamSelector.SelectedItem != null)
            {
                SelectedParameter = ListBox_ParamSelector.SelectedItem as SharedParameter;
                DialogResult = true;
                Close();
            }
            else
            {
                DialogResult = false;
                MessageBox.Show("Select a parameter above.");
            }
        }

        private void ListBox_ParamSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBox_ParamSelector.SelectedItem != null)
            {
                Button_Confirm.IsEnabled = true;
            }
        }
    }
}
