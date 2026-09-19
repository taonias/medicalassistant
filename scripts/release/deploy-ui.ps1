#!/usr/bin/env pwsh
# Deployment control panel: a WPF window wrapping package-release.ps1 + deploy-remote.ps1
# with a live step tracker instead of a scrolling console, plus read-only tabs for the VM's
# container status, required-secrets checklist, and recent service logs.
#
# Requires exactly what release.ps1 does: Docker Desktop running, the Posh-SSH module
# (Install-Module -Name Posh-SSH -Scope CurrentUser), and scripts/release/secrets.json
# (copy secrets.json.example and fill in the VM's connection details).
#
# Usage:
#   ./scripts/release/deploy-ui.ps1

param()

# Windows PowerShell (Desktop edition — what Explorer's "Run with PowerShell" launches) and
# PowerShell 7 (pwsh) keep separate module folders. Posh-SSH is normally only installed for
# whichever one you ran `Install-Module` from, so relaunch under pwsh here rather than making
# every SSH-dependent tab fail with "module not found" depending on how this was started.
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $pwshCmd = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwshCmd) {
        Start-Process -FilePath $pwshCmd.Source -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
        exit
    }
}

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

$ScriptRoot = $PSScriptRoot
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ScriptRoot)
$SecretsPath = Join-Path $ScriptRoot "secrets.json"
$ComposeFilePath = Join-Path $RepoRoot "docker-compose.prod.yml"
$EnvExamplePath = Join-Path $RepoRoot "ops/deploy/.env.prod.example"
$DeploymentDocPath = Join-Path $RepoRoot "ops/deploy/DEPLOYMENT.md"

. (Join-Path $ScriptRoot "PackageRelease.Checks.ps1")

# ============================================================================
# Generic background-async helper — runs $Action in its own runspace so the UI
# thread never blocks, then calls $OnComplete(result, errorMessage) on the UI
# thread once it finishes. $Action must only reference PowerShell built-ins and
# whatever is passed via -Variables (it runs in a fresh runspace, not a closure
# over this script's locals) — $OnComplete runs back here, so it can freely
# reference controls and outer variables.
# ============================================================================
$script:PendingAsyncOps = [System.Collections.Generic.List[object]]::new()

function Start-Async {
    param(
        [Parameter(Mandatory)][scriptblock]$Action,
        [Parameter(Mandatory)][scriptblock]$OnComplete,
        [hashtable]$Variables = @{}
    )

    $rs = [runspacefactory]::CreateRunspace()
    $rs.Open()
    foreach ($key in $Variables.Keys) {
        $rs.SessionStateProxy.SetVariable($key, $Variables[$key])
    }
    $ps = [powershell]::Create()
    $ps.Runspace = $rs
    [void]$ps.AddScript($Action)
    $handle = $ps.BeginInvoke()

    $script:PendingAsyncOps.Add([PSCustomObject]@{
        PS         = $ps
        Handle     = $handle
        OnComplete = $OnComplete
    })
}

function Update-PendingAsyncOps {
    for ($i = $script:PendingAsyncOps.Count - 1; $i -ge 0; $i--) {
        $op = $script:PendingAsyncOps[$i]
        if (-not $op.Handle.IsCompleted) { continue }

        $result = $null
        $errorMessage = $null
        try {
            $result = $op.PS.EndInvoke($op.Handle)
            if ($op.PS.HadErrors -and $op.PS.Streams.Error.Count -gt 0) {
                $errorMessage = ($op.PS.Streams.Error | ForEach-Object { $_.ToString() }) -join "`n"
            }
        } catch {
            $errorMessage = $_.Exception.Message
        } finally {
            $op.PS.Dispose()
            $op.PS.Runspace.Close()
        }

        $script:PendingAsyncOps.RemoveAt($i)
        & $op.OnComplete $result $errorMessage
    }
}

# Loads secrets.json the same way deploy-remote.ps1 does. Throws if missing/incomplete.
function Get-VmSecrets {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) {
        throw "Missing $Path. Copy secrets.json.example to secrets.json and fill in the VM's connection details."
    }
    $secrets = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($field in @("host", "user", "password", "remotePath")) {
        if (-not $secrets.$field) { throw "$Path is missing required field: $field" }
    }
    return $secrets
}

