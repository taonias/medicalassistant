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
$ExposedServicesDocPath = Join-Path $RepoRoot "ops/deploy/EXPOSED-SERVICES.md"
# Local staging copy for the optional "also upload .env.prod" checkbox — never committed
# (see .gitignore's **/.env.prod rule), lives next to secrets.json for the same reason.
$LocalEnvProdPath = Join-Path $ScriptRoot ".env.prod"
# Source of truth it's synced from — the repo-root .env (also gitignored) you already
# maintain for local dev; re-copied fresh every time the checkbox is used, so the two files
# can't quietly drift apart the way the VM's .env.prod and its connection string just did.
$RootEnvPath = Join-Path $RepoRoot ".env"

. (Join-Path $ScriptRoot "PackageRelease.Checks.ps1")

# Every tab reads $SecretsPath directly (several from background runspaces, which can't
# share a function call with the main thread) — ensuring the file exists here, once, before
# the window even opens means none of them need their own fallback logic.
try {
    Get-OrCreateVmSecretsFile -SecretsPath $SecretsPath -EnvPath $RootEnvPath
} catch {
    [System.Windows.MessageBox]::Show($_.Exception.Message, "Can't find VM connection details", "OK", "Error") | Out-Null
    exit 1
}

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

  <TabControl x:Name="MainTabControl" Margin="12">
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
          <StackPanel>
            <WrapPanel>
              <TextBlock Text="Release tag" VerticalAlignment="Center" Margin="0,0,8,0" Foreground="{StaticResource Muted}"/>
              <TextBox x:Name="TagTextBox" Width="160" Text="latest" VerticalContentAlignment="Center" Padding="6,4" Margin="0,0,20,0"/>
              <CheckBox x:Name="SkipBuildCheckBox" Content="Skip build (reuse release/&lt;tag&gt;)" VerticalAlignment="Center" Margin="0,0,20,10"/>
              <CheckBox x:Name="NoLoadCheckBox" Content="Don't reload images on VM (--no-load)" VerticalAlignment="Center" Margin="0,0,20,10"/>
              <Button x:Name="DeployButton" Content="Deploy" Style="{StaticResource PrimaryButton}" Width="140" Margin="0,0,0,10"/>
            </WrapPanel>
            <Separator Margin="0,2,0,10" Opacity="0.4"/>
            <WrapPanel>
              <CheckBox x:Name="UploadEnvCheckBox" VerticalAlignment="Center"/>
              <TextBlock VerticalAlignment="Center" Margin="6,0,16,10">
                <Run Text="⚠ Also build &amp; upload .env.prod from" FontWeight="SemiBold" Foreground="{StaticResource Orange}"/>
                <Run x:Name="EnvProdPathRun" Text=".env" FontFamily="Consolas" FontSize="12"/>
                <Run Text="(overwrites the VM's secrets — off by default)" Foreground="{StaticResource Muted}"/>
              </TextBlock>
              <Button x:Name="UploadEnvOnlyButton" Content="Upload .env.prod only (no deploy)" Style="{StaticResource SecondaryButton}"
                      Padding="8,4" FontSize="11" Margin="0,0,0,10"/>
            </WrapPanel>
          </StackPanel>
        </Border>

        <TextBox Grid.Row="1" x:Name="StatusBanner" Text="" FontWeight="SemiBold" Margin="4,0,0,10"
                 TextWrapping="Wrap" IsReadOnly="True" BorderThickness="0" Background="Transparent"
                 MaxHeight="130" VerticalScrollBarVisibility="Auto" IsReadOnlyCaretVisible="True"/>

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
                <StackPanel Width="240">
                  <TextBlock Style="{StaticResource StepLabel}" Text="6. Uploading to VM"/>
                  <TextBlock x:Name="UploadDetail" Style="{StaticResource StepDetail}" TextWrapping="Wrap"/>
                  <ProgressBar x:Name="UploadProgressBar" Height="6" Minimum="0" Maximum="100" Margin="0,4,0,0" Visibility="Collapsed"/>
                </StackPanel>
              </StackPanel>
              <StackPanel Orientation="Horizontal">
                <TextBlock x:Name="DeployStepIcon" Style="{StaticResource StepIcon}" Text="○"/>
                <StackPanel>
                  <TextBlock Style="{StaticResource StepLabel}" Text="7. Deploying on VM"/>
                  <TextBlock x:Name="DeployStepDetail" Style="{StaticResource StepDetail}" TextWrapping="Wrap"/>
                  <Button x:Name="RerunDeployButton" Content="Rerun deploy.sh only" Style="{StaticResource SecondaryButton}"
                          HorizontalAlignment="Left" Padding="8,4" Margin="0,6,0,0" FontSize="11"/>
                </StackPanel>
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

    <!-- ================= Exposed Services ================= -->
    <TabItem Header="Exposed Services">
      <Grid Margin="12">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row="0" Margin="0,0,0,12">
          <StackPanel Orientation="Horizontal">
            <Button x:Name="ExposedServicesEditButton" Content="Open EXPOSED-SERVICES.md" Style="{StaticResource SecondaryButton}" Padding="10,6"/>
            <TextBlock x:Name="ExposedServicesMessage" VerticalAlignment="Center" Margin="14,0,0,0" Foreground="{StaticResource Orange}"/>
          </StackPanel>
          <TextBlock Margin="0,6,0,0" FontSize="11" Foreground="{StaticResource Red}" TextWrapping="Wrap"
                     Text="⚠ This tab fills in real passwords and keys from your local .env. Don't screen-share or screenshot it."/>
        </StackPanel>
        <Border Grid.Row="1" Style="{StaticResource Card}">
          <FlowDocumentScrollViewer x:Name="ExposedServicesViewer" VerticalScrollBarVisibility="Auto"/>
        </Border>
      </Grid>
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

    <!-- ================= Recreate ================= -->
    <TabItem Header="Recreate">
      <Grid Margin="12">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Margin="0,0,0,12" TextWrapping="Wrap" Foreground="{StaticResource Muted}">
          Recreates one container from the image and .env.prod already on the VM
          (<Run FontFamily="Consolas" Text="docker compose up -d --force-recreate &lt;service&gt;"/>) —
          use this when you've fixed .env.prod on the VM and the running container needs to pick up
          the change, without touching any other service. A few seconds of downtime for that one
          container; everything else on the VM keeps running.
        </TextBlock>

        <Border Grid.Row="1" Style="{StaticResource Card}" Margin="0,0,0,12">
          <WrapPanel x:Name="RecreateServicesPanel"/>
        </Border>

        <Border Grid.Row="2" Style="{StaticResource Card}" Padding="0">
          <TextBox x:Name="RecreateOutputBox" IsReadOnly="True" Background="#1E1E1E" Foreground="#D4D4D4"
                   FontFamily="Consolas" FontSize="12" TextWrapping="NoWrap"
                   VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
                   BorderThickness="0" Padding="10"/>
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

    <!-- ================= Server ================= -->
    <TabItem x:Name="ServerTabItem" Header="Server">
      <ScrollViewer VerticalScrollBarVisibility="Auto" Margin="12">
        <StackPanel>
          <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
            <Button x:Name="ServerRefreshButton" Content="Refresh now" Style="{StaticResource SecondaryButton}" Width="110"/>
            <TextBlock x:Name="ServerAutoRefreshText" Text="Auto-refreshing every 4s while this tab is open" VerticalAlignment="Center" Margin="14,0,0,0" Foreground="{StaticResource Muted}"/>
            <TextBlock x:Name="ServerMessage" VerticalAlignment="Center" Margin="14,0,0,0" Foreground="{StaticResource Muted}"/>
          </StackPanel>

          <WrapPanel>
            <Border Style="{StaticResource Card}" Width="220" Margin="0,0,12,12">
              <StackPanel>
                <TextBlock Text="CPU" FontWeight="SemiBold" Foreground="{StaticResource Muted}" FontSize="12"/>
                <TextBlock x:Name="CpuPctText" Text="—" FontSize="32" FontWeight="Bold" Margin="0,2,0,6"/>
                <ProgressBar x:Name="CpuBar" Height="8" Minimum="0" Maximum="100" Value="0"/>
                <TextBlock x:Name="CpuSubText" Text="" FontSize="11" Foreground="{StaticResource Muted}" Margin="0,6,0,0"/>
              </StackPanel>
            </Border>
            <Border Style="{StaticResource Card}" Width="220" Margin="0,0,12,12">
              <StackPanel>
                <TextBlock Text="MEMORY" FontWeight="SemiBold" Foreground="{StaticResource Muted}" FontSize="12"/>
                <TextBlock x:Name="MemPctText" Text="—" FontSize="32" FontWeight="Bold" Margin="0,2,0,6"/>
                <ProgressBar x:Name="MemBar" Height="8" Minimum="0" Maximum="100" Value="0"/>
                <TextBlock x:Name="MemSubText" Text="" FontSize="11" Foreground="{StaticResource Muted}" Margin="0,6,0,0"/>
              </StackPanel>
            </Border>
            <Border Style="{StaticResource Card}" Width="220" Margin="0,0,12,12">
              <StackPanel>
                <TextBlock Text="DISK (/)" FontWeight="SemiBold" Foreground="{StaticResource Muted}" FontSize="12"/>
                <TextBlock x:Name="DiskPctText" Text="—" FontSize="32" FontWeight="Bold" Margin="0,2,0,6"/>
                <ProgressBar x:Name="DiskBar" Height="8" Minimum="0" Maximum="100" Value="0"/>
                <TextBlock x:Name="DiskSubText" Text="" FontSize="11" Foreground="{StaticResource Muted}" Margin="0,6,0,0"/>
              </StackPanel>
            </Border>
            <Border Style="{StaticResource Card}" Width="220" Margin="0,0,12,12">
              <StackPanel>
                <TextBlock Text="SWAP" FontWeight="SemiBold" Foreground="{StaticResource Muted}" FontSize="12"/>
                <TextBlock x:Name="SwapPctText" Text="—" FontSize="32" FontWeight="Bold" Margin="0,2,0,6"/>
                <ProgressBar x:Name="SwapBar" Height="8" Minimum="0" Maximum="100" Value="0"/>
                <TextBlock x:Name="SwapSubText" Text="" FontSize="11" Foreground="{StaticResource Muted}" Margin="0,6,0,0"/>
              </StackPanel>
            </Border>
          </WrapPanel>

          <WrapPanel>
            <Border Style="{StaticResource Card}" Width="340" Margin="0,0,12,12" VerticalAlignment="Top">
              <StackPanel>
                <TextBlock Text="System" FontWeight="SemiBold" Margin="0,0,0,10"/>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Hostname" Width="110" Foreground="{StaticResource Muted}"/><TextBlock x:Name="InfoHostname" Text="—"/></StackPanel>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="OS" Width="110" Foreground="{StaticResource Muted}"/><TextBlock x:Name="InfoOs" Text="—"/></StackPanel>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Kernel" Width="110" Foreground="{StaticResource Muted}"/><TextBlock x:Name="InfoKernel" Text="—"/></StackPanel>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="CPU" Width="110" Foreground="{StaticResource Muted}"/><TextBlock x:Name="InfoCpuModel" Text="—" TextWrapping="Wrap"/></StackPanel>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Cores" Width="110" Foreground="{StaticResource Muted}"/><TextBlock x:Name="InfoCores" Text="—"/></StackPanel>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Uptime" Width="110" Foreground="{StaticResource Muted}"/><TextBlock x:Name="InfoUptime" Text="—"/></StackPanel>
                <StackPanel Orientation="Horizontal"><TextBlock Text="Load avg" Width="110" Foreground="{StaticResource Muted}"/><TextBlock x:Name="InfoLoadAvg" Text="—"/></StackPanel>
              </StackPanel>
            </Border>
            <Border Style="{StaticResource Card}" Width="480" Margin="0,0,12,12" Padding="0" VerticalAlignment="Top">
              <StackPanel>
                <TextBlock Text="Docker disk usage" FontWeight="SemiBold" Margin="16,16,16,8"/>
                <TextBox x:Name="DockerDfBox" Text="—" IsReadOnly="True" BorderThickness="0" Background="#1E1E1E" Foreground="#D4D4D4"
                         FontFamily="Consolas" FontSize="12" TextWrapping="NoWrap" Padding="16"
                         HorizontalScrollBarVisibility="Auto"/>
              </StackPanel>
            </Border>
          </WrapPanel>
        </StackPanel>
      </ScrollViewer>
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
$UploadEnvCheckBox      = Get-Control "UploadEnvCheckBox"
$UploadEnvOnlyButton    = Get-Control "UploadEnvOnlyButton"
$DeployButton           = Get-Control "DeployButton"
$RerunDeployButton      = Get-Control "RerunDeployButton"
$StatusBanner           = Get-Control "StatusBanner"
$OverallProgressBar     = Get-Control "OverallProgressBar"
$LogBox                 = Get-Control "LogBox"
$DocsViewer             = Get-Control "DocsViewer"
$ExposedServicesViewer      = Get-Control "ExposedServicesViewer"
$ExposedServicesEditButton  = Get-Control "ExposedServicesEditButton"
$ExposedServicesMessage     = Get-Control "ExposedServicesMessage"
$VmStatusRefreshButton  = Get-Control "VmStatusRefreshButton"
$VmStatusMessage        = Get-Control "VmStatusMessage"
$VmStatusList           = Get-Control "VmStatusList"
$ConfigRefreshButton    = Get-Control "ConfigRefreshButton"
$ConfigList             = Get-Control "ConfigList"
$RecreateServicesPanel  = Get-Control "RecreateServicesPanel"
$RecreateOutputBox      = Get-Control "RecreateOutputBox"
$LogsServiceCombo       = Get-Control "LogsServiceCombo"
$LogsTailTextBox        = Get-Control "LogsTailTextBox"
$LogsHideHealthCheckBox = Get-Control "LogsHideHealthCheckBox"
$LogsFetchButton        = Get-Control "LogsFetchButton"
$LogsMessage            = Get-Control "LogsMessage"
$LogsOutputBox          = Get-Control "LogsOutputBox"

