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
    description: "Determinate, indeterminate, and styled progress bars.",
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
                       Text="  Custom color (25%):" Padding="2,0,0,0" />
            <ProgressBar StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                         Value="25" Foreground="#E06C75" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Default (75%):" Padding="2,0,0,0" />
            <ProgressBar StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                         Value="75" />

            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "barchart",
    title: "Bar Chart",
    description: "Grouped and stacked bars with sub-cell precision, axes, and legend.",
    docPage: "./samples/barchart.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="Bar Chart">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Bar Chart" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <BarChart StackPanel.SizeMode="Stretch" Title="  Quarterly revenue" BarMode="Grouped">
                <ChartSeries Name="2024">
                    <ChartDataPoint Label="Q1" Value="42" />
                    <ChartDataPoint Label="Q2" Value="55" />
                    <ChartDataPoint Label="Q3" Value="30" />
                    <ChartDataPoint Label="Q4" Value="70" />
                </ChartSeries>
                <ChartSeries Name="2025">
                    <ChartDataPoint Label="Q1" Value="35" />
                    <ChartDataPoint Label="Q2" Value="60" />
                    <ChartDataPoint Label="Q3" Value="48" />
                    <ChartDataPoint Label="Q4" Value="90" />
                </ChartSeries>
            </BarChart>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "linechart",
    title: "Line Chart",
    description: "Multi-series line charts with date x-labels, drawn on a braille canvas.",
    docPage: "./samples/linechart.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="Line Chart">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Line Chart" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <LineChart StackPanel.SizeMode="Stretch" Title="  Latency by day (ms)" ShowMarkers="True">
                <ChartSeries Name="p50" Color="#4EC9B0">
                    <ChartDataPoint Label="Mon" Value="12" />
                    <ChartDataPoint Label="Tue" Value="18" />
                    <ChartDataPoint Label="Wed" Value="9" />
                    <ChartDataPoint Label="Thu" Value="22" />
                    <ChartDataPoint Label="Fri" Value="15" />
                    <ChartDataPoint Label="Sat" Value="28" />
                    <ChartDataPoint Label="Sun" Value="19" />
                </ChartSeries>
                <ChartSeries Name="p99" Color="#F48771">
                    <ChartDataPoint Label="Mon" Value="40" />
                    <ChartDataPoint Label="Tue" Value="55" />
                    <ChartDataPoint Label="Wed" Value="35" />
                    <ChartDataPoint Label="Thu" Value="70" />
                    <ChartDataPoint Label="Fri" Value="60" />
                    <ChartDataPoint Label="Sat" Value="85" />
                    <ChartDataPoint Label="Sun" Value="50" />
                </ChartSeries>
            </LineChart>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "tracechart",
    title: "Trace Chart",
    description: "Distributed-trace waterfall of nested spans on a shared time axis.",
    docPage: "./samples/tracechart.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="Trace Chart">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Trace Chart" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <TraceChart StackPanel.SizeMode="Stretch" Title="  GET /api/order/42" LabelWidth="18">
                <TraceSpan Name="http.request" StartMs="0" DurationMs="120">
                    <TraceSpan Name="auth" StartMs="2" DurationMs="15" />
                    <TraceSpan Name="db.query" StartMs="20" DurationMs="60">
                        <TraceSpan Name="connect" StartMs="20" DurationMs="8" />
                        <TraceSpan Name="exec" StartMs="30" DurationMs="45" />
                    </TraceSpan>
                    <TraceSpan Name="serialize" StartMs="85" DurationMs="30" />
                </TraceSpan>
            </TraceChart>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "flamegraph",
    title: "Flame Graph",
    description: "Hierarchical profiler frames sized by value, icicle layout.",
    docPage: "./samples/flamegraph.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="Flame Graph">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Flame Graph" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <FlameGraph StackPanel.SizeMode="Stretch" Title="  CPU profile">
                <FlameNode Name="main" Value="100">
                    <FlameNode Name="parseRequest" Value="25">
                        <FlameNode Name="decodeJson" Value="18" />
                    </FlameNode>
                    <FlameNode Name="handleQuery" Value="60">
                        <FlameNode Name="sqlExec" Value="40">
                            <FlameNode Name="ioWait" Value="30" />
                        </FlameNode>
                        <FlameNode Name="mapRows" Value="15" />
                    </FlameNode>
                    <FlameNode Name="writeResponse" Value="15" />
                </FlameNode>
            </FlameGraph>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "nodegraph",
    title: "Node Graph",
    description: "Topology graph with force-directed auto-layout and selectable nodes.",
    docPage: "./samples/nodegraph.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="Node Graph">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Node Graph" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <NodeGraph StackPanel.SizeMode="Stretch" Title="  Service topology">
                <GraphNode Id="lb" Name="load balancer" />
                <GraphNode Id="web1" Name="web-1" />
                <GraphNode Id="web2" Name="web-2" />
                <GraphNode Id="api" Name="api" />
                <GraphNode Id="db" Name="postgres" />
                <GraphNode Id="cache" Name="redis" />
                <NodeGraph.GraphEdges>
                    <GraphEdge From="lb" To="web1" />
                    <GraphEdge From="lb" To="web2" />
                    <GraphEdge From="web1" To="api" />
                    <GraphEdge From="web2" To="api" />
                    <GraphEdge From="api" To="db" />
                    <GraphEdge From="api" To="cache" />
                </NodeGraph.GraphEdges>
            </NodeGraph>
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
                       Text="  Modal dialogs use ShowDialogAsync() from C# code-behind."
                       Padding="2,0,0,0" VerticalTextAlignment="Center" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Usage:" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="    var result = await dialog.ShowDialogAsync();" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="    dialog.DialogResult = true; // closes it" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Dialog layout preview:" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <Border StackPanel.SizeMode="Stretch" BorderStyle="Rounded" Background="#252526">
                <StackPanel Orientation="Vertical">
                    <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                               Text=" Confirm Action" />
                    <TextBlock StackPanel.SizeMode="Stretch" Text="Are you sure?"
                               HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
                    <StackPanel StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" Orientation="Horizontal">
                        <Button StackPanel.SizeMode="Auto" Text="OK" Width="12" Height="3"
                                HoverColor="Green" TabIndex="0" />
                        <Button StackPanel.SizeMode="Auto" Text="Cancel" Width="12" Height="3"
                                HoverColor="Red" TabIndex="1" />
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
    id: "dock-layout",
    title: "DockPanel Layout",
    description: "Dock children to the edges in declaration order; the last child fills the rest.",
    docPage: "./samples/dock-layout.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="DockPanel Layout">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" DockPanel Layout" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Each child is docked to one edge of what is left; the last child fills the rest."
                       Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  LastChildFill (default) — header, footer, sidebar, content:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Fixed" StackPanel.FixedSize="10" BorderStyle="Single">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text=" Top — header (1 row)" Background="#5F0000" />
                    <TextBlock DockPanel.Dock="Bottom" Text=" Bottom — status bar (1 row)" Background="#00005F" />
                    <Border DockPanel.Dock="Left" Width="18" Background="#005F5F">
                        <TextBlock Text=" Left (18 cols)" VerticalTextAlignment="Center" />
                    </Border>
                    <Border DockPanel.Dock="Right" Width="14" Background="#5F005F">
                        <TextBlock Text=" Right (14)" VerticalTextAlignment="Center" />
                    </Border>
                    <Border Background="#005F00">
                        <TextBlock Text=" Fill — takes every cell left over"
                                   HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
                    </Border>
                </DockPanel>
            </Border>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Order matters — Left first claims the full height; Top first claims the full width:"
                       Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Stretch" BorderStyle="Single">
                <UniformGrid Columns="2">
                    <Border BorderStyle="Rounded">
                        <DockPanel>
                            <Border DockPanel.Dock="Left" Width="12" Background="#5F5F00">
                                <TextBlock Text=" Left first" VerticalTextAlignment="Center" />
                            </Border>
                            <TextBlock DockPanel.Dock="Top" Text=" Top second" Background="#5F0000" />
                            <Border Background="#00005F">
                                <TextBlock Text=" Fill" VerticalTextAlignment="Center" />
                            </Border>
                        </DockPanel>
                    </Border>
                    <Border BorderStyle="Rounded">
                        <DockPanel>
                            <TextBlock DockPanel.Dock="Top" Text=" Top first" Background="#5F0000" />
                            <Border DockPanel.Dock="Left" Width="12" Background="#5F5F00">
                                <TextBlock Text=" Left second" VerticalTextAlignment="Center" />
                            </Border>
                            <Border Background="#00005F">
                                <TextBlock Text=" Fill" VerticalTextAlignment="Center" />
                            </Border>
                        </DockPanel>
                    </Border>
                </UniformGrid>
            </Border>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "shared-size",
    title: "Shared Size",
    description: "Columns in separate grids agreeing on one width, so a list of rows lines up.",
    docPage: "./samples/shared-size.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Shared Size">
    <Border BorderStyle="Rounded" Grid.IsSharedSizeScope="True">
    <StackPanel Orientation="Vertical">
        <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                   Text=" Shared Size" Padding="2,0,0,0" />

        <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="2"
                   Text="  Separate grids agreeing on one column width. The border above carries&#10;  Grid.IsSharedSizeScope, which is what bounds the group."
                   Padding="2,0,0,0" />

        <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                   Text="  Each row below is its own Grid — yet the keys line up:" Padding="2,0,0,0" />

        <Grid StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" ColumnSpacing="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="auto" SharedSizeGroup="keys" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="enter" Padding="2,0,0,0" />
            <TextBlock Grid.Column="1" Text="open the selected row" />
        </Grid>

        <Grid StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" ColumnSpacing="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="auto" SharedSizeGroup="keys" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="backspace" Padding="2,0,0,0" />
            <TextBlock Grid.Column="1" Text="back to the previous centre" />
        </Grid>

        <Grid StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" ColumnSpacing="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="auto" SharedSizeGroup="keys" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="q" Padding="2,0,0,0" />
            <TextBlock Grid.Column="1" Text="quit" />
        </Grid>

        <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="2"
                   Text="&#10;  Without a group, each Auto column sizes to its own content only:" Padding="2,0,0,0" />

        <Grid StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" ColumnSpacing="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="auto" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="enter" Padding="2,0,0,0" />
            <TextBlock Grid.Column="1" Text="ragged — starts right after the key" />
        </Grid>

        <Grid StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" ColumnSpacing="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="auto" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="backspace" Padding="2,0,0,0" />
            <TextBlock Grid.Column="1" Text="ragged — starts further along" />
        </Grid>

        <TextBlock StackPanel.SizeMode="Stretch" Text="" />
    </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "uniformgrid-layout",
    title: "UniformGrid Layout",
    description: "Equal cells filled row by row, with the shape derived from the child count.",
    docPage: "./samples/uniformgrid-layout.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="UniformGrid Layout">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" UniformGrid Layout" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Equal cells. Leftover cells go to the leading columns, so the widths always add up."
                       Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Rows=2 Columns=3 — six equal cells:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Fixed" StackPanel.FixedSize="8" BorderStyle="Single">
                <UniformGrid Rows="2" Columns="3">
                    <Border Background="#5F0000"><TextBlock Text=" 1" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#00005F"><TextBlock Text=" 2" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#005F00"><TextBlock Text=" 3" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#5F5F00"><TextBlock Text=" 4" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#5F005F"><TextBlock Text=" 5" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#005F5F"><TextBlock Text=" 6" VerticalTextAlignment="Center" /></Border>
                </UniformGrid>
            </Border>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Columns=4, Rows left at 0 — the row count follows the child count:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Fixed" StackPanel.FixedSize="6" BorderStyle="Single">
                <UniformGrid Columns="4">
                    <Border Background="#5F0000"><TextBlock Text=" A" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#00005F"><TextBlock Text=" B" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#005F00"><TextBlock Text=" C" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#5F5F00"><TextBlock Text=" D" VerticalTextAlignment="Center" /></Border>
                    <Border Background="#5F005F"><TextBlock Text=" E" VerticalTextAlignment="Center" /></Border>
                </UniformGrid>
            </Border>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Neither set — nine children auto-derive a 3x3 grid:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Stretch" BorderStyle="Single">
                <UniformGrid>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 1" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 2" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 3" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 4" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 5" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 6" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 7" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 8" VerticalTextAlignment="Center" /></Border>
                    <Border BorderStyle="Rounded"><TextBlock Text=" 9" VerticalTextAlignment="Center" /></Border>
                </UniformGrid>
            </Border>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "wrap-layout",
    title: "WrapPanel Layout",
    description: "Children flow along one axis and wrap to a new line at the bound.",
    docPage: "./samples/wrap-layout.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="WrapPanel Layout">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" WrapPanel Layout" Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Children flow until the next one would cross the bound, then a new line starts."
                       Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Horizontal — flows right, wraps down:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Fixed" StackPanel.FixedSize="8" BorderStyle="Single">
                <WrapPanel Orientation="Horizontal">
                    <Border Width="22" Height="3" Background="#5F0000"><TextBlock Text=" alpha" VerticalTextAlignment="Center" /></Border>
                    <Border Width="22" Height="3" Background="#00005F"><TextBlock Text=" bravo" VerticalTextAlignment="Center" /></Border>
                    <Border Width="22" Height="3" Background="#005F00"><TextBlock Text=" charlie" VerticalTextAlignment="Center" /></Border>
                    <Border Width="22" Height="3" Background="#5F5F00"><TextBlock Text=" delta" VerticalTextAlignment="Center" /></Border>
                    <Border Width="22" Height="3" Background="#5F005F"><TextBlock Text=" echo" VerticalTextAlignment="Center" /></Border>
                    <Border Width="22" Height="3" Background="#005F5F"><TextBlock Text=" foxtrot" VerticalTextAlignment="Center" /></Border>
                </WrapPanel>
            </Border>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Mixed sizes — every child in a line gets the tallest child's height:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Fixed" StackPanel.FixedSize="8" BorderStyle="Single">
                <WrapPanel Orientation="Horizontal">
                    <Border Width="24" Height="2" Background="#5F0000"><TextBlock Text=" 24x2" VerticalTextAlignment="Center" /></Border>
                    <Border Width="28" Height="4" Background="#00005F"><TextBlock Text=" 28x4" VerticalTextAlignment="Center" /></Border>
                    <Border Width="22" Height="1" Background="#005F00"><TextBlock Text=" 22x1" /></Border>
                    <Border Width="26" Height="3" Background="#5F5F00"><TextBlock Text=" 26x3" VerticalTextAlignment="Center" /></Border>
                </WrapPanel>
            </Border>

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />

            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text="  Vertical — flows down, wraps right:" Padding="2,0,0,0" />

            <Border StackPanel.SizeMode="Stretch" BorderStyle="Single">
                <WrapPanel Orientation="Vertical">
                    <Border Width="16" Height="2" Background="#5F0000"><TextBlock Text=" one" VerticalTextAlignment="Center" /></Border>
                    <Border Width="16" Height="2" Background="#00005F"><TextBlock Text=" two" VerticalTextAlignment="Center" /></Border>
                    <Border Width="16" Height="2" Background="#005F00"><TextBlock Text=" three" VerticalTextAlignment="Center" /></Border>
                    <Border Width="16" Height="2" Background="#5F5F00"><TextBlock Text=" four" VerticalTextAlignment="Center" /></Border>
                    <Border Width="16" Height="2" Background="#5F005F"><TextBlock Text=" five" VerticalTextAlignment="Center" /></Border>
                    <Border Width="16" Height="2" Background="#005F5F"><TextBlock Text=" six" VerticalTextAlignment="Center" /></Border>
                    <Border Width="16" Height="2" Background="#5F2F00"><TextBlock Text=" seven" VerticalTextAlignment="Center" /></Border>
                    <Border Width="16" Height="2" Background="#2F005F"><TextBlock Text=" eight" VerticalTextAlignment="Center" /></Border>
                </WrapPanel>
            </Border>
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
  },

  {
    id: "numberpicker",
    title: "NumberPicker",
    description: "Numeric input with arrow key increment/decrement and direct digit entry.",
    docPage: "./samples/numberpicker.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="NumberPicker">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" NumberPicker" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <NumberPicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" Value="42" Minimum="0" Maximum="100" TabIndex="0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <NumberPicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" Value="0.5" Minimum="0" Maximum="1" Increment="0.1" DecimalPlaces="1" TabIndex="1" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "datepicker",
    title: "DatePicker",
    description: "Date input with field-by-field editing and calendar icon.",
    docPage: "./samples/datepicker.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="DatePicker">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" DatePicker" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <DatePicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" TabIndex="0" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "timepicker",
    title: "TimePicker",
    description: "Time input with hours/minutes/seconds fields and clock icon.",
    docPage: "./samples/timepicker.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="TimePicker">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" TimePicker" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <TimePicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" TabIndex="0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <TimePicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" ShowSeconds="True" TabIndex="1" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "datetimepicker",
    title: "DateTimePicker",
    description: "Combined date and time input with calendar icon.",
    docPage: "./samples/datetimepicker.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="DateTimePicker">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" DateTimePicker" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <DateTimePicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" TabIndex="0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <DateTimePicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="3" ShowSeconds="True" TabIndex="1" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "datagrid",
    title: "DataGrid",
    description: "Multi-column grid with typed columns, sorting, and row selection.",
    docPage: "./samples/datagrid.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="DataGrid">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" DataGrid" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <DataGrid StackPanel.SizeMode="Stretch" SelectedIndex="0">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="File" />
                    <DataGridTextColumn Header="Size" Width="10" />
                    <DataGridTextColumn Header="Status" Width="12" SortDirection="Ascending" />
                </DataGrid.Columns>
                <ListViewItem Content="Program.cs" />
                <ListViewItem Content="README.md" />
                <ListViewItem Content="App.xaml" />
                <ListViewItem Content="appsettings.json" />
            </DataGrid>
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "filepicker",
    title: "FilePicker",
    description: "Modal dialog for browsing and selecting files.",
    docPage: "./samples/filepicker.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="FilePicker">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" FilePicker" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="  Opens a modal dialog (requires Application)" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "folderpicker",
    title: "FolderPicker",
    description: "Modal dialog for browsing and selecting a folder.",
    docPage: "./samples/folderpicker.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="FolderPicker">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" FolderPicker" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="  Opens a modal dialog (requires Application)" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "colorpicker",
    title: "ColorPicker",
    description: "Color selection with palette grid, hex entry, and preview swatch.",
    docPage: "./samples/colorpicker.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="ColorPicker">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" ColorPicker" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <ColorPicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="5" TabIndex="0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="" />
            <ColorPicker StackPanel.SizeMode="Fixed" StackPanel.FixedSize="5" TabIndex="1" SelectedColor="#FF6B6B" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  },

  {
    id: "image",
    title: "Image",
    description: "Pixel rendering using half-block characters for 2x vertical resolution.",
    docPage: "./samples/image.html",
    xaml: `<Window xmlns="http://schemas.terminalninja.dev/xaml" Title="Image">
    <Border BorderStyle="Rounded">
        <StackPanel Orientation="Vertical">
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text=" Image" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1" Text="  Half-block rendering (source set programmatically)" Padding="2,0,0,0" />
            <TextBlock StackPanel.SizeMode="Stretch" Text="" />
        </StackPanel>
    </Border>
</Window>`
  }
];