# ============================================================================
# XAML — window shell, styles, and the five tabs.
# ============================================================================
[xml]$xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Medical Assistant — Deploy" Height="800" Width="1180" MinHeight="600" MinWidth="900"
        WindowStartupLocation="CenterScreen" Background="#F5F6F8" FontFamily="Segoe UI">
  <Window.Resources>
    <SolidColorBrush x:Key="Accent" Color="#2F6FED"/>
    <SolidColorBrush x:Key="Green" Color="#1E8E5A"/>
    <SolidColorBrush x:Key="Red" Color="#D64545"/>
    <SolidColorBrush x:Key="Orange" Color="#E0A100"/>
    <SolidColorBrush x:Key="Muted" Color="#6B7280"/>
    <SolidColorBrush x:Key="CardBorder" Color="#E1E4E8"/>

    <Style TargetType="TabItem">
      <Setter Property="Padding" Value="16,10"/>
      <Setter Property="FontSize" Value="13"/>
    </Style>
    <Style x:Key="PrimaryButton" TargetType="Button">
      <Setter Property="Background" Value="{StaticResource Accent}"/>
      <Setter Property="Foreground" Value="White"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Padding" Value="18,10"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}" CornerRadius="6" Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
      <Style.Triggers>
        <Trigger Property="IsEnabled" Value="False">
          <Setter Property="Background" Value="#A9BEF0"/>
        </Trigger>
      </Style.Triggers>
    </Style>
    <Style x:Key="SecondaryButton" TargetType="Button">
      <Setter Property="Background" Value="White"/>
      <Setter Property="Foreground" Value="#1F2937"/>
      <Setter Property="Padding" Value="12,7"/>
      <Setter Property="BorderBrush" Value="{StaticResource CardBorder}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="6" Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="Card" TargetType="Border">
      <Setter Property="Background" Value="White"/>
      <Setter Property="BorderBrush" Value="{StaticResource CardBorder}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="CornerRadius" Value="8"/>
      <Setter Property="Padding" Value="16"/>
    </Style>
    <Style x:Key="StepIcon" TargetType="TextBlock">
      <Setter Property="FontSize" Value="16"/>
      <Setter Property="Width" Value="26"/>
      <Setter Property="TextAlignment" Value="Center"/>
      <Setter Property="Foreground" Value="#C4C9D1"/>
    </Style>
    <Style x:Key="StepLabel" TargetType="TextBlock">
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="FontSize" Value="13"/>
      <Setter Property="Foreground" Value="#1F2937"/>
    </Style>
    <Style x:Key="StepDetail" TargetType="TextBlock">
      <Setter Property="FontSize" Value="12"/>
      <Setter Property="Foreground" Value="{StaticResource Muted}"/>
      <Setter Property="Margin" Value="0,1,0,0"/>
    </Style>
  </Window.Resources>

  <TabControl Margin="12">
    <!-- ================= Deploy ================= -->
    <TabItem Header="Deploy">
      <Grid Margin="12">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="*"/>
          <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <Border Grid.Row="0" Style="{StaticResource Card}" Margin="0,0,0,12">
          <StackPanel Orientation="Horizontal">
            <TextBlock Text="Release tag" VerticalAlignment="Center" Margin="0,0,8,0" Foreground="{StaticResource Muted}"/>
            <TextBox x:Name="TagTextBox" Width="160" Text="latest" VerticalContentAlignment="Center" Padding="6,4" Margin="0,0,20,0"/>
            <CheckBox x:Name="SkipBuildCheckBox" Content="Skip build (reuse release/&lt;tag&gt;)" VerticalAlignment="Center" Margin="0,0,20,0"/>
            <CheckBox x:Name="NoLoadCheckBox" Content="Don't reload images on VM (--no-load)" VerticalAlignment="Center" Margin="0,0,20,0"/>
            <Button x:Name="DeployButton" Content="Deploy" Style="{StaticResource PrimaryButton}" Width="140" HorizontalAlignment="Right"/>
          </StackPanel>
        </Border>

        <TextBlock Grid.Row="1" x:Name="StatusBanner" Text="" FontWeight="SemiBold" Margin="4,0,0,10" TextWrapping="Wrap"/>

        <Grid Grid.Row="2">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="300"/>
            <ColumnDefinition Width="12"/>
            <ColumnDefinition Width="*"/>
          </Grid.ColumnDefinitions>

          <Border Grid.Column="0" Style="{StaticResource Card}">
            <StackPanel>
              <ProgressBar x:Name="OverallProgressBar" Height="8" Minimum="0" Maximum="100" Margin="0,0,0,16"/>

              <StackPanel Orientation="Horizontal" Margin="0,0,0,14">
                <TextBlock x:Name="PreflightIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel><TextBlock Style="{StaticResource StepLabel}" Text="1. Pre-flight check"/><TextBlock x:Name="PreflightDetail" Style="{StaticResource StepDetail}"/></StackPanel>
              </StackPanel>
              <StackPanel Orientation="Horizontal" Margin="0,0,0,14">
                <TextBlock x:Name="BuildIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel><TextBlock Style="{StaticResource StepLabel}" Text="2. Building Docker images"/><TextBlock x:Name="BuildDetail" Style="{StaticResource StepDetail}"/></StackPanel>
              </StackPanel>
              <StackPanel Orientation="Horizontal" Margin="0,0,0,14">
                <TextBlock x:Name="SaveIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel><TextBlock Style="{StaticResource StepLabel}" Text="3. Saving images to disk"/><TextBlock x:Name="SaveDetail" Style="{StaticResource StepDetail}"/></StackPanel>
              </StackPanel>
              <StackPanel Orientation="Horizontal" Margin="0,0,0,14">
                <TextBlock x:Name="PackageIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel><TextBlock Style="{StaticResource StepLabel}" Text="4. Packaging release bundle"/><TextBlock x:Name="PackageDetail" Style="{StaticResource StepDetail}"/></StackPanel>
              </StackPanel>
              <StackPanel Orientation="Horizontal" Margin="0,0,0,14">
                <TextBlock x:Name="ConnectIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel><TextBlock Style="{StaticResource StepLabel}" Text="5. Connecting to VM"/><TextBlock x:Name="ConnectDetail" Style="{StaticResource StepDetail}"/></StackPanel>
              </StackPanel>
              <StackPanel Orientation="Horizontal" Margin="0,0,0,14">
                <TextBlock x:Name="UploadIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel><TextBlock Style="{StaticResource StepLabel}" Text="6. Uploading to VM"/><TextBlock x:Name="UploadDetail" Style="{StaticResource StepDetail}"/></StackPanel>
              </StackPanel>
              <StackPanel Orientation="Horizontal">
                <TextBlock x:Name="DeployStepIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel><TextBlock Style="{StaticResource StepLabel}" Text="7. Deploying on VM"/><TextBlock x:Name="DeployStepDetail" Style="{StaticResource StepDetail}"/></StackPanel>
              </StackPanel>
            </StackPanel>
          </Border>

          <Border Grid.Column="2" Style="{StaticResource Card}" Padding="0">
            <TextBox x:Name="LogBox" IsReadOnly="True" Background="#1E1E1E" Foreground="#D4D4D4"
                     FontFamily="Consolas" FontSize="12" TextWrapping="NoWrap"
                     VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
                     BorderThickness="0" Padding="10"/>
          </Border>
        </Grid>
      </Grid>
    </TabItem>

    <!-- ================= Documentation ================= -->
    <TabItem Header="Documentation">
      <Border Style="{StaticResource Card}" Margin="12">
        <FlowDocumentScrollViewer x:Name="DocsViewer" VerticalScrollBarVisibility="Auto"/>
      </Border>
    </TabItem>

    <!-- ================= VM Status ================= -->
    <TabItem Header="VM Status">
      <Grid Margin="12">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
          <Button x:Name="VmStatusRefreshButton" Content="Refresh" Style="{StaticResource SecondaryButton}" Width="110"/>
          <TextBlock x:Name="VmStatusMessage" VerticalAlignment="Center" Margin="14,0,0,0" Foreground="{StaticResource Muted}"/>
        </StackPanel>
        <Border Grid.Row="1" Style="{StaticResource Card}">
          <ListView x:Name="VmStatusList" BorderThickness="0">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="Container" Width="300" DisplayMemberBinding="{Binding Name}"/>
                <GridViewColumn Header="State" Width="120" DisplayMemberBinding="{Binding State}"/>
                <GridViewColumn Header="Status" Width="400" DisplayMemberBinding="{Binding Status}"/>
              </GridView>
            </ListView.View>
          </ListView>
        </Border>
      </Grid>
    </TabItem>

    <!-- ================= Config Checklist ================= -->
    <TabItem Header="Config Checklist">
      <Grid Margin="12">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
          <Button x:Name="ConfigRefreshButton" Content="Refresh" Style="{StaticResource SecondaryButton}" Width="110"/>
          <TextBlock Text="Checks presence only — actual secret values never leave the VM." VerticalAlignment="Center" Margin="14,0,0,0" Foreground="{StaticResource Muted}"/>
        </StackPanel>
        <Border Grid.Row="1" Style="{StaticResource Card}">
          <ListView x:Name="ConfigList" BorderThickness="0">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="Variable" Width="320" DisplayMemberBinding="{Binding Key}"/>
                <GridViewColumn Header="Status" Width="300" DisplayMemberBinding="{Binding StatusText}"/>
              </GridView>
            </ListView.View>
          </ListView>
        </Border>
      </Grid>
    </TabItem>

    <!-- ================= Live Logs ================= -->
    <TabItem Header="Live Logs">
      <Grid Margin="12">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
          <TextBlock Text="Service" VerticalAlignment="Center" Margin="0,0,8,0" Foreground="{StaticResource Muted}"/>
          <ComboBox x:Name="LogsServiceCombo" Width="200" Margin="0,0,20,0"/>
          <TextBlock Text="Lines" VerticalAlignment="Center" Margin="0,0,8,0" Foreground="{StaticResource Muted}"/>
          <TextBox x:Name="LogsTailTextBox" Width="70" Text="200" Padding="6,4" Margin="0,0,20,0"/>
          <CheckBox x:Name="LogsHideHealthCheckBox" Content="Hide health-check noise" IsChecked="True" VerticalAlignment="Center" Margin="0,0,20,0"/>
          <Button x:Name="LogsFetchButton" Content="Fetch" Style="{StaticResource SecondaryButton}" Width="100"/>
          <TextBlock x:Name="LogsMessage" VerticalAlignment="Center" Margin="14,0,0,0" Foreground="{StaticResource Muted}"/>
        </StackPanel>
        <Border Grid.Row="1" Style="{StaticResource Card}" Padding="0">
          <TextBox x:Name="LogsOutputBox" IsReadOnly="True" Background="#1E1E1E" Foreground="#D4D4D4"
                   FontFamily="Consolas" FontSize="12" TextWrapping="NoWrap"
                   VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
                   BorderThickness="0" Padding="10"/>
        </Border>
      </Grid>
    </TabItem>
  </TabControl>