$MainTabControl         = Get-Control "MainTabControl"
$ServerTabItem          = Get-Control "ServerTabItem"
$ServerRefreshButton    = Get-Control "ServerRefreshButton"
$ServerMessage          = Get-Control "ServerMessage"
$CpuPctText  = Get-Control "CpuPctText";  $CpuBar  = Get-Control "CpuBar";  $CpuSubText  = Get-Control "CpuSubText"
$MemPctText  = Get-Control "MemPctText";  $MemBar  = Get-Control "MemBar";  $MemSubText  = Get-Control "MemSubText"
$DiskPctText = Get-Control "DiskPctText"; $DiskBar = Get-Control "DiskBar"; $DiskSubText = Get-Control "DiskSubText"
$SwapPctText = Get-Control "SwapPctText"; $SwapBar = Get-Control "SwapBar"; $SwapSubText = Get-Control "SwapSubText"
$InfoHostname = Get-Control "InfoHostname"; $InfoOs       = Get-Control "InfoOs"
$InfoKernel   = Get-Control "InfoKernel";   $InfoCpuModel = Get-Control "InfoCpuModel"
$InfoCores    = Get-Control "InfoCores";    $InfoUptime   = Get-Control "InfoUptime"
$InfoLoadAvg  = Get-Control "InfoLoadAvg"
$DockerDfBox  = Get-Control "DockerDfBox"

