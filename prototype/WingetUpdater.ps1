#Requires -Version 5.1
<#
    Winget Updater - pick which winget upgrades to install.
    Run with "Winget Updater.bat" (or: powershell -ExecutionPolicy Bypass -File WingetUpdater.ps1)
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) {
    [System.Windows.MessageBox]::Show('winget.exe was not found. Install "App Installer" from the Microsoft Store.', 'Winget Updater', 'OK', 'Error') | Out-Null
    return
}

# ---------------------------------------------------------------- data model
if (-not ('WgPackageV1' -as [type])) {
    Add-Type -TypeDefinition @'
using System.ComponentModel;
public class WgPackageV1 : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private void Notify(string p) { PropertyChangedEventHandler h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(p)); }
    private bool _selected, _ignored;
    private string _status = "", _state = "Idle";
    public string Name { get; set; }
    public string Id { get; set; }
    public string Version { get; set; }
    public string Available { get; set; }
    public string Source { get; set; }
    public bool Explicit { get; set; }
    public bool Selected { get { return _selected; } set { if (_selected != value) { _selected = value; Notify("Selected"); } } }
    public bool Ignored  { get { return _ignored; }  set { if (_ignored != value) { _ignored = value; Notify("Ignored"); Notify("Notes"); } } }
    public string Status { get { return _status; } set { _status = value; Notify("Status"); } }
    public string State  { get { return _state; }  set { _state = value; Notify("State"); } }
    public string Notes {
        get {
            if (_ignored) return "Ignored";
            if (Explicit) return "Pinned / explicit only";
            if (Version == "Unknown") return "Current version unknown";
            return "";
        }
    }
}
'@
}

# ---------------------------------------------------------------- settings
$AppDir       = Join-Path $env:APPDATA 'WingetUpdater'
$SettingsFile = Join-Path $AppDir 'settings.json'
$LogDir       = Join-Path $AppDir 'logs'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$Settings = [ordered]@{ IgnoredIds = @(); Silent = $false; IncludeUnknown = $true }
if (Test-Path $SettingsFile) {
    try {
        $j = Get-Content $SettingsFile -Raw | ConvertFrom-Json
        foreach ($k in @($Settings.Keys)) { if ($j.PSObject.Properties[$k]) { $Settings[$k] = $j.$k } }
    } catch { }
}
$Ignored = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($id in @($Settings.IgnoredIds)) { if ($id) { [void]$Ignored.Add($id) } }

function Save-Settings {
    $Settings.IgnoredIds     = @($Ignored | Sort-Object)
    $Settings.Silent         = [bool]$chkSilent.IsChecked
    $Settings.IncludeUnknown = [bool]$chkUnknown.IsChecked
    $Settings | ConvertTo-Json | Set-Content -Path $SettingsFile -Encoding UTF8
}