</Window>
'@

$reader = New-Object System.Xml.XmlNodeReader $xaml
$window = [Windows.Markup.XamlReader]::Load($reader)

function Get-Control([string]$Name) { return $window.FindName($Name) }

$TagTextBox            = Get-Control "TagTextBox"
$SkipBuildCheckBox      = Get-Control "SkipBuildCheckBox"
$NoLoadCheckBox         = Get-Control "NoLoadCheckBox"
$DeployButton           = Get-Control "DeployButton"
$StatusBanner           = Get-Control "StatusBanner"
$OverallProgressBar     = Get-Control "OverallProgressBar"
$LogBox                 = Get-Control "LogBox"
$DocsViewer             = Get-Control "DocsViewer"
$VmStatusRefreshButton  = Get-Control "VmStatusRefreshButton"
$VmStatusMessage        = Get-Control "VmStatusMessage"
$VmStatusList           = Get-Control "VmStatusList"
$ConfigRefreshButton    = Get-Control "ConfigRefreshButton"
$ConfigList             = Get-Control "ConfigList"
$LogsServiceCombo       = Get-Control "LogsServiceCombo"
$LogsTailTextBox        = Get-Control "LogsTailTextBox"
$LogsHideHealthCheckBox = Get-Control "LogsHideHealthCheckBox"
$LogsFetchButton        = Get-Control "LogsFetchButton"
$LogsMessage            = Get-Control "LogsMessage"
$LogsOutputBox          = Get-Control "LogsOutputBox"