$StepKeys = @("preflight", "build", "save", "package", "connect", "upload", "deploy")
$StepIcons = @{}
$StepDetails = @{}
$StepIcons["preflight"] = Get-Control "PreflightIcon";   $StepDetails["preflight"] = Get-Control "PreflightDetail"
$StepIcons["build"]     = Get-Control "BuildIcon";       $StepDetails["build"]     = Get-Control "BuildDetail"
$StepIcons["save"]      = Get-Control "SaveIcon";        $StepDetails["save"]      = Get-Control "SaveDetail"
$StepIcons["package"]   = Get-Control "PackageIcon";     $StepDetails["package"]   = Get-Control "PackageDetail"
$StepIcons["connect"]   = Get-Control "ConnectIcon";     $StepDetails["connect"]   = Get-Control "ConnectDetail"
$StepIcons["upload"]    = Get-Control "UploadIcon";      $StepDetails["upload"]    = Get-Control "UploadDetail"
$UploadProgressBar = Get-Control "UploadProgressBar"
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
    $UploadProgressBar.Value = 0
    $UploadProgressBar.Visibility = "Collapsed"
    $LogBox.Clear()
    $StatusBanner.Text = ""
}

<#
.SYNOPSIS
  Formats an upload-progress snapshot (see the $onUploadProgress hook below) into the
  step-detail text: current file, per-file and overall percent, throughput, and a
  throughput-based ETA for the remaining bytes.
#>
function Format-UploadProgressDetail {
    param($Progress)
    if (-not $Progress) { return "" }

    $elapsedSec = ((Get-Date) - $Progress.StartedAt).TotalSeconds
    $throughput = if ($elapsedSec -gt 0.5) { $Progress.OverallUploaded / $elapsedSec } else { 0 }
    $remaining = $Progress.OverallSize - $Progress.OverallUploaded
    $etaText = if ($throughput -gt 0 -and $remaining -gt 0) {
        $etaSec = [Math]::Round($remaining / $throughput)
        if ($etaSec -ge 60) { "ETA {0}m {1}s" -f [Math]::Floor($etaSec / 60), ($etaSec % 60) } else { "ETA ${etaSec}s" }
    } else { "" }
    $throughputText = if ($throughput -gt 0) { "$(Format-ByteSize $throughput)/s" } else { "" }
    $filePct = if ($Progress.FileSize -gt 0) { [Math]::Round(($Progress.FileUploaded / $Progress.FileSize) * 100) } else { 0 }

    $fileName = Split-Path -Leaf $Progress.FileName
    $line1 = "$fileName ($($Progress.FileIndex) of $($Progress.FileCount)) — $filePct%"
    $line2 = "$(Format-ByteSize $Progress.OverallUploaded) / $(Format-ByteSize $Progress.OverallSize)" +
        $(if ($throughputText) { " — $throughputText" } else { "" }) +
        $(if ($etaText) { " — $etaText" } else { "" })
    return "$line1`n$line2"
}

