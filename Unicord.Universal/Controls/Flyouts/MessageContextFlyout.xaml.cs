﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unicord.Universal.Controls.Messages;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unicord.Universal.Controls.Flyouts
{
    public sealed partial class MessageContextFlyout : MenuFlyout
    {
        public MessageControl Parent
        {
            get { return (MessageControl)GetValue(ParentProperty); }
            set { SetValue(ParentProperty, value); }
        }
        
        public static readonly DependencyProperty ParentProperty =
            DependencyProperty.Register("Parent", typeof(MessageControl), typeof(MessageContextFlyout), new PropertyMetadata(null));

        public MessageContextFlyout()
        {
            this.InitializeComponent();
        }

        // todo: is there a less shit way of doing this?
        private void EditFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            this.Target.FindParent<MessageControl>().BeginEdit();
        }
    }
}