$StepKeys = @("preflight", "build", "save", "package", "connect", "upload", "deploy")
$StepIcons = @{}
$StepDetails = @{}
$StepIcons["preflight"] = Get-Control "PreflightIcon";   $StepDetails["preflight"] = Get-Control "PreflightDetail"
$StepIcons["build"]     = Get-Control "BuildIcon";       $StepDetails["build"]     = Get-Control "BuildDetail"
$StepIcons["save"]      = Get-Control "SaveIcon";        $StepDetails["save"]      = Get-Control "SaveDetail"
$StepIcons["package"]   = Get-Control "PackageIcon";     $StepDetails["package"]   = Get-Control "PackageDetail"
$StepIcons["connect"]   = Get-Control "ConnectIcon";     $StepDetails["connect"]   = Get-Control "ConnectDetail"
$StepIcons["upload"]    = Get-Control "UploadIcon";      $StepDetails["upload"]    = Get-Control "UploadDetail"
$StepIcons["deploy"]    = Get-Control "DeployStepIcon";  $StepDetails["deploy"]    = Get-Control "DeployStepDetail"

$BrushPending = $window.Resources["CardBorder"]
$BrushRunning = $window.Resources["Accent"]
$BrushDone    = $window.Resources["Green"]
$BrushFailed  = $window.Resources["Red"]

function Set-StepVisual {
    param([string]$Key, [string]$Status, [string]$Detail)
    $icon = $StepIcons[$Key]
    $detailBlock = $StepDetails[$Key]
    switch ($Status) {
        "Running" { $icon.Text = "●"; $icon.Foreground = $BrushRunning }
        "Done"    { $icon.Text = "✓"; $icon.Foreground = $BrushDone }
        "Failed"  { $icon.Text = "✗"; $icon.Foreground = $BrushFailed }
        default   { $icon.Text = "○"; $icon.Foreground = $BrushPending }
    }
    $detailBlock.Text = $Detail
}

function Reset-DeploySteps {
    foreach ($key in $StepKeys) { Set-StepVisual -Key $key -Status "Pending" -Detail "" }
    $OverallProgressBar.Value = 0
    $LogBox.Clear()
    $StatusBanner.Text = ""
}

# ============================================================================
# Deploy tab — runs package-release.ps1 then deploy-remote.ps1 in a background
# runspace, reporting through $DeploySync (steps + a log line queue).
# ============================================================================
$DeploySync = [hashtable]::Synchronized(@{
    Steps    = [hashtable]::Synchronized(@{})
    LogQueue = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
    Running  = $false
    Result   = ""
    ErrorMessage = ""
})
foreach ($key in $StepKeys) { $DeploySync.Steps[$key] = @{ Status = "Pending"; Detail = "" } }

