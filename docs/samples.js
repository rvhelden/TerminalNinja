/**
 * samples.js — XAML sample snippets for the TerminalNinja playground.
 *
 * Each sample is a self-contained Window (no code-behind / no DataContext)
 * so it can render in the WASM module without a ViewModel.
 */

export const SAMPLES = [
  {
    id: "progress-bars",
    title: "Progress Bars",
    description: "Determinate, indeterminate, and custom-character progress bars.",
    docPage: "./samples/progress-bars.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Progress Bars" Width="80" Height="24">
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
    id: "buttons",
    title: "Buttons",
    description: "Button controls with styling, hover colors, and tab navigation.",
    docPage: "./samples/buttons.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Buttons" Width="80" Height="24">
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
        Title="Data Binding" Width="80" Height="24">
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
        Title="Dialogs" Width="80" Height="24">
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
    id: "lists",
    title: "Lists",
    description: "ListBox with selection, ObservableCollection, and add/remove items.",
    docPage: "./samples/lists.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Lists" Width="80" Height="24">
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
        Title="Grid Layout" Width="80" Height="24">
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
        Title="StackPanel Layout" Width="80" Height="24">
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
  }
];