# ---------------------------------------------------------------- background work (runs in a separate runspace)
$Common = {
    function Send-Msg($Type, $Data) { $Q.Enqueue(@{ Type = $Type; Data = $Data }) }

    # Runs winget, streams its output to the UI, returns exit code + output lines.
    function Invoke-Winget([string[]]$WgArgs, [switch]$Quiet) {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName  = 'winget.exe'
        $psi.Arguments = ($WgArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
        $psi.UseShellExecute        = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        $psi.CreateNoWindow         = $true
        $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
        $psi.StandardErrorEncoding  = [System.Text.Encoding]::UTF8
        if (-not $Quiet) { Send-Msg 'log' ('> winget ' + $psi.Arguments) }

        $p = [System.Diagnostics.Process]::Start($psi)
        $errTask = $p.StandardError.ReadToEndAsync()
        $lines = New-Object System.Collections.Generic.List[string]
        $cur   = New-Object System.Text.StringBuilder
        $buf   = New-Object char[] 4096
        $pendingCR = $false

        # "\n" ends a real line; a lone "\r" means winget is redrawing a spinner/progress bar.
        $emitLine = {
            $t = $cur.ToString(); [void]$cur.Clear(); $lines.Add($t)
            if (-not $Quiet -and $t.Trim()) { Send-Msg 'log' $t }
        }
        $emitProgress = {
            $t = $cur.ToString().Trim(); [void]$cur.Clear()
            if ($t -and $t -notmatch '^[-\\|/]$') { Send-Msg 'progress' $t }
        }

        while (($n = $p.StandardOutput.Read($buf, 0, $buf.Length)) -gt 0) {
            for ($i = 0; $i -lt $n; $i++) {
                $c = $buf[$i]
                if ($pendingCR) {
                    $pendingCR = $false
                    if ($c -eq "`n") { & $emitLine; continue }
                    & $emitProgress
                }
                if     ($c -eq "`r") { $pendingCR = $true }
                elseif ($c -eq "`n") { & $emitLine }
                else                 { [void]$cur.Append($c) }
            }
        }
        if ($cur.Length -gt 0) { & $emitLine }
        $p.WaitForExit()
        $err = $errTask.Result
        if ($err -and $err.Trim()) { Send-Msg 'log' $err.Trim() }
        [pscustomobject]@{ ExitCode = $p.ExitCode; Lines = $lines.ToArray() }
    }

    # Parses winget's fixed-width tables using the header column positions.
    function ConvertFrom-WingetTable([string[]]$Lines) {
        $out = New-Object System.Collections.Generic.List[object]
        $table = 0
        for ($i = 1; $i -lt $Lines.Count; $i++) {
            if ($Lines[$i] -notmatch '^\s*-{10,}\s*$') { continue }
            $table++
            $starts = @([regex]::Matches($Lines[$i - 1], '\S+') | ForEach-Object { $_.Index })
            if ($starts.Count -lt 4) { continue }
            $j = $i + 1
            for (; $j -lt $Lines.Count; $j++) {
                $l = $Lines[$j]
                if ([string]::IsNullOrWhiteSpace($l) -or $l.Length -le $starts[3]) { break }
                $f = @()
                for ($c = 0; $c -lt $starts.Count; $c++) {
                    $s = $starts[$c]
                    if ($c + 1 -lt $starts.Count) { $e = [Math]::Min($starts[$c + 1], $l.Length) } else { $e = $l.Length }
                    if ($s -ge $l.Length) { $f += '' } else { $f += $l.Substring($s, $e - $s).Trim() }
                }
                if ($f[1] -notmatch '^\S+$') { continue }
                $src = ''; if ($f.Count -gt 4) { $src = $f[4] }
                $out.Add([pscustomobject]@{
                    Name = $f[0]; Id = $f[1]; Version = $f[2]; Available = $f[3]; Source = $src
                    Explicit = ($table -gt 1)
                })
            }
            $i = $j
        }
        , $out.ToArray()
    }
}

$ListWork = {
    try {
        if (-not $QuietRefresh) { Send-Msg 'progress' 'Checking for updates...' }
        $a = @('upgrade', '--accept-source-agreements', '--disable-interactivity')
        if ($IncludeUnknown) { $a += '--include-unknown' }
        $r = Invoke-Winget $a -Quiet
        $pkgs = ConvertFrom-WingetTable $r.Lines
        Send-Msg 'list' $pkgs
        if (-not $QuietRefresh) { Send-Msg 'progress' '' }
    } catch { Send-Msg 'log' ('ERROR: ' + $_) }
}

$UpdateWork = {
    $ok = 0; $fail = 0; $skip = 0; $n = 0
    try {
        foreach ($id in $Ids) {
            if ($Shared.Cancel) { $skip++; Send-Msg 'status' @{ Id = $id; State = 'Skip'; Text = 'Cancelled' }; continue }
            $n++
            Send-Msg 'status'   @{ Id = $id; State = 'Running'; Text = 'Updating...' }
            Send-Msg 'progress' ('[{0}/{1}] Updating {2}' -f $n, $Ids.Count, $id)
            $a = @('upgrade', '--id', $id, '--exact', '--include-unknown',
                   '--accept-package-agreements', '--accept-source-agreements', '--disable-interactivity')
            if ($Silent) { $a += '--silent' }
            $r = Invoke-Winget $a
            $code = $r.ExitCode
            $hex = '0x{0:X8}' -f $code
            if ($code -eq 0) {
                $ok++
                $text = 'Updated'
                if (($r.Lines -join "`n") -match 'restart') { $text = 'Updated - restart needed' }
                Send-Msg 'status' @{ Id = $id; State = 'Ok'; Text = $text }
            } elseif ($code -eq 0x8A15002B) {
                $skip++; Send-Msg 'status' @{ Id = $id; State = 'Skip'; Text = 'No applicable update' }
            } elseif ($code -eq 0x8A150101) {
                $fail++; Send-Msg 'status' @{ Id = $id; State = 'Fail'; Text = 'App is running - close it' }
            } else {
                $fail++; Send-Msg 'status' @{ Id = $id; State = 'Fail'; Text = "Failed ($hex)" }
            }
            Send-Msg 'log' ("<< {0}: exit {1}" -f $id, $hex)
            Send-Msg 'log' ''
        }
    } catch { Send-Msg 'log' ('ERROR: ' + $_) }
    Send-Msg 'summary' ('Finished: {0} updated, {1} failed, {2} skipped' -f $ok, $fail, $skip)
}

$Q      = New-Object 'System.Collections.Concurrent.ConcurrentQueue[object]'
$Shared = [hashtable]::Synchronized(@{ Cancel = $false })
$script:Job = $null
$script:AfterDone = $null

function Start-Background([scriptblock]$Work, [hashtable]$Vars) {
    Set-Busy $true
    $rs = [runspacefactory]::CreateRunspace()
    $rs.Open()
    $rs.SessionStateProxy.SetVariable('Q', $Q)
    $rs.SessionStateProxy.SetVariable('Shared', $Shared)
    foreach ($k in $Vars.Keys) { $rs.SessionStateProxy.SetVariable($k, $Vars[$k]) }
    $ps = [powershell]::Create()
    $ps.Runspace = $rs
    [void]$ps.AddScript($Common.ToString() + "`n" + $Work.ToString())
    $script:Job = @{ PS = $ps; RS = $rs; Handle = $ps.BeginInvoke() }
}

function Complete-Background {
    try { [void]$script:Job.PS.EndInvoke($script:Job.Handle) } catch { Add-Log ('ERROR: ' + $_.Exception.Message) }
    $script:Job.PS.Dispose(); $script:Job.RS.Dispose(); $script:Job = $null
    Set-Busy $false
    if ($script:AfterDone) { $a = $script:AfterDone; $script:AfterDone = $null; & $a }
}

# ---------------------------------------------------------------- UI
[xml]$xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Winget Updater" Height="660" Width="1060" MinHeight="420" MinWidth="760"
        WindowStartupLocation="CenterScreen" Background="#F3F4F6" FontFamily="Segoe UI" FontSize="13">
  <Window.Resources>
    <Style TargetType="Button">
      <Setter Property="Padding" Value="14,5"/>
      <Setter Property="Margin" Value="0,0,8,0"/>
      <Setter Property="MinHeight" Value="30"/>
      <Setter Property="Background" Value="White"/>
      <Setter Property="BorderBrush" Value="#C9CDD2"/>
    </Style>
    <Style x:Key="Primary" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
      <Setter Property="Background" Value="#0067C0"/>
      <Setter Property="Foreground" Value="White"/>
      <Setter Property="BorderBrush" Value="#005A9E"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Margin" Value="0"/>
    </Style>
    <Style TargetType="CheckBox">
      <Setter Property="VerticalAlignment" Value="Center"/>
      <Setter Property="Margin" Value="0,0,16,0"/>
    </Style>
    <Style x:Key="Cell" TargetType="TextBlock">
      <Setter Property="VerticalAlignment" Value="Center"/>
      <Setter Property="Margin" Value="6,0"/>
      <Setter Property="TextTrimming" Value="CharacterEllipsis"/>
    </Style>
  </Window.Resources>

  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- header -->
    <DockPanel Grid.Row="0" Margin="0,0,0,12">
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
        <TextBlock x:Name="AdminText" VerticalAlignment="Center" Margin="0,0,10,0" Foreground="#555"/>
        <Button x:Name="BtnAdmin" Content="Restart as admin" Margin="0"
                ToolTip="Run elevated so installers don't each ask for UAC permission"/>
      </StackPanel>
      <StackPanel>
        <TextBlock Text="Winget Updater" FontSize="22" FontWeight="SemiBold"/>
        <TextBlock x:Name="SummaryText" Foreground="#555" Text="Loading..."/>
      </StackPanel>
    </DockPanel>

    <!-- toolbar -->
    <DockPanel Grid.Row="1" Margin="0,0,0,8" LastChildFill="False">
      <Button x:Name="BtnRefresh" Content="Refresh"/>
      <Button x:Name="BtnAll" Content="Select all"/>
      <Button x:Name="BtnNone" Content="Select none"/>
      <TextBlock Text="Filter:" VerticalAlignment="Center" Margin="8,0,6,0"/>
      <TextBox x:Name="TxtFilter" Width="220" VerticalContentAlignment="Center" Height="30" Padding="4,0"/>
      <CheckBox x:Name="ChkShowIgnored" DockPanel.Dock="Right" Content="Show ignored" Margin="0"/>
    </DockPanel>

    <!-- package list -->
    <DataGrid x:Name="Grid" Grid.Row="2" AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
              HeadersVisibility="Column" GridLinesVisibility="Horizontal" HorizontalGridLinesBrush="#ECEEF1"
              Background="White" RowBackground="White" BorderBrush="#D5D9DE" RowHeight="32"
              SelectionMode="Extended" SelectionUnit="FullRow">
      <DataGrid.ColumnHeaderStyle>
        <Style TargetType="DataGridColumnHeader">
          <Setter Property="Padding" Value="6,6"/>
          <Setter Property="Background" Value="#F8F9FA"/>
          <Setter Property="BorderBrush" Value="#E1E4E8"/>
          <Setter Property="BorderThickness" Value="0,0,1,1"/>
          <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
      </DataGrid.ColumnHeaderStyle>
      <DataGrid.RowStyle>
        <Style TargetType="DataGridRow">
          <Style.Triggers>
            <DataTrigger Binding="{Binding Ignored}" Value="True"><Setter Property="Opacity" Value="0.45"/></DataTrigger>
          </Style.Triggers>
        </Style>
      </DataGrid.RowStyle>
      <DataGrid.ContextMenu>
        <ContextMenu>
          <MenuItem Header="Ignore (never update)"/>
          <MenuItem Header="Stop ignoring"/>
          <Separator/>
          <MenuItem Header="Copy ID"/>
          <MenuItem Header="Show package info in log"/>
        </ContextMenu>
      </DataGrid.ContextMenu>
      <DataGrid.Columns>
        <DataGridTemplateColumn Width="40" SortMemberPath="Selected">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate>
              <CheckBox IsChecked="{Binding Selected, UpdateSourceTrigger=PropertyChanged}" HorizontalAlignment="Center" Margin="0"/>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
        <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="2*" IsReadOnly="True" ElementStyle="{StaticResource Cell}"/>
        <DataGridTextColumn Header="ID" Binding="{Binding Id}" Width="2*" IsReadOnly="True" ElementStyle="{StaticResource Cell}"/>
        <DataGridTextColumn Header="Installed" Binding="{Binding Version}" Width="*" IsReadOnly="True" ElementStyle="{StaticResource Cell}"/>
        <DataGridTextColumn Header="Available" Binding="{Binding Available}" Width="*" IsReadOnly="True" ElementStyle="{StaticResource Cell}"/>
        <DataGridTextColumn Header="Notes" Binding="{Binding Notes}" Width="1.4*" IsReadOnly="True" ElementStyle="{StaticResource Cell}"/>
        <DataGridTemplateColumn Header="Status" Width="1.6*" SortMemberPath="Status">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate>
              <TextBlock Text="{Binding Status}" VerticalAlignment="Center" Margin="6,0" TextTrimming="CharacterEllipsis">
                <TextBlock.Style>
                  <Style TargetType="TextBlock">
                    <Style.Triggers>
                      <DataTrigger Binding="{Binding State}" Value="Ok"><Setter Property="Foreground" Value="#107C10"/><Setter Property="FontWeight" Value="SemiBold"/></DataTrigger>
                      <DataTrigger Binding="{Binding State}" Value="Fail"><Setter Property="Foreground" Value="#C42B1C"/><Setter Property="FontWeight" Value="SemiBold"/></DataTrigger>
                      <DataTrigger Binding="{Binding State}" Value="Running"><Setter Property="Foreground" Value="#0067C0"/><Setter Property="FontWeight" Value="SemiBold"/></DataTrigger>
                      <DataTrigger Binding="{Binding State}" Value="Queued"><Setter Property="Foreground" Value="#6B7280"/></DataTrigger>
                      <DataTrigger Binding="{Binding State}" Value="Skip"><Setter Property="Foreground" Value="#9A6700"/></DataTrigger>
                    </Style.Triggers>
                  </Style>
                </TextBlock.Style>
              </TextBlock>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
      </DataGrid.Columns>
    </DataGrid>

    <!-- actions -->
    <DockPanel Grid.Row="3" Margin="0,10,0,0">
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
        <Button x:Name="BtnCancel" Content="Stop after current" Visibility="Collapsed"/>
        <Button x:Name="BtnUpdate" Content="Update selected (0)" Style="{StaticResource Primary}" MinWidth="170"/>
      </StackPanel>
      <StackPanel Orientation="Horizontal">
        <CheckBox x:Name="ChkSilent" Content="Silent install (hide installer windows)"/>
        <CheckBox x:Name="ChkUnknown" Content="Include apps with unknown version"/>
      </StackPanel>
    </DockPanel>

    <!-- progress -->
    <StackPanel Grid.Row="4" Margin="0,10,0,0">
      <ProgressBar x:Name="Progress" Height="4" IsIndeterminate="False" Visibility="Hidden" BorderThickness="0" Foreground="#0067C0"/>
      <TextBlock x:Name="ProgressText" Margin="0,4,0,0" Foreground="#444" TextTrimming="CharacterEllipsis" FontFamily="Consolas"/>
    </StackPanel>

    <!-- log -->
    <Expander Grid.Row="5" Header="Log" Margin="0,6,0,0">
      <DockPanel Margin="0,6,0,0">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,6,0,0">
          <Button x:Name="BtnOpenLogs" Content="Open log folder"/>
          <Button x:Name="BtnClearLog" Content="Clear"/>
        </StackPanel>
        <TextBox x:Name="LogBox" Height="170" IsReadOnly="True" FontFamily="Consolas" FontSize="12"
                 VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto" TextWrapping="NoWrap" Background="White"/>
      </DockPanel>
    </Expander>
  </Grid>
</Window>
'@

$window = [Windows.Markup.XamlReader]::Load((New-Object System.Xml.XmlNodeReader $xaml))
foreach ($n in 'AdminText','BtnAdmin','SummaryText','BtnRefresh','BtnAll','BtnNone','TxtFilter','ChkShowIgnored','Grid',
               'BtnCancel','BtnUpdate','ChkSilent','ChkUnknown','Progress','ProgressText','BtnOpenLogs','BtnClearLog','LogBox') {
    Set-Variable -Name $n -Value $window.FindName($n)
}
$chkSilent = $ChkSilent; $chkUnknown = $ChkUnknown
$ChkSilent.IsChecked  = [bool]$Settings.Silent
$ChkUnknown.IsChecked = [bool]$Settings.IncludeUnknown

$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($IsAdmin) { $AdminText.Text = 'Running as administrator'; $BtnAdmin.Visibility = 'Collapsed' }

$Items = New-Object 'System.Collections.ObjectModel.ObservableCollection[object]'
$Grid.ItemsSource = $Items
$View = [System.Windows.Data.CollectionViewSource]::GetDefaultView($Items)
$View.Filter = [Predicate[object]]{
    param($o)
    if ($o.Ignored -and -not $ChkShowIgnored.IsChecked) { return $false }
    $f = $TxtFilter.Text.Trim()
    if (-not $f) { return $true }
    return ($o.Name.IndexOf($f, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $o.Id.IndexOf($f, [StringComparison]::OrdinalIgnoreCase) -ge 0)
}

# ---------------------------------------------------------------- helpers
$script:LogPending = New-Object System.Text.StringBuilder
function Add-Log([string]$Text) { [void]$script:LogPending.AppendLine($Text) }
function Flush-Log {
    if ($script:LogPending.Length -eq 0) { return }
    $t = $script:LogPending.ToString(); [void]$script:LogPending.Clear()
    $LogBox.AppendText($t); $LogBox.ScrollToEnd()
    try { Add-Content -Path (Join-Path $LogDir ((Get-Date -Format 'yyyy-MM-dd') + '.log')) -Value $t.TrimEnd() -Encoding UTF8 } catch { }
}

function Set-Busy([bool]$Busy) {
    foreach ($c in $BtnRefresh, $BtnUpdate, $ChkUnknown, $BtnAdmin) { $c.IsEnabled = -not $Busy }
    $Progress.IsIndeterminate = $Busy
    $Progress.Visibility = $(if ($Busy) { 'Visible' } else { 'Hidden' })
    $BtnCancel.Visibility = 'Collapsed'
    $BtnCancel.IsEnabled = $true
}

function Update-Summary {
    $visible = @($Items | Where-Object { -not $_.Ignored }).Count
    $ign = @($Items | Where-Object { $_.Ignored }).Count
    $s = '{0} update(s) available' -f $visible
    if ($ign) { $s += ' ({0} ignored)' -f $ign }
    $SummaryText.Text = $s + ' - last checked ' + (Get-Date -Format 'HH:mm')
}

function Start-Refresh([switch]$Quiet) {
    Save-Settings
    $script:KeepStatus = [bool]$Quiet
    Start-Background $ListWork @{ IncludeUnknown = [bool]$ChkUnknown.IsChecked; QuietRefresh = [bool]$Quiet }
}

function Get-RowTargets { @($Grid.SelectedItems | ForEach-Object { $_ }) }

# ---------------------------------------------------------------- message pump (UI thread)
function Invoke-Msg($m) {
    switch ($m.Type) {
        'log'      { Add-Log $m.Data }
        'progress' { $ProgressText.Text = $m.Data }
        'summary'  { $ProgressText.Text = $m.Data; Add-Log $m.Data; [System.Media.SystemSounds]::Asterisk.Play() }
        'status' {
            foreach ($it in $Items) {
                if ($it.Id -eq $m.Data.Id) { $it.State = $m.Data.State; $it.Status = $m.Data.Text; if ($m.Data.State -eq 'Ok') { $it.Selected = $false } }
            }
        }
        'list' {
            $old = @{}
            foreach ($it in $Items) { $old[$it.Id] = $it }
            $Items.Clear()
            foreach ($p in @($m.Data)) {
                $o = New-Object WgPackageV1
                $o.Name = $p.Name; $o.Id = $p.Id; $o.Version = $p.Version; $o.Available = $p.Available
                $o.Source = $p.Source; $o.Explicit = [bool]$p.Explicit
                $o.Ignored = $Ignored.Contains($p.Id)
                if ($old.ContainsKey($p.Id)) {
                    $o.Selected = $old[$p.Id].Selected -and $old[$p.Id].State -ne 'Ok'
                    if ($script:KeepStatus) { $o.State = $old[$p.Id].State; $o.Status = $old[$p.Id].Status }
                } else {
                    $o.Selected = -not $o.Ignored -and -not $o.Explicit
                }
                $Items.Add($o)
            }
            Update-Summary
        }
    }
}

$script:LastCount = -1
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(100)
$timer.Add_Tick({
    $m = $null; $k = 0
    while ($k -lt 500 -and $Q.TryDequeue([ref]$m)) { $k++; Invoke-Msg $m }
    if ($script:Job -and $script:Job.Handle.IsCompleted) {
        while ($Q.TryDequeue([ref]$m)) { Invoke-Msg $m }
        Complete-Background
    }
    Flush-Log
    $count = @($Items | Where-Object { $_.Selected -and -not $_.Ignored }).Count
    if ($count -ne $script:LastCount) { $script:LastCount = $count; $BtnUpdate.Content = "Update selected ($count)" }
})

# ---------------------------------------------------------------- events
$BtnRefresh.Add_Click({ Start-Refresh })
$BtnAll.Add_Click({  foreach ($o in $View) { if (-not $o.Ignored) { $o.Selected = $true } } })
$BtnNone.Add_Click({ foreach ($o in $Items) { $o.Selected = $false } })
$TxtFilter.Add_TextChanged({ $View.Refresh() })
$ChkShowIgnored.Add_Click({ $View.Refresh() })
$ChkSilent.Add_Click({ Save-Settings })
$ChkUnknown.Add_Click({ Start-Refresh })
$Grid.Add_MouseDoubleClick({ $o = $Grid.SelectedItem; if ($o -and -not $o.Ignored) { $o.Selected = -not $o.Selected } })

$BtnUpdate.Add_Click({
    $sel = @($Items | Where-Object { $_.Selected -and -not $_.Ignored })
    if (-not $sel.Count) {
        [System.Windows.MessageBox]::Show('Tick at least one app to update.', 'Winget Updater') | Out-Null
        return
    }
    foreach ($s in $sel) { $s.State = 'Queued'; $s.Status = 'Queued' }
    $Shared.Cancel = $false
    Save-Settings
    $script:AfterDone = { Start-Refresh -Quiet }
    Start-Background $UpdateWork @{ Ids = [string[]]@($sel | ForEach-Object { $_.Id }); Silent = [bool]$ChkSilent.IsChecked }
    $BtnCancel.Visibility = 'Visible'
})

$BtnCancel.Add_Click({
    $Shared.Cancel = $true
    $BtnCancel.IsEnabled = $false
    $ProgressText.Text = 'Stopping after the current app finishes...'
})

$menu = $Grid.ContextMenu.Items
$menu[0].Add_Click({
    foreach ($o in Get-RowTargets) { [void]$Ignored.Add($o.Id); $o.Ignored = $true; $o.Selected = $false }
    Save-Settings; $View.Refresh(); Update-Summary
})
$menu[1].Add_Click({
    foreach ($o in Get-RowTargets) { [void]$Ignored.Remove($o.Id); $o.Ignored = $false }
    Save-Settings; $View.Refresh(); Update-Summary
})
$menu[3].Add_Click({
    $ids = @(Get-RowTargets | ForEach-Object { $_.Id })
    if ($ids.Count) { [System.Windows.Clipboard]::SetText(($ids -join "`r`n")) }
})
$menu[4].Add_Click({
    $o = $Grid.SelectedItem
    if (-not $o -or $script:Job) { return }
    $window.FindName('LogBox').Parent.Parent.IsExpanded = $true
    Start-Background { Invoke-Winget @('show', '--id', $Id, '--exact', '--accept-source-agreements', '--disable-interactivity') | Out-Null } @{ Id = $o.Id }
})

$BtnOpenLogs.Add_Click({ Start-Process explorer.exe $LogDir })
$BtnClearLog.Add_Click({ $LogBox.Clear() })

$BtnAdmin.Add_Click({
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', ('"{0}"' -f $PSCommandPath))
        $window.Close()
    } catch { }
})

$window.Add_Closing({
    param($s, $e)
    if ($script:Job) {
        $r = [System.Windows.MessageBox]::Show('An operation is still running. Close anyway?', 'Winget Updater', 'YesNo', 'Warning')
        if ($r -ne 'Yes') { $e.Cancel = $true; return }
        try { $script:Job.PS.Stop() } catch { }
    }
    try { Save-Settings } catch { }
    $timer.Stop()
})

$window.Add_ContentRendered({ Start-Refresh })
$timer.Start()
[void]$window.ShowDialog()