$script:DeployWasRunning = $false

$DeployButton.Add_Click({
    if ($DeploySync.Running) { return }

    $tag = $TagTextBox.Text.Trim()
    if (-not $tag) { $tag = "latest" }
    $confirm = [System.Windows.MessageBox]::Show(
        "Deploy tag '$tag' to production now?`n`nThis builds, uploads, and restarts the live site.",
        "Confirm deployment", "YesNo", "Warning")
    if ($confirm -ne "Yes") { return }

    Reset-DeploySteps
    foreach ($key in $StepKeys) { $DeploySync.Steps[$key] = @{ Status = "Pending"; Detail = "" } }
    $DeploySync.LogQueue = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
    $DeploySync.Running = $true
    $DeploySync.Result = ""
    $DeploySync.ErrorMessage = ""
    $script:DeployWasRunning = $true
    $DeployButton.IsEnabled = $false
    $StatusBanner.Text = "Deploying…"
    $StatusBanner.Foreground = $BrushRunning

    $rs = [runspacefactory]::CreateRunspace()
    $rs.Open()
    $rs.SessionStateProxy.SetVariable('sync', $DeploySync)
    $rs.SessionStateProxy.SetVariable('ScriptRoot', $ScriptRoot)
    $rs.SessionStateProxy.SetVariable('Tag', $tag)
    $rs.SessionStateProxy.SetVariable('SkipBuild', [bool]$SkipBuildCheckBox.IsChecked)
    $rs.SessionStateProxy.SetVariable('NoLoad', [bool]$NoLoadCheckBox.IsChecked)

    $ps = [powershell]::Create()
    $ps.Runspace = $rs
    [void]$ps.AddScript({
        $onStep = { param($Stage, $Status, $Detail) $sync.Steps[$Stage] = @{ Status = $Status; Detail = $Detail } }
        $onLine = { param($Line) $sync.LogQueue.Enqueue($Line) }
        try {
            $packageArgs = @{ Tag = $Tag; OnStep = $onStep; OnLine = $onLine }
            if ($SkipBuild) { $packageArgs.SkipBuild = $true }
            & (Join-Path $ScriptRoot "package-release.ps1") @packageArgs

            $deployArgs = @{ Tag = $Tag; OnStep = $onStep; OnLine = $onLine }
            if ($NoLoad) { $deployArgs.NoLoad = $true }
            & (Join-Path $ScriptRoot "deploy-remote.ps1") @deployArgs

            $sync.Result = "success"
        } catch {
            $sync.Result = "error"
            $sync.ErrorMessage = $_.Exception.Message
        } finally {
            $sync.Running = $false
        }
    })
    [void]$ps.BeginInvoke()
    $script:DeployPs = $ps
})

# ============================================================================
# Documentation tab — a small line-based Markdown-to-FlowDocument renderer.
# Covers what DEPLOYMENT.md actually uses: headers, code fences, bullets,
# bold/inline-code spans, and links. Anything fancier just renders as plain text.
# ============================================================================
function Add-InlineRuns {
    param([System.Windows.Documents.Paragraph]$Paragraph, [string]$Text)
    $pattern = '\*\*(.+?)\*\*|`([^`]+?)`|\[([^\]]+)\]\(([^)]+)\)'
    $lastIndex = 0
    foreach ($m in [regex]::Matches($Text, $pattern)) {
        if ($m.Index -gt $lastIndex) {
            $Paragraph.Inlines.Add((New-Object System.Windows.Documents.Run($Text.Substring($lastIndex, $m.Index - $lastIndex))))
        }
        if ($m.Groups[1].Success) {
            $run = New-Object System.Windows.Documents.Run($m.Groups[1].Value)
            $run.FontWeight = "Bold"
            $Paragraph.Inlines.Add($run)
        } elseif ($m.Groups[2].Success) {
            $run = New-Object System.Windows.Documents.Run($m.Groups[2].Value)
            $run.FontFamily = "Consolas"
            $run.Background = [System.Windows.Media.Brushes]::WhiteSmoke
            $Paragraph.Inlines.Add($run)
        } elseif ($m.Groups[3].Success) {
            $link = New-Object System.Windows.Documents.Hyperlink((New-Object System.Windows.Documents.Run($m.Groups[3].Value)))
            $url = $m.Groups[4].Value
            $link.Add_Click({ try { Start-Process $url } catch {} }.GetNewClosure())
            $Paragraph.Inlines.Add($link)
        }
        $lastIndex = $m.Index + $m.Length
    }
    if ($lastIndex -lt $Text.Length) {
        $Paragraph.Inlines.Add((New-Object System.Windows.Documents.Run($Text.Substring($lastIndex))))
    }
}