function Format-ByteSize {
    param([double]$Bytes)
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N1} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N0} KB" -f ($Bytes / 1KB) }
    return "$([Math]::Round($Bytes)) B"
}

# ============================================================================
# Deploy tab — runs package-release.ps1 then deploy-remote.ps1 in a background
# runspace, reporting through $DeploySync (steps, a log line queue, and the
# latest upload-progress snapshot).
# ============================================================================
$DeploySync = [hashtable]::Synchronized(@{
    Steps    = [hashtable]::Synchronized(@{})
    LogQueue = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
    Running  = $false
    Result   = ""
    ErrorMessage = ""
    UploadProgress = $null
})
foreach ($key in $StepKeys) { $DeploySync.Steps[$key] = @{ Status = "Pending"; Detail = "" } }

$script:DeployWasRunning = $false

<#
.SYNOPSIS
  Shared pipeline runner behind Deploy, "Rerun deploy.sh only", and "Upload .env.prod only" —
  same background runspace, same step/log/progress wiring. -DeployOnly skips packaging and
  just (re)runs deploy.sh against what's already on the VM. -EnvOnly skips packaging AND
  deploy.sh, uploading only .env.prod — running containers are left completely untouched.
#>
function Start-DeployRun {
    param(
        [string]$Tag,
        [bool]$SkipBuild,
        [bool]$NoLoad,
        [string]$EnvProdPath,
        [bool]$DeployOnly,
        [bool]$EnvOnly = $false
    )

    Reset-DeploySteps
    foreach ($key in $StepKeys) { $DeploySync.Steps[$key] = @{ Status = "Pending"; Detail = "" } }
    $DeploySync.LogQueue = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
    $DeploySync.Running = $true
    $DeploySync.Result = ""
    $DeploySync.ErrorMessage = ""
    $DeploySync.UploadProgress = $null
    $script:DeployWasRunning = $true
    $DeployButton.IsEnabled = $false
    $RerunDeployButton.IsEnabled = $false
    $UploadEnvOnlyButton.IsEnabled = $false
    $StatusBanner.Text = if ($EnvOnly) { "Uploading .env.prod…" } elseif ($DeployOnly) { "Rerunning deploy.sh…" } else { "Deploying…" }
    $StatusBanner.Foreground = $BrushRunning

    $rs = [runspacefactory]::CreateRunspace()
    $rs.Open()
    $rs.SessionStateProxy.SetVariable('sync', $DeploySync)
    $rs.SessionStateProxy.SetVariable('ScriptRoot', $ScriptRoot)
    $rs.SessionStateProxy.SetVariable('Tag', $Tag)
    $rs.SessionStateProxy.SetVariable('SkipBuild', $SkipBuild)
    $rs.SessionStateProxy.SetVariable('NoLoad', $NoLoad)
    $rs.SessionStateProxy.SetVariable('EnvProdPath', $EnvProdPath)
    $rs.SessionStateProxy.SetVariable('DeployOnly', $DeployOnly)
    $rs.SessionStateProxy.SetVariable('EnvOnly', $EnvOnly)

    $ps = [powershell]::Create()
    $ps.Runspace = $rs
    [void]$ps.AddScript({
        $onStep = { param($Stage, $Status, $Detail) $sync.Steps[$Stage] = @{ Status = $Status; Detail = $Detail } }
        $onLine = { param($Line) $sync.LogQueue.Enqueue($Line) }
        # $script: here, not a plain local — each `& $onUploadProgress` call gets its own fresh
        # scope, so a plain local write wouldn't persist back across ticks (same reason the SCP
        # upload counters in deploy-remote.ps1 need script scope).
        $script:UploadStartedAt = $null
        $onUploadProgress = {
            param($FileName, $FileUploaded, $FileSize, $OverallUploaded, $OverallSize, $FileIndex, $FileCount)
            if (-not $script:UploadStartedAt) { $script:UploadStartedAt = Get-Date }
            $sync.UploadProgress = @{
                FileName = $FileName; FileUploaded = $FileUploaded; FileSize = $FileSize
                OverallUploaded = $OverallUploaded; OverallSize = $OverallSize
                FileIndex = $FileIndex; FileCount = $FileCount; StartedAt = $script:UploadStartedAt
            }
        }
        try {
            if ($DeployOnly -or $EnvOnly) {
                $skipDetail = if ($EnvOnly) { "Skipped (.env.prod only)" } else { "Skipped (deploy.sh only)" }
                foreach ($skipped in @("preflight", "build", "save", "package")) {
                    $sync.Steps[$skipped] = @{ Status = "Done"; Detail = $skipDetail }
                }
            } else {
                $packageArgs = @{ Tag = $Tag; OnStep = $onStep; OnLine = $onLine }
                if ($SkipBuild) { $packageArgs.SkipBuild = $true }
                if ($EnvProdPath) { $packageArgs.EnvProdPath = $EnvProdPath }
                & (Join-Path $ScriptRoot "package-release.ps1") @packageArgs
            }

            $deployArgs = @{ Tag = $Tag; OnStep = $onStep; OnLine = $onLine; OnUploadProgress = $onUploadProgress }
            if ($NoLoad) { $deployArgs.NoLoad = $true }
            if ($EnvOnly) {
                $deployArgs.EnvOnly = $true
                $deployArgs.EnvProdPath = $EnvProdPath
            } elseif ($DeployOnly) {
                $deployArgs.DeployOnly = $true
            } elseif ($EnvProdPath) {
                $deployArgs.EnvProdPath = $EnvProdPath
            }
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
}

<#
.SYNOPSIS
  Syncs $LocalEnvProdPath from $RootEnvPath, runs the required-variable readiness check, and
  prompts if anything looks wrong. Returns $true to proceed, $false to abort (the user already
  saw why, via the dialogs this shows).
#>
function Sync-AndCheckLocalEnvProd {
    if (-not (Test-Path $RootEnvPath)) {
        [System.Windows.MessageBox]::Show(
            "$RootEnvPath not found, so there's nothing to build .env.prod from.",
            "Can't create .env.prod", "OK", "Error") | Out-Null
        return $false
    }
    Copy-Item -LiteralPath $RootEnvPath -Destination $LocalEnvProdPath -Force

    $problems = Test-LocalEnvProdReadiness -Path $LocalEnvProdPath -RequiredVars $RequiredEnvVars
    if ($problems.Count -gt 0) {
        $proceed = [System.Windows.MessageBox]::Show(
            "$RootEnvPath has problems with required variables:`n`n" +
            ($problems -join "`n") +
            "`n`nUploading this now would overwrite the VM's .env.prod with these values. Fix $RootEnvPath first, or continue anyway?",
            "Local .env looks incomplete", "YesNo", "Error")
        if ($proceed -ne "Yes") { return $false }
    }
    return $true
}

$DeployButton.Add_Click({
    if ($DeploySync.Running) { return }

    $tag = $TagTextBox.Text.Trim()
    if (-not $tag) { $tag = "latest" }
    $uploadEnv = [bool]$UploadEnvCheckBox.IsChecked

    if ($uploadEnv -and -not (Sync-AndCheckLocalEnvProd)) { return }

    $confirmText = "Deploy tag '$tag' to production now?`n`nThis builds, uploads, and restarts the live site."
    if ($uploadEnv) {
        $confirmText += "`n`n⚠ It will ALSO overwrite .env.prod on the VM with $LocalEnvProdPath — including database, RabbitMQ, and API secrets."
    }
    $confirm = [System.Windows.MessageBox]::Show($confirmText, "Confirm deployment", "YesNo", "Warning")
    if ($confirm -ne "Yes") { return }

    Start-DeployRun -Tag $tag -SkipBuild ([bool]$SkipBuildCheckBox.IsChecked) -NoLoad ([bool]$NoLoadCheckBox.IsChecked) `
        -EnvProdPath $(if ($uploadEnv) { $LocalEnvProdPath } else { $null }) -DeployOnly $false
})

$UploadEnvOnlyButton.Add_Click({
    if ($DeploySync.Running) { return }

    if (-not (Sync-AndCheckLocalEnvProd)) { return }

    $confirm = [System.Windows.MessageBox]::Show(
        "Upload $LocalEnvProdPath to the VM's .env.prod now?`n`n" +
        "⚠ This overwrites the VM's .env.prod — including database, RabbitMQ, and API secrets. " +
        "It does NOT touch running containers or run deploy.sh — restart the stack afterward " +
        "('Rerun deploy.sh only') to actually pick up the changes.",
        "Confirm .env.prod upload", "YesNo", "Warning")
    if ($confirm -ne "Yes") { return }

    $tag = $TagTextBox.Text.Trim()
    if (-not $tag) { $tag = "latest" }
    Start-DeployRun -Tag $tag -SkipBuild $false -NoLoad $false -EnvProdPath $LocalEnvProdPath -DeployOnly $false -EnvOnly $true
})

$RerunDeployButton.Add_Click({
    if ($DeploySync.Running) { return }

    $confirm = [System.Windows.MessageBox]::Show(
        "Rerun deploy.sh on the VM now, using whatever release is already uploaded there?`n`n" +
        "This skips building, packaging, and uploading entirely — it only restarts the stack from what's already on the VM. " +
        "Use this to retry a deploy.sh failure (e.g. after fixing .env.prod on the VM directly).",
        "Confirm deploy.sh rerun", "YesNo", "Warning")
    if ($confirm -ne "Yes") { return }

    $tag = $TagTextBox.Text.Trim()
    if (-not $tag) { $tag = "latest" }
    Start-DeployRun -Tag $tag -SkipBuild $false -NoLoad ([bool]$NoLoadCheckBox.IsChecked) -EnvProdPath $null -DeployOnly $true
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

<#
.SYNOPSIS
  Renders Markdown to a FlowDocument, either from $Path (read fresh, the normal case) or
  from $Lines (already-loaded text — used to render a template after substituting live
  values into it without writing a temp file to disk).
#>
function ConvertTo-FlowDocument {
    param([string]$Path, [string[]]$Lines)

    $doc = New-Object System.Windows.Documents.FlowDocument
    $doc.FontFamily = "Segoe UI"
    $doc.FontSize = 13
    $doc.PagePadding = New-Object System.Windows.Thickness(4)

    if (-not $Lines) {
        if (-not (Test-Path $Path)) {
            $p = New-Object System.Windows.Documents.Paragraph
            $p.Inlines.Add("$Path not found.")
            $doc.Blocks.Add($p)
            return $doc
        }
        $Lines = Get-Content -LiteralPath $Path
    }

    $inCode = $false
    $codeParagraph = $null

    foreach ($rawLine in $Lines) {
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
# Exposed Services tab — renders ops/deploy/EXPOSED-SERVICES.md the same way
# Documentation renders DEPLOYMENT.md, except every <SOME_VAR_NAME> placeholder in the
# template is first substituted with that key's real value from the repo-root .env (the
# same file deploy-ui already treats as the source of truth for secrets — see
# Sync-AndCheckLocalEnvProd above). Polled on the main timer below, watching both the
# template and .env's write times, so editing either one shows up here within a tick —
# no re-run, no reopening the window.
# ============================================================================
$script:ExposedServicesLastWriteKey = $null

<#
.SYNOPSIS
  Replaces every <UPPER_SNAKE_CASE> token in $Lines with $EnvValues' value for that key,
  when one exists — a template placeholder with no matching .env entry (e.g. the literal
  <port>/<user> examples in the "tunnel over SSH" line, which are lowercase and never
  match) is left exactly as written rather than guessed at.
#>
function Resolve-ExposedServicesPlaceholders {
    param([string[]]$Lines, [hashtable]$EnvValues)

    return $Lines | ForEach-Object {
        [regex]::Replace($_, '<([A-Z0-9_]+)>', {
            param($m)
            $key = $m.Groups[1].Value
            if ($EnvValues.ContainsKey($key)) { $EnvValues[$key] } else { $m.Value }
        })
    }
}

function Update-ExposedServicesDoc {
    if (-not (Test-Path -LiteralPath $ExposedServicesDocPath)) {
        if ($script:ExposedServicesLastWriteKey -ne "missing") {
            $ExposedServicesViewer.Document = ConvertTo-FlowDocument -Path $ExposedServicesDocPath
            $ExposedServicesMessage.Text = "$ExposedServicesDocPath not found."
            $ExposedServicesMessage.Foreground = $BrushFailed
            $script:ExposedServicesLastWriteKey = "missing"
        }
        return
    }

    $docWriteUtc = (Get-Item -LiteralPath $ExposedServicesDocPath).LastWriteTimeUtc
    $envWriteUtc = if (Test-Path -LiteralPath $RootEnvPath) { (Get-Item -LiteralPath $RootEnvPath).LastWriteTimeUtc } else { [DateTime]::MinValue }
    $writeKey = "$docWriteUtc|$envWriteUtc"
    if ($script:ExposedServicesLastWriteKey -eq $writeKey) { return }

    $envValues = Get-DotEnvValues -Path $RootEnvPath
    $rawLines = Get-Content -LiteralPath $ExposedServicesDocPath
    $filledLines = Resolve-ExposedServicesPlaceholders -Lines $rawLines -EnvValues $envValues

    $ExposedServicesViewer.Document = ConvertTo-FlowDocument -Lines $filledLines
    $ExposedServicesMessage.Text = if ($envValues.Count -gt 0) {
        "Filled in from $RootEnvPath at $(Get-Date -Format 'HH:mm:ss') — showing real values. Auto-refreshes when either file changes."
    } else {
        "$RootEnvPath not found — showing placeholders as written. Auto-refreshes when either file changes."
    }
    $ExposedServicesMessage.Foreground = $window.Resources["Orange"]
    $script:ExposedServicesLastWriteKey = $writeKey
}
Update-ExposedServicesDoc

$ExposedServicesEditButton.Add_Click({
    try { Start-Process $ExposedServicesDocPath } catch {
        [System.Windows.MessageBox]::Show("Couldn't open $ExposedServicesDocPath`: $($_.Exception.Message)", "Can't open file", "OK", "Error") | Out-Null
    }
})

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

<#
.SYNOPSIS
  Local counterpart to the Config Checklist's remote check, used to gate the "also upload
  .env.prod" checkbox: returns one description per required key that's missing, empty, or
  still equal to the .env.prod.example placeholder, so a stale local file can't silently
  overwrite real secrets on the VM.
#>
function Test-LocalEnvProdReadiness {
    param([string]$Path, [object[]]$RequiredVars)

    if (-not (Test-Path $Path)) { return @("(file not found: $Path)") }

    $problems = [System.Collections.Generic.List[string]]::new()
    $lines = Get-Content -LiteralPath $Path
    foreach ($v in $RequiredVars) {
        $line = $lines | Where-Object { $_ -match "^$([regex]::Escape($v.Key))=" } | Select-Object -First 1
        if (-not $line) { $problems.Add("$($v.Key) — missing"); continue }
        $value = $line.Substring($v.Key.Length + 1)
        if ([string]::IsNullOrWhiteSpace($value)) { $problems.Add("$($v.Key) — empty"); continue }
        if ($value -eq $v.Default) { $problems.Add("$($v.Key) — still the example placeholder"); continue }
    }
    return $problems.ToArray()
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
# Shared with the Recreate tab below — same source of truth, read once.
$ComposeServiceNames = try { Get-ComposeServiceNames -ComposeFilePath $ComposeFilePath } catch { @() }

foreach ($service in $ComposeServiceNames) {
    [void]$LogsServiceCombo.Items.Add($service)
}
if ($LogsServiceCombo.Items.Count -gt 0) { $LogsServiceCombo.SelectedIndex = 0 }

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
# Recreate tab — one card per compose service. Each button runs
# `docker compose up -d --force-recreate <service>` scoped to that single
# service (compose only recreates the services named on the command line),
# over the same Posh-SSH session pattern as the other tabs.
# ============================================================================
<#
.SYNOPSIS
  Confirms, then runs `up -d --force-recreate $ServiceName` on the VM in a background
  runspace, updating $Button/$StatusBlock (this card) and $RecreateOutputBox (shared log)
  when it completes. $Button and $StatusBlock are this function's own locals, so the
  -OnComplete scriptblock is bound with GetNewClosure() — by the time it actually runs (a
  later timer tick, back at script scope), this function call's locals would otherwise
  already be gone.
#>
function Start-RecreateService {
    param(
        [Parameter(Mandatory)][string]$ServiceName,
        [Parameter(Mandatory)][System.Windows.Controls.Button]$Button,
        [Parameter(Mandatory)][System.Windows.Controls.TextBlock]$StatusBlock
    )

    $confirm = [System.Windows.MessageBox]::Show(
        "Recreate '$ServiceName' now?`n`n" +
        "Runs 'docker compose up -d --force-recreate $ServiceName' on the VM — picks up the " +
        "current .env.prod and the image already loaded there, for this service only. Causes a " +
        "few seconds of downtime for '$ServiceName'; every other service keeps running untouched.",
        "Confirm recreate", "YesNo", "Warning")
    if ($confirm -ne "Yes") { return }

    $Button.IsEnabled = $false
    $StatusBlock.Text = "Recreating…"
    $StatusBlock.Foreground = $BrushRunning
    $RecreateOutputBox.AppendText("`r`n== Recreating $ServiceName @ $(Get-Date -Format 'HH:mm:ss') ==`r`n")
    $RecreateOutputBox.ScrollToEnd()

    $action = {
        Import-Module Posh-SSH -ErrorAction Stop
        $secrets = Get-Content -LiteralPath $SecretsPath -Raw | ConvertFrom-Json
        $securePw = ConvertTo-SecureString $secrets.password -AsPlainText -Force
        $cred = New-Object System.Management.Automation.PSCredential($secrets.user, $securePw)
        $port = if ($secrets.port) { [int]$secrets.port } else { 22 }
        $session = New-SSHSession -ComputerName $secrets.host -Port $port -Credential $cred -AcceptKey
        try {
            $cmd = "cd '$($secrets.remotePath)' && docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --force-recreate $ServiceName 2>&1"
            $result = Invoke-SSHCommand -SSHSession $session -Command $cmd
            return @{ Output = ($result.Output -join "`n"); ExitStatus = $result.ExitStatus }
        } finally {
            Remove-SSHSession -SSHSession $session | Out-Null
        }
    }

    Start-Async -Variables @{ SecretsPath = $SecretsPath; ServiceName = $ServiceName } -Action $action -OnComplete ({
        param($result, $errorMessage)
        $Button.IsEnabled = $true
        if ($errorMessage) {
            $StatusBlock.Text = "Failed"
            $StatusBlock.Foreground = $BrushFailed
            $RecreateOutputBox.AppendText("ERROR: $errorMessage`r`n")
            $RecreateOutputBox.ScrollToEnd()
            return
        }
        $ok = ($result.ExitStatus -eq 0)
        $StatusBlock.Text = if ($ok) { "Recreated $(Get-Date -Format 'HH:mm:ss')" } else { "Failed (exit $($result.ExitStatus))" }
        $StatusBlock.Foreground = if ($ok) { $BrushDone } else { $BrushFailed }
        $RecreateOutputBox.AppendText("$($result.Output)`r`n")
        $RecreateOutputBox.ScrollToEnd()
    }.GetNewClosure())
}

function New-RecreateCard {
    param([string]$ServiceName)

    $border = New-Object System.Windows.Controls.Border
    $border.Style = $window.Resources["Card"]
    $border.Width = 200
    $border.Margin = New-Object System.Windows.Thickness(0, 0, 12, 12)

    $stack = New-Object System.Windows.Controls.StackPanel

    $nameBlock = New-Object System.Windows.Controls.TextBlock
    $nameBlock.Text = $ServiceName
    $nameBlock.FontWeight = "SemiBold"
    $nameBlock.FontSize = 14
    $nameBlock.TextWrapping = "Wrap"
    $nameBlock.Margin = New-Object System.Windows.Thickness(0, 0, 0, 6)
    [void]$stack.Children.Add($nameBlock)

    $statusBlock = New-Object System.Windows.Controls.TextBlock
    $statusBlock.Text = "Idle"
    $statusBlock.FontSize = 11
    $statusBlock.TextWrapping = "Wrap"
    $statusBlock.Foreground = $window.Resources["Muted"]
    $statusBlock.Margin = New-Object System.Windows.Thickness(0, 0, 0, 10)
    [void]$stack.Children.Add($statusBlock)

    $button = New-Object System.Windows.Controls.Button
    $button.Content = "Recreate"
    $button.Style = $window.Resources["SecondaryButton"]
    $button.HorizontalAlignment = "Stretch"
    [void]$stack.Children.Add($button)

    $button.Add_Click({
        Start-RecreateService -ServiceName $ServiceName -Button $button -StatusBlock $statusBlock
    }.GetNewClosure())

    $border.Child = $stack
    return $border
}

foreach ($service in $ComposeServiceNames) {
    [void]$RecreateServicesPanel.Children.Add((New-RecreateCard -ServiceName $service))
}

# ============================================================================
# Server tab — CPU/memory/disk/swap + system info + docker disk usage, polled on a timer
# while this tab is the active one. One combined SSH round trip per poll (a handful of shell
# one-liners), not four separate connections.
# ============================================================================
$ServerMetricsScript = @'
echo "HOSTNAME=$(hostname)"
echo "UPTIME=$(uptime -p 2>/dev/null || uptime)"
echo "LOADAVG=$(cut -d' ' -f1-3 /proc/loadavg)"
echo "KERNEL=$(uname -r)"
echo "OS=$(. /etc/os-release 2>/dev/null && echo "$PRETTY_NAME")"
echo "CPU_MODEL=$(grep -m1 'model name' /proc/cpuinfo | cut -d: -f2 | sed 's/^ *//')"
echo "CPU_CORES=$(nproc)"
read _ a b c idle1 _ < /proc/stat
sleep 0.3
read _ a2 b2 c2 idle2 _ < /proc/stat
total1=$((a+b+c+idle1)); total2=$((a2+b2+c2+idle2))
totald=$((total2-total1)); idled=$((idle2-idle1))
if [ "$totald" -gt 0 ]; then cpupct=$(( (1000*(totald-idled)/totald+5)/10 )); else cpupct=0; fi
echo "CPU_PCT=$cpupct"
free -k | awk '/^Mem:/ {print "MEM_TOTAL_KB="$2; print "MEM_USED_KB="$3; print "MEM_AVAIL_KB="$7} /^Swap:/ {print "SWAP_TOTAL_KB="$2; print "SWAP_USED_KB="$3}'
df / | awk 'NR==2 {print "DISK_TOTAL_KB="$2; print "DISK_USED_KB="$3; print "DISK_AVAIL_KB="$4; gsub("%","",$5); print "DISK_PCT="$5}'
echo "===DOCKER_DF==="
docker system df 2>/dev/null
echo "===END==="
'@
# This file is saved with Windows CRLF line endings, which land verbatim inside the here-
# string above. Sent as-is to the VM's bash, the embedded \r corrupts the script right at
# the `read ... < /proc/stat` line (\r attaches to the redirect target, an invalid path) and
# silently truncates everything after it. Strip them before this is ever sent over SSH.
$ServerMetricsScript = $ServerMetricsScript -replace "`r", ""

function Format-Gb {
    param([double]$Kb)
    return "{0:N1} GB" -f ($Kb / 1MB)
}

function ConvertFrom-ServerMetrics {
    param([string]$Output)
    $data = @{}
    $dockerLines = [System.Collections.Generic.List[string]]::new()
    $inDockerBlock = $false
    foreach ($line in ($Output -split "`n")) {
        $trimmed = $line.Trim()
        if ($trimmed -eq "===DOCKER_DF===") { $inDockerBlock = $true; continue }
        if ($trimmed -eq "===END===") { $inDockerBlock = $false; continue }
        if ($inDockerBlock) { $dockerLines.Add($line); continue }
        if ($line -match '^([A-Z_]+)=(.*)$') { $data[$Matches[1]] = $Matches[2] }
    }
    $data['DOCKER_DF'] = ($dockerLines -join "`n").Trim()
    return $data
}

# Traffic-light thresholds: comfortable below 70%, watch 70-90%, hot above 90%.
function Get-MetricBrush {
    param([double]$Percent)
    if ($Percent -ge 90) { return $BrushFailed }
    if ($Percent -ge 70) { return $window.Resources["Orange"] }
    return $BrushDone
}

function Update-ServerMetricsUi {
    param([hashtable]$Data)

    $cpuPct = [double]($Data['CPU_PCT'])
    $CpuPctText.Text = "$cpuPct%"
    $CpuPctText.Foreground = Get-MetricBrush $cpuPct
    $CpuBar.Value = $cpuPct
    $CpuSubText.Text = "$($Data['CPU_CORES']) core(s)"

    $memTotal = [double]($Data['MEM_TOTAL_KB'])
    $memUsed  = [double]($Data['MEM_USED_KB'])
    $memPct = if ($memTotal -gt 0) { [Math]::Round(($memUsed / $memTotal) * 100) } else { 0 }
    $MemPctText.Text = "$memPct%"
    $MemPctText.Foreground = Get-MetricBrush $memPct
    $MemBar.Value = $memPct
    $MemSubText.Text = "$(Format-Gb $memUsed) / $(Format-Gb $memTotal)"

    $diskPct = [double]($Data['DISK_PCT'])
    $DiskPctText.Text = "$diskPct%"
    $DiskPctText.Foreground = Get-MetricBrush $diskPct
    $DiskBar.Value = $diskPct
    $DiskSubText.Text = "$(Format-Gb ([double]($Data['DISK_USED_KB']))) / $(Format-Gb ([double]($Data['DISK_TOTAL_KB'])))"

    $swapTotal = [double]($Data['SWAP_TOTAL_KB'])
    $swapUsed  = [double]($Data['SWAP_USED_KB'])
    $swapPct = if ($swapTotal -gt 0) { [Math]::Round(($swapUsed / $swapTotal) * 100) } else { 0 }
    $SwapPctText.Text = if ($swapTotal -gt 0) { "$swapPct%" } else { "n/a" }
    $SwapPctText.Foreground = Get-MetricBrush $swapPct
    $SwapBar.Value = $swapPct
    $SwapSubText.Text = if ($swapTotal -gt 0) { "$(Format-Gb $swapUsed) / $(Format-Gb $swapTotal)" } else { "no swap configured" }

    $InfoHostname.Text = $Data['HOSTNAME']
    $InfoOs.Text = $Data['OS']
    $InfoKernel.Text = $Data['KERNEL']
    $InfoCpuModel.Text = $Data['CPU_MODEL']
    $InfoCores.Text = $Data['CPU_CORES']
    $InfoUptime.Text = $Data['UPTIME']
    $InfoLoadAvg.Text = $Data['LOADAVG']
    $DockerDfBox.Text = $Data['DOCKER_DF']
}

$script:ServerPollInFlight = $false

function Start-ServerMetricsPoll {
    if ($script:ServerPollInFlight) { return }
    $script:ServerPollInFlight = $true
    $ServerMessage.Text = "Refreshing…"
    $ServerMessage.Foreground = $window.Resources["Muted"]

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

    Start-Async -Variables @{ SecretsPath = $SecretsPath; RemoteScript = $ServerMetricsScript } -Action $action -OnComplete {
        param($result, $errorMessage)
        $script:ServerPollInFlight = $false
        if ($errorMessage) {
            $ServerMessage.Text = "Failed: $errorMessage"
            $ServerMessage.Foreground = $BrushFailed
            return
        }
        Update-ServerMetricsUi -Data (ConvertFrom-ServerMetrics -Output $result)
        $ServerMessage.Text = "Updated $(Get-Date -Format 'HH:mm:ss')"
        $ServerMessage.Foreground = $window.Resources["Muted"]
    }
}

$ServerRefreshButton.Add_Click({ Start-ServerMetricsPoll })

$serverTimer = New-Object System.Windows.Threading.DispatcherTimer
$serverTimer.Interval = [TimeSpan]::FromSeconds(4)
$serverTimer.Add_Tick({
    if ($MainTabControl.SelectedItem -eq $ServerTabItem) { Start-ServerMetricsPoll }
})
$serverTimer.Start()
$MainTabControl.Add_SelectionChanged({
    if ($MainTabControl.SelectedItem -eq $ServerTabItem) { Start-ServerMetricsPoll }
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

    Update-ExposedServicesDoc

    $doneCount = 0
    foreach ($key in $StepKeys) {
        $info = $DeploySync.Steps[$key]
        $detail = $info.Detail
        if ($key -eq "upload" -and $info.Status -eq "Running" -and $DeploySync.UploadProgress) {
            $detail = Format-UploadProgressDetail -Progress $DeploySync.UploadProgress
        }
        Set-StepVisual -Key $key -Status $info.Status -Detail $detail
        if ($info.Status -eq "Done") { $doneCount += 1 }
    }
    $OverallProgressBar.Value = ($doneCount / $StepKeys.Count) * 100

    if ($DeploySync.UploadProgress -and $DeploySync.Steps["upload"].Status -eq "Running") {
        $p = $DeploySync.UploadProgress
        $UploadProgressBar.Visibility = "Visible"
        $UploadProgressBar.Value = if ($p.OverallSize -gt 0) { ($p.OverallUploaded / $p.OverallSize) * 100 } else { 0 }
    } elseif ($DeploySync.Steps["upload"].Status -ne "Running") {
        $UploadProgressBar.Visibility = "Collapsed"
    }

    if (-not $DeploySync.Running -and $script:DeployWasRunning) {
        $script:DeployWasRunning = $false
        $DeployButton.IsEnabled = $true
        $RerunDeployButton.IsEnabled = $true
        $UploadEnvOnlyButton.IsEnabled = $true
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
