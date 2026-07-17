using AdvancedProgramming.Communs;
using AdvancedProgramming.Events;
using AdvancedProgramming.ViewModels;
using CarthaBotVPL.Services;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AdvancedProgramming.Views
{
    /// <summary>
    /// Interaction logic for AdvancedProgrammingView
    /// </summary>
    public partial class AdvancedProgrammingView : UserControl
    {
        private readonly IEventAggregator _eventAggregator;
        private bool mustUpdate = true;

        // Completion
        private CompletionWindow _completionWindow;



        public AdvancedProgrammingView(IEventAggregator eventAggregator, List<string> oldComs)
        {
            InitializeComponent();

            _eventAggregator = eventAggregator;

            if (DataContext is AdvancedProgrammingViewModel vm)
            {
                vm.Subscribe(_eventAggregator);
                vm.OldCom = oldComs;
                vm.ConnectMethod();
            }


        }

        /// <summary>
        /// Wireless overload: identical to the USB ctor, but the connection carrier (WiFi / BLE)
        /// is selected via Configure(...) BEFORE the link is opened. <paramref name="param"/> is the
        /// WiFi endpoint ("host:port") or the BLE device name.
        /// </summary>
        public AdvancedProgrammingView(IEventAggregator eventAggregator, List<string> oldComs,
                                       ConnectionMode mode, string param)
        {
            InitializeComponent();

            _eventAggregator = eventAggregator;

            if (DataContext is AdvancedProgrammingViewModel vm)
            {
                vm.Subscribe(_eventAggregator);
                vm.OldCom = oldComs;
                vm.Configure(mode, param);
                vm.ConnectMethod();
            }
        }

        private void CliOutputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            textBox?.ScrollToEnd();
        }

        private void editControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is AdvancedProgrammingViewModel vm)
            {
                vm.ExecuteEditLoaded(sender);
            }
        }

        private void editControl_TextChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {

            _eventAggregator.GetEvent<ScriptChangedEvent>().Publish((d as Syncfusion.Windows.Edit.EditControl));
        }
    }


}