function ConvertTo-FlowDocument {
    param([string]$Path)

    $doc = New-Object System.Windows.Documents.FlowDocument
    $doc.FontFamily = "Segoe UI"
    $doc.FontSize = 13
    $doc.PagePadding = New-Object System.Windows.Thickness(4)

    if (-not (Test-Path $Path)) {
        $p = New-Object System.Windows.Documents.Paragraph
        $p.Inlines.Add("$Path not found.")
        $doc.Blocks.Add($p)
        return $doc
    }

    $inCode = $false
    $codeParagraph = $null

    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine

        if ($line -match '^\s*```') {
            if ($inCode) {
                $inCode = $false
                $codeParagraph = $null
            } else {
                $inCode = $true
                $codeParagraph = New-Object System.Windows.Documents.Paragraph
                $codeParagraph.FontFamily = "Consolas"
                $codeParagraph.FontSize = 12
                $codeParagraph.Background = [System.Windows.Media.Brushes]::WhiteSmoke
                $codeParagraph.Padding = New-Object System.Windows.Thickness(8)
                $codeParagraph.Margin = New-Object System.Windows.Thickness(0, 4, 0, 4)
                $doc.Blocks.Add($codeParagraph)
            }
            continue
        }

        if ($inCode) {
            $run = New-Object System.Windows.Documents.Run("$line`n")
            $codeParagraph.Inlines.Add($run)
            continue
        }

        if ($line -match '^(#{1,3})\s+(.*)$') {
            $level = $Matches[1].Length
            $p = New-Object System.Windows.Documents.Paragraph
            $p.FontWeight = "Bold"
            $p.FontSize = switch ($level) { 1 { 20 } 2 { 17 } default { 15 } }
            $p.Margin = New-Object System.Windows.Thickness(0, 14, 0, 6)
            $p.Foreground = [System.Windows.Media.Brushes]::Black
            Add-InlineRuns -Paragraph $p -Text $Matches[2]
            $doc.Blocks.Add($p)
            continue
        }

        if ($line -match '^\s*[-*]\s+(.*)$') {
            $p = New-Object System.Windows.Documents.Paragraph
            $p.Margin = New-Object System.Windows.Thickness(16, 1, 0, 1)
            $bullet = New-Object System.Windows.Documents.Run("• ")
            $p.Inlines.Add($bullet)
            Add-InlineRuns -Paragraph $p -Text $Matches[1]
            $doc.Blocks.Add($p)
            continue
        }

        if ($line -match '^\s*---+\s*$') {
            $p = New-Object System.Windows.Documents.Paragraph
            $p.BorderBrush = [System.Windows.Media.Brushes]::LightGray
            $p.BorderThickness = New-Object System.Windows.Thickness(0, 0, 0, 1)
            $p.Margin = New-Object System.Windows.Thickness(0, 8, 0, 8)
            $doc.Blocks.Add($p)
            continue
        }

        if ($line.Trim() -eq "") {
            continue
        }

        $p = New-Object System.Windows.Documents.Paragraph
        $p.Margin = New-Object System.Windows.Thickness(0, 1, 0, 1)
        Add-InlineRuns -Paragraph $p -Text $line
        $doc.Blocks.Add($p)
    }

    return $doc
}

$DocsViewer.Document = ConvertTo-FlowDocument -Path $DeploymentDocPath

# ============================================================================
# VM Status tab
# ============================================================================
$VmStatusRefreshButton.Add_Click({
    $VmStatusRefreshButton.IsEnabled = $false
    $VmStatusMessage.Text = "Connecting…"
    $VmStatusMessage.Foreground = $window.Resources["Muted"]

    $action = {
        Import-Module Posh-SSH -ErrorAction Stop
        $secrets = Get-Content -LiteralPath $SecretsPath -Raw | ConvertFrom-Json
        $securePw = ConvertTo-SecureString $secrets.password -AsPlainText -Force
        $cred = New-Object System.Management.Automation.PSCredential($secrets.user, $securePw)
        $port = if ($secrets.port) { [int]$secrets.port } else { 22 }
        $session = New-SSHSession -ComputerName $secrets.host -Port $port -Credential $cred -AcceptKey
        try {
            $cmd = "cd '$($secrets.remotePath)' && docker compose -f docker-compose.prod.yml --env-file .env.prod ps --format json"
            $result = Invoke-SSHCommand -SSHSession $session -Command $cmd
            return $result.Output -join "`n"
        } finally {
            Remove-SSHSession -SSHSession $session | Out-Null
        }
    }

    Start-Async -Variables @{ SecretsPath = $SecretsPath } -Action $action -OnComplete {
        param($result, $errorMessage)
        $VmStatusRefreshButton.IsEnabled = $true
        if ($errorMessage) {
            $VmStatusMessage.Text = "Failed: $errorMessage"
            $VmStatusMessage.Foreground = $BrushFailed
            return
        }
        $rows = [System.Collections.Generic.List[object]]::new()
        foreach ($jsonLine in ($result -split "`n" | Where-Object { $_.Trim() })) {
            try {
                $obj = $jsonLine | ConvertFrom-Json
                $rows.Add([PSCustomObject]@{
                    Name   = $obj.Name
                    State  = $obj.State
                    Status = $obj.Status
                })
            } catch { }
        }
        if ($rows.Count -eq 0) {
            # Some compose versions emit one JSON array instead of one object per line.
            try {
                foreach ($obj in ($result | ConvertFrom-Json)) {
                    $rows.Add([PSCustomObject]@{ Name = $obj.Name; State = $obj.State; Status = $obj.Status })
                }
            } catch { }
        }
        $VmStatusList.ItemsSource = $rows
        $VmStatusMessage.Text = "Updated $(Get-Date -Format 'HH:mm:ss') — $($rows.Count) container(s)"
        $VmStatusMessage.Foreground = $window.Resources["Muted"]
    }
})

