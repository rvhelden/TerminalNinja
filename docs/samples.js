/**
 * samples.js — XAML sample snippets for the TerminalNinja playground.
 *
 * Each sample is a self-contained Window (no code-behind / no DataContext)
 * so it can render in the WASM module without a ViewModel.
 */

export const SAMPLES = [
  {
    id: "progressbar",
    title: "ProgressBar",
    description: "Determinate, indeterminate, and custom-character progress bars.",
    docPage: "./samples/progressbar.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Progress Bars">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Progress Bars" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Determinate (60%, with percentage):" Padding="2,0,0,0" />
            <ProgressBar StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                         Value="60" ShowPercentage="True" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Indeterminate:" Padding="2,0,0,0" />
            <ProgressBar StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                         IsIndeterminate="True" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Custom characters (25%):" Padding="2,0,0,0" />
            <ProgressBar StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                         Value="25" BarCharacter="=" TrackCharacter="-" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Default characters (75%):" Padding="2,0,0,0" />
            <ProgressBar StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                         Value="75" />

            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "button",
    title: "Button",
    description: "Button controls with styling, hover colors, and tab navigation.",
    docPage: "./samples/button.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Buttons">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Buttons and Commands" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="2"
                       Text="  Buttons with ICommand binding, hover colors, and keyboard focus."
                       Padding="2,0,0,0" VerticalTextAlignment="Center" />

            <StackPanel StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" Orientation="Horizontal">
                <Button StackPanel.SizeMode="Auto" Text="New" Width="12" Height="3" TabIndex="0" />
                <Button StackPanel.SizeMode="Auto" Text="Open" Width="12" Height="3" TabIndex="1" />
                <Button StackPanel.SizeMode="Auto" Text="Save" Width="12" Height="3" TabIndex="2" />
                <Button StackPanel.SizeMode="Auto" Text="Delete" Width="12" Height="3" TabIndex="3"
                        HoverColor="Red" />
                <Button StackPanel.SizeMode="Auto" Text="GC Collect" Width="16" Height="3" TabIndex="4" />
            </StackPanel>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Last action: New clicked (total: 3)" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "data-binding",
    title: "Data Binding",
    description: "One-way and two-way binding, converters, and animated colors.",
    docPage: "./samples/data-binding.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Data Binding">
    <Border BorderStyle="Rounded">
        <Grid Rows="1 2 3 1 * 1 *" Columns="* *">
            <TextBlock Grid.Row="0" Grid.ColumnSpan="2"
                       Text=" Data Binding" Padding="2,0,0,0" />

            <TextBlock Grid.Row="1" Grid.ColumnSpan="2"
                       Text="  One-way binding, two-way binding, and converters."
                       Padding="2,0,0,0" VerticalTextAlignment="Center" />

            <StackPanel Grid.Row="2" Grid.ColumnSpan="2" Orientation="Horizontal">
                <Button StackPanel.SizeMode="Auto" Text="Update Header" Width="20" Height="3" TabIndex="0" />
                <Button StackPanel.SizeMode="Auto" Text="Update Content" Width="20" Height="3" TabIndex="1" />
            </StackPanel>

            <Border Grid.Row="3" Grid.ColumnSpan="2" BorderStyle="Single">
                <TextBlock Text="Updated at 14:32:05 (#3)" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
            </Border>

            <Border Grid.Row="4" Grid.Column="0" BorderStyle="Rounded">
                <StackPanel Orientation="Vertical">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text=" Bound Content" Padding="1,0,0,0" />
                    <TextBlock StackPanel.SizeMode="Stretch"
                               Text="Content updated!\\nClick count: 3\\nTime: 14:32:05" Padding="2,0,2,0"
                               TextWrapping="Wrap" />
                </StackPanel>
            </Border>

            <Border Grid.Row="4" Grid.Column="1" Grid.RowSpan="2" BorderStyle="Rounded">
                <StackPanel Orientation="Vertical">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text=" Two-Way Selection" Padding="1,0,0,0" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text="  Selected: Converter" Padding="1,0,0,0" />
                    <ListBox StackPanel.SizeMode="Stretch" TabIndex="2">
                        <ListBoxItem>One-Way</ListBoxItem>
                        <ListBoxItem>Two-Way</ListBoxItem>
                        <ListBoxItem>Converter</ListBoxItem>
                        <ListBoxItem>RelativeSource</ListBoxItem>
                    </ListBox>
                </StackPanel>
            </Border>

            <TextBlock Grid.Row="6" Grid.ColumnSpan="2" Text="" />
        </Grid>
    </Border>
</Window>`
  },

  {
    id: "dialogs",
    title: "Dialogs",
    description: "Modal dialog with OK/Cancel, dimmed background, and dialog result.",
    docPage: "./samples/dialogs.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Dialogs">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Dialogs" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="2"
                       Text="  Modal dialogs with OK/Cancel buttons and dimmed background."
                       Padding="2,0,0,0" VerticalTextAlignment="Center" />

            <StackPanel StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" Orientation="Horizontal">
                <Button StackPanel.SizeMode="Auto" Text="Show Dialog" Width="20" Height="3" TabIndex="0" />
            </StackPanel>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Dialog #2: Confirmed" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <!-- Simulated dialog preview -->
            <Border StackPanel.SizeMode="Stretch" BorderStyle="Rounded">
                <StackPanel Orientation="Vertical">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text=" Confirm Action" />
                    <TextBlock StackPanel.SizeMode="Stretch"
                               Text="Are you sure you want to proceed?\\n\\nThis is a modal dialog demo."
                               HorizontalTextAlignment="Center" VerticalTextAlignment="Center"
                               TextWrapping="Wrap" Padding="2,1,2,1" />
                    <StackPanel StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" Orientation="Horizontal">
                        <Button StackPanel.SizeMode="Auto" Text="OK" Width="12" Height="3"
                                HoverColor="Green" TabIndex="1" />
                        <Button StackPanel.SizeMode="Auto" Text="Cancel" Width="12" Height="3"
                                HoverColor="Red" TabIndex="2" />
                    </StackPanel>
                </StackPanel>
            </Border>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "listbox",
    title: "ListBox",
    description: "ListBox with selection, ObservableCollection, and add/remove items.",
    docPage: "./samples/listbox.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Lists">
    <Border BorderStyle="Rounded">
        <Grid Rows="1 2 3 1 *" Columns="* *">
            <TextBlock Grid.Row="0" Grid.ColumnSpan="2"
                       Text=" Lists and Collections" Padding="2,0,0,0" />

            <TextBlock Grid.Row="1" Grid.ColumnSpan="2"
                       Text="  ListBox with selection, ObservableCollection, and add/remove."
                       Padding="2,0,0,0" VerticalTextAlignment="Center" />

            <StackPanel Grid.Row="2" Grid.ColumnSpan="2" Orientation="Horizontal">
                <Button StackPanel.SizeMode="Auto" Text="Add Item" Width="16" Height="3" TabIndex="0" />
                <Button StackPanel.SizeMode="Auto" Text="Remove" Width="16" Height="3" TabIndex="1" />
                <TextBlock StackPanel.SizeMode="Stretch"
                           Text="  Selected: Settings"
                           VerticalTextAlignment="Center" Padding="2,0,0,0" />
            </StackPanel>

            <TextBlock Grid.Row="3" Grid.Column="0"
                       Text="  Selected: Settings" Padding="2,0,0,0" />

            <Border Grid.Row="4" Grid.Column="0" BorderStyle="Rounded">
                <ListBox TabIndex="2">
                    <ListBoxItem>Dashboard</ListBoxItem>
                    <ListBoxItem>Messages</ListBoxItem>
                    <ListBoxItem>Settings</ListBoxItem>
                    <ListBoxItem>Profile</ListBoxItem>
                    <ListBoxItem>Help</ListBoxItem>
                </ListBox>
            </Border>

            <Border Grid.Row="3" Grid.Column="1" Grid.RowSpan="2" BorderStyle="Rounded">
                <StackPanel Orientation="Vertical">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text=" Activity Log" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text="  14:30:01: Lists sample opened" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text="  14:30:05: Added 'Item 1'" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text="  14:30:08: Removed 'Help'" />
                    <TextBlock StackPanel.SizeMode="Stretch" Text="" />
                </StackPanel>
            </Border>
        </Grid>
    </Border>
</Window>`
  },

  {
    id: "grid-layout",
    title: "Grid Layout",
    description: "Rows, columns, star/fixed sizing, and row/column spans.",
    docPage: "./samples/grid-layout.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Grid Layout">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Grid Layout" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Rows and columns with star, fixed, and auto sizing."
                       Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <Border StackPanel.SizeMode="Stretch" BorderStyle="Single">
                <Grid Rows="3 * *" Columns="20 * *">
                    <Border Grid.Row="0" Grid.ColumnSpan="3" BorderStyle="Single">
                        <TextBlock Text="Row=0 ColSpan=3 (Fixed 3)" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
                    </Border>

                    <Border Grid.Row="1" Grid.Column="0" BorderStyle="Rounded">
                        <TextBlock Text="R1 C0 (20w)" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
                    </Border>

                    <Border Grid.Row="1" Grid.Column="1" BorderStyle="Rounded">
                        <TextBlock Text="R1 C1 (*)" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
                    </Border>

                    <Border Grid.Row="1" Grid.Column="2" Grid.RowSpan="2" BorderStyle="Rounded" Background="#00005F">
                        <TextBlock Text="R1 C2 RowSpan=2 (*)" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
                    </Border>

                    <Border Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2" BorderStyle="Rounded" Background="#005F00">
                        <TextBlock Text="R2 C0 ColSpan=2" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
                    </Border>
                </Grid>
            </Border>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "stack-layout",
    title: "StackPanel Layout",
    description: "Vertical/horizontal stacking with Auto, Fixed, and Stretch sizing.",
    docPage: "./samples/stack-layout.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="StackPanel Layout">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" StackPanel Layout" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Vertical and horizontal stacking with Auto, Fixed, and Stretch sizing."
                       Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Vertical StackPanel:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Fixed" StackPanel.FixedSize="6" BorderStyle="Single">
                <StackPanel Orientation="Vertical">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text="  Fixed (1 row)" Background="#5F0000" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="2"
                               Text="  Fixed (2 rows)" Background="#00005F" VerticalTextAlignment="Center" />
                    <TextBlock StackPanel.SizeMode="Stretch"
                               Text="  Stretch (fills remaining)" Background="#005F00" VerticalTextAlignment="Center" />
                </StackPanel>
            </Border>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Horizontal StackPanel:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" BorderStyle="Single">
                <StackPanel Orientation="Horizontal">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="15"
                               Text=" Fixed (15)" Background="#5F0000" VerticalTextAlignment="Center" />
                    <TextBlock StackPanel.SizeMode="Auto"
                               Text=" Auto " Background="#00005F" VerticalTextAlignment="Center" />
                    <TextBlock StackPanel.SizeMode="Stretch"
                               Text=" Stretch" Background="#005F00" VerticalTextAlignment="Center" />
                    <TextBlock StackPanel.SizeMode="Stretch"
                               Text=" Stretch" Background="#5F005F" VerticalTextAlignment="Center" />
                </StackPanel>
            </Border>

            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "scroll-viewer",
    title: "ScrollViewer",
    description: "Scrollable viewport with keyboard and mouse wheel navigation.",
    docPage: "./samples/scroll-viewer.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="ScrollViewer">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" ScrollViewer" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Scrollable viewport with keyboard and mouse wheel."
                       Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <ScrollViewer StackPanel.SizeMode="Stretch" VerticalScrollBarVisibility="Auto">
                <StackPanel Orientation="Vertical">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   1. Welcome to ScrollViewer!" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   2. This content exceeds the viewport." />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   3. Use arrow keys to scroll." />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   4. Page Up/Down scroll by page." />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   5. Home/End jump to top/bottom." />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   6. The indicator shows position." />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   7. Features:" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   8. - Vertical/horizontal scrolling" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="   9. - Auto/Visible/Hidden/Disabled" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="  10. - Mouse wheel (3 lines/tick)" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="  11. - ScrollIntoView API" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="  12. End of content." />
                </StackPanel>
            </ScrollViewer>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "text-box",
    title: "TextBox",
    description: "Editable text input with caret, selection, and placeholder.",
    docPage: "./samples/text-box.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="TextBox">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Text Input" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="2"
                       Text="  TextBox controls with editing, selection, and placeholder."
                       Padding="2,0,0,0" VerticalTextAlignment="Center" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Single-line input:" Padding="2,0,0,0" />
            <TextBox StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3"
                     PlaceholderText="Type something here..." TabIndex="0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Read-only:" Padding="2,0,0,0" />
            <TextBox StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3"
                     Text="This text cannot be edited" IsReadOnly="True" TabIndex="1" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Multi-line (Enter for new line):" Padding="2,0,0,0" />
            <TextBox StackPanel.SizeMode="Stretch" AcceptsReturn="True"
                     PlaceholderText="Enter notes here..." TabIndex="2" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "checkbox",
    title: "CheckBox",
    description: "Toggle control with [x] / [ ] indicator and content label.",
    docPage: "./samples/checkbox.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="CheckBox">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" CheckBox" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <CheckBox StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Content="Enable notifications" IsChecked="True" TabIndex="0" />
            <CheckBox StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Content="Dark mode" IsChecked="True" TabIndex="1" />
            <CheckBox StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Content="Auto-save" TabIndex="2" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "radiobutton",
    title: "RadioButton",
    description: "Mutually exclusive option with (*) / ( ) indicator and GroupName.",
    docPage: "./samples/radiobutton.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="RadioButton">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" RadioButton" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <RadioButton StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" GroupName="Size" Content="Small" TabIndex="0" />
            <RadioButton StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" GroupName="Size" Content="Medium" IsChecked="True" TabIndex="1" />
            <RadioButton StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" GroupName="Size" Content="Large" TabIndex="2" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "combobox",
    title: "ComboBox",
    description: "Dropdown selection control with popup item list.",
    docPage: "./samples/combobox.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="ComboBox">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" ComboBox" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <ComboBox StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" TabIndex="0">
                <ComboBoxItem Content="Red" />
                <ComboBoxItem Content="Green" />
                <ComboBoxItem Content="Blue" />
            </ComboBox>
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "tabcontrol",
    title: "TabControl",
    description: "Tabbed content switching with header strip and content area.",
    docPage: "./samples/tabcontrol.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="TabControl">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" TabControl" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <TabControl StackPanel.SizeMode="Stretch">
                <TabItem Header="Overview">
                    <TextBlock Text="  Overview content" VerticalTextAlignment="Center" />
                </TabItem>
                <TabItem Header="Settings">
                    <TextBlock Text="  Settings content" VerticalTextAlignment="Center" />
                </TabItem>
                <TabItem Header="About">
                    <TextBlock Text="  About content" VerticalTextAlignment="Center" />
                </TabItem>
            </TabControl>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "treeview",
    title: "TreeView",
    description: "Hierarchical data display with expand/collapse navigation.",
    docPage: "./samples/treeview.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="TreeView">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" TreeView" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <TreeView StackPanel.SizeMode="Stretch">
                <TreeViewItem Header="Documents" IsExpanded="True">
                    <TreeViewItem Header="Work">
                        <TreeViewItem Header="report.docx" />
                        <TreeViewItem Header="budget.xlsx" />
                    </TreeViewItem>
                    <TreeViewItem Header="Personal" IsExpanded="True">
                        <TreeViewItem Header="notes.txt" />
                    </TreeViewItem>
                </TreeViewItem>
                <TreeViewItem Header="Downloads">
                    <TreeViewItem Header="setup.exe" />
                </TreeViewItem>
            </TreeView>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "listview",
    title: "ListView",
    description: "Multi-column data display with headers, grid lines, and row selection.",
    docPage: "./samples/listview.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="ListView">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" ListView" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <ListView StackPanel.SizeMode="Stretch">
                <ListViewItem Content="Program.cs" />
                <ListViewItem Content="README.md" />
                <ListViewItem Content="appsettings.json" />
            </ListView>
        </StackPanel>
    </Border>
</Window>`
  }
];
