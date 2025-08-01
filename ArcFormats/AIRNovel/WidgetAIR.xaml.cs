//! \brief      Code-behind for INT encryption query widget.
//
// Copyright (C) 2014-2015 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using GameRes.Formats.AirNovel;

namespace GameRes.Formats.GUI
{
    /// <summary>
    /// Interaction logic for WidgetAIR.xaml
    /// </summary>
    public partial class WidgetAIR : Grid
    {
        public WidgetAIR()
        {
            InitializeComponent();
            LoadDictionary();
        }

        private void LoadDictionary()
        {
            // Get the KnownKeys dictionary
            var knownKeys = AirOpener.DefaultScheme.KnownKeys;

            // Populate the combobox with keys
            KeyComboBox.ItemsSource = knownKeys.Keys;

            // Select first item if available
            if (KeyComboBox.Items.Count > 0)
            {
                KeyComboBox.SelectedIndex = 0;
            }
        }

        private void KeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (KeyComboBox.SelectedItem != null)
            {
                string selectedKey = KeyComboBox.SelectedItem.ToString();
                if (AirOpener.DefaultScheme.KnownKeys.TryGetValue(selectedKey, out string value))
                {
                    ValueTextBox.Text = value;
                }
                else
                {
                    ValueTextBox.Text = "Value not found";
                }
            }
        }
    }
}