# ============================================================================
# Config Checklist tab — presence-only check; actual secret values never
# leave the VM (the remote shell script only ever echoes SET/DEFAULT/MISSING).
# ============================================================================
function Get-RequiredEnvVars {
    param([string]$Path)
    $vars = [System.Collections.Generic.List[object]]::new()
    $inRequired = $false
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^# ---\s*(.+?)\s*-{2,}\s*$') {
            $inRequired = $Matches[1] -match '\(REQUIRED'
            continue
        }
        if ($inRequired -and $line -match '^([A-Z0-9_]+)=(.*)$') {
            $vars.Add([PSCustomObject]@{ Key = $Matches[1]; Default = $Matches[2] })
        }
    }
    return $vars.ToArray()
}

function Build-RemoteEnvCheckScript {
    param([string]$RemotePath, [object[]]$RequiredVars)
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("cd '$RemotePath' || { echo 'NO_DIR'; exit 0; }")
    $lines.Add("if [ ! -f .env.prod ]; then echo 'NO_ENV_FILE'; exit 0; fi")
    foreach ($v in $RequiredVars) {
        $key = $v.Key
        $default = ($v.Default -replace "'", "'\''")
        $lines.Add("line=`$(grep -m1 '^$key=' .env.prod || true)")
        $lines.Add("if [ -z `"`$line`" ]; then echo '$key|MISSING'; else val=`"`${line#$key=}`"; if [ -z `"`$val`" ]; then echo '$key|EMPTY'; elif [ `"`$val`" = '$default' ]; then echo '$key|DEFAULT'; else echo '$key|SET'; fi; fi")
    }
    return ($lines -join "`n")
}

$RequiredEnvVars = Get-RequiredEnvVars -Path $EnvExamplePath

$ConfigRefreshButton.Add_Click({
    $ConfigRefreshButton.IsEnabled = $false

    $secretsForCmd = $null
    try { $secretsForCmd = Get-VmSecrets -Path $SecretsPath } catch {
        $ConfigList.ItemsSource = @([PSCustomObject]@{ Key = "secrets.json"; StatusText = $_.Exception.Message })
        $ConfigRefreshButton.IsEnabled = $true
        return
    }
    $remoteScript = Build-RemoteEnvCheckScript -RemotePath $secretsForCmd.remotePath -RequiredVars $RequiredEnvVars

    $action = {
        Import-Module Posh-SSH -ErrorAction Stop
        $secrets = Get-Content -LiteralPath $SecretsPath -Raw | ConvertFrom-Json
        $securePw = ConvertTo-SecureString $secrets.password -AsPlainText -Force
        $cred = New-Object System.Management.Automation.PSCredential($secrets.user, $securePw)
        $port = if ($secrets.port) { [int]$secrets.port } else { 22 }
        $session = New-SSHSession -ComputerName $secrets.host -Port $port -Credential $cred -AcceptKey
        try {
            $result = Invoke-SSHCommand -SSHSession $session -Command $RemoteScript
            return $result.Output -join "`n"
        } finally {
            Remove-SSHSession -SSHSession $session | Out-Null
        }
    }

    Start-Async -Variables @{ SecretsPath = $SecretsPath; RemoteScript = $remoteScript } -Action $action -OnComplete {
        param($result, $errorMessage)
        $ConfigRefreshButton.IsEnabled = $true
        if ($errorMessage) {
            $ConfigList.ItemsSource = @([PSCustomObject]@{ Key = "Error"; StatusText = $errorMessage })
            return
        }
        if ($result -match 'NO_ENV_FILE') {
            $ConfigList.ItemsSource = @([PSCustomObject]@{ Key = ".env.prod"; StatusText = "Not found on the VM yet" })
            return
        }
        $statusByKey = @{}
        foreach ($line in ($result -split "`n")) {
            if ($line -match '^([A-Z0-9_]+)\|(SET|DEFAULT|MISSING|EMPTY)$') {
                $statusByKey[$Matches[1]] = $Matches[2]
            }
        }
        $rows = [System.Collections.Generic.List[object]]::new()
        foreach ($v in $RequiredEnvVars) {
            $status = if ($statusByKey.ContainsKey($v.Key)) { $statusByKey[$v.Key] } else { "MISSING" }
            $text = switch ($status) {
                "SET"     { "✓ set" }
                "DEFAULT" { "⚠ still the example placeholder — change this" }
                "EMPTY"   { "⚠ present but empty" }
                default   { "✗ missing" }
            }
            $rows.Add([PSCustomObject]@{ Key = $v.Key; StatusText = $text })
        }
        $ConfigList.ItemsSource = $rows
    }
})

# ============================================================================
# Live Logs tab
# ============================================================================
try {
    foreach ($service in (Get-ComposeServiceNames -ComposeFilePath $ComposeFilePath)) {
        [void]$LogsServiceCombo.Items.Add($service)
    }
    if ($LogsServiceCombo.Items.Count -gt 0) { $LogsServiceCombo.SelectedIndex = 0 }
} catch { }

$LogsFetchButton.Add_Click({
    $service = $LogsServiceCombo.SelectedItem
    if (-not $service) { return }
    $tail = $LogsTailTextBox.Text.Trim()
    if (-not ($tail -match '^\d+$')) { $tail = "200" }
    $hideHealth = [bool]$LogsHideHealthCheckBox.IsChecked

    $LogsFetchButton.IsEnabled = $false
    $LogsMessage.Text = "Fetching…"
    $LogsMessage.Foreground = $window.Resources["Muted"]

    $action = {
        Import-Module Posh-SSH -ErrorAction Stop
        $secrets = Get-Content -LiteralPath $SecretsPath -Raw | ConvertFrom-Json
        $securePw = ConvertTo-SecureString $secrets.password -AsPlainText -Force
        $cred = New-Object System.Management.Automation.PSCredential($secrets.user, $securePw)
        $port = if ($secrets.port) { [int]$secrets.port } else { 22 }
        $session = New-SSHSession -ComputerName $secrets.host -Port $port -Credential $cred -AcceptKey
        try {
            $cmd = "cd '$($secrets.remotePath)' && docker compose -f docker-compose.prod.yml --env-file .env.prod logs --tail=$Tail --no-color $ServiceName"
            if ($HideHealth) { $cmd += " | grep -v '/health/live'" }
            $result = Invoke-SSHCommand -SSHSession $session -Command $cmd
            return $result.Output -join "`n"
        } finally {
            Remove-SSHSession -SSHSession $session | Out-Null
        }
    }

    Start-Async -Variables @{ SecretsPath = $SecretsPath; Tail = $tail; ServiceName = $service; HideHealth = $hideHealth } -Action $action -OnComplete {
        param($result, $errorMessage)
        $LogsFetchButton.IsEnabled = $true
        if ($errorMessage) {
            $LogsMessage.Text = "Failed: $errorMessage"
            $LogsMessage.Foreground = $BrushFailed
            return
        }
        $LogsOutputBox.Text = $result
        $LogsOutputBox.ScrollToEnd()
        $LogsMessage.Text = "Updated $(Get-Date -Format 'HH:mm:ss')"
        $LogsMessage.Foreground = $window.Resources["Muted"]
    }
})

# ============================================================================
# Main polling loop — drains the deploy log queue, refreshes step visuals, and
# resolves any pending background ops (VM Status / Config / Logs refreshes).
# ============================================================================
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(200)
$timer.Add_Tick({
    $line = $null
    $appended = $false
    while ($DeploySync.LogQueue.TryDequeue([ref]$line)) {
        $LogBox.AppendText("$line`r`n")
        $appended = $true
    }
    if ($appended) { $LogBox.ScrollToEnd() }

    $doneCount = 0
    foreach ($key in $StepKeys) {
        $info = $DeploySync.Steps[$key]
        Set-StepVisual -Key $key -Status $info.Status -Detail $info.Detail
        if ($info.Status -eq "Done") { $doneCount += 1 }
    }
    $OverallProgressBar.Value = ($doneCount / $StepKeys.Count) * 100

    if (-not $DeploySync.Running -and $script:DeployWasRunning) {
        $script:DeployWasRunning = $false
        $DeployButton.IsEnabled = $true
        if ($DeploySync.Result -eq "success") {
            $StatusBanner.Text = "Deployment complete."
            $StatusBanner.Foreground = $BrushDone
        } elseif ($DeploySync.Result -eq "error") {
            $StatusBanner.Text = "Deployment failed: $($DeploySync.ErrorMessage)"
            $StatusBanner.Foreground = $BrushFailed
        }
    }

    Update-PendingAsyncOps
})
$timer.Start()

$window.Add_Closed({ $timer.Stop() })

[void]$window.ShowDialog()
